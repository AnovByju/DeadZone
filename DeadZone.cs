// DeadZone v2 - makes Windows treat only the working part of a damaged laptop panel as "the screen".
//
// How it works: a virtual monitor (Virtual Display Driver) is the real desktop, sized to the working
// area only. Windows, the taskbar, fullscreen buttons, games, etc. all use that smaller monitor.
// This program copies that virtual monitor's frames, on the GPU, onto the working part of the laptop panel.
// Idle cost is near zero: it only does work when the screen actually changes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct CURSORINFO { public int cbSize; public int flags; public IntPtr hCursor; public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct ICONINFO { public int fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int v);
    [DllImport("user32.dll")] public static extern bool ClipCursor(ref RECT r);
    [DllImport("user32.dll")] public static extern bool ClipCursor(IntPtr r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO ci);
    [DllImport("user32.dll")] public static extern bool GetIconInfo(IntPtr h, out ICONINFO i);
    [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr h, int cx, int cy, uint step, IntPtr br, uint flags);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;
}

// ---- Minimal Direct3D11 / DXGI access through raw COM vtables (no external libraries) ----
static class Dx
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TexDesc { public uint W, H, Mips, ArrSize, Format, SC, SQ, Usage, Bind, Cpu, Misc; }
    [StructLayout(LayoutKind.Sequential)]
    public struct SwapDesc { public uint W, H, RN, RD, Format, Scan, Scaling, SC, SQ, Usage, Buffers; public IntPtr Wnd; public int Windowed, Effect; public uint Flags; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DQI(IntPtr s, ref Guid iid, out IntPtr o);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint DRel(IntPtr s);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DEnum(IntPtr s, uint i, out IntPtr o);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DGetDesc(IntPtr s, IntPtr buf);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DDup(IntPtr s, IntPtr dev, out IntPtr dup);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DAcq(IntPtr s, uint ms, IntPtr info, out IntPtr res);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DRelF(IntPtr s);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DCreateTex(IntPtr s, ref TexDesc d, IntPtr init, out IntPtr tex);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DCopyReg(IntPtr s, IntPtr dst, uint ds, uint x, uint y, uint z, IntPtr src, uint ss, IntPtr box);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DCopyRes(IntPtr s, IntPtr dst, IntPtr src);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DCreateSwap(IntPtr s, IntPtr dev, ref SwapDesc d, out IntPtr sc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DAssoc(IntPtr s, IntPtr wnd, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DPresent(IntPtr s, uint sync, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DGetBuffer(IntPtr s, uint i, ref Guid iid, out IntPtr o);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DGetDC(IntPtr s, int discard, out IntPtr hdc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int DRelDC(IntPtr s, IntPtr rect);

    [DllImport("dxgi.dll")] public static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr f);
    [DllImport("d3d11.dll")] public static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr sw, uint flags,
        IntPtr levels, uint n, uint sdk, out IntPtr dev, out int fl, out IntPtr ctx);

    public static T Fn<T>(IntPtr obj, int slot) where T : class
    {
        IntPtr vt = Marshal.ReadIntPtr(obj);
        IntPtr fp = Marshal.ReadIntPtr(vt, slot * IntPtr.Size);
        return (T)(object)Marshal.GetDelegateForFunctionPointer(fp, typeof(T));
    }
    public static void Rel(IntPtr p) { if (p != IntPtr.Zero) Fn<DRel>(p, 2)(p); }
    public static IntPtr QI(IntPtr p, Guid iid)
    {
        IntPtr o;
        if (Fn<DQI>(p, 0)(p, ref iid, out o) != 0) return IntPtr.Zero;
        return o;
    }
}

// Copies the virtual monitor to the working part of the physical panel on a background thread.
class Presenter
{
    Thread th;
    volatile bool stop;
    public volatile int Frames;
    public volatile bool Alive;
    public volatile bool DrawCursor = true;
    Dictionary<IntPtr, Point> hots = new Dictionary<IntPtr, Point>();

    public void Start(IntPtr hwnd, Rectangle vb, Size phys, int cut)
    {
        Stop();
        stop = false; Frames = 0; Alive = true;
        th = new Thread(delegate()
        {
            try { Run(hwnd, vb, phys, cut); } catch (Exception) { }
            Alive = false;
        });
        th.IsBackground = true;
        th.Start();
    }

    public void Stop()
    {
        stop = true;
        if (th != null) { th.Join(800); th = null; }
        Alive = false; Frames = 0;
    }

    void Run(IntPtr hwnd, Rectangle vb, Size phys, int cut)
    {
        const int WAIT_TIMEOUT = unchecked((int)0x887A0027);
        Guid iidFactory = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        Guid iidTex = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        Guid iidSurf = new Guid("4AE63092-6327-4c1b-80AE-BFE12EA32B86");
        Guid iidOut1 = new Guid("00cddea8-939b-4b83-a340-a685226666cc");

        List<IntPtr> refs = new List<IntPtr>();
        IntPtr info = Marshal.AllocHGlobal(64);
        IntPtr dup = IntPtr.Zero;
        try
        {
            IntPtr factory;
            if (Dx.CreateDXGIFactory1(ref iidFactory, out factory) != 0) return;
            refs.Add(factory);

            // find the DXGI output that is the virtual monitor (match its desktop rectangle)
            IntPtr adapter = IntPtr.Zero, output = IntPtr.Zero;
            for (uint a = 0; output == IntPtr.Zero; a++)
            {
                IntPtr ad;
                if (Dx.Fn<Dx.DEnum>(factory, 12)(factory, a, out ad) != 0) break;
                for (uint o = 0; ; o++)
                {
                    IntPtr op;
                    if (Dx.Fn<Dx.DEnum>(ad, 7)(ad, o, out op) != 0) break;
                    IntPtr buf = Marshal.AllocHGlobal(128);
                    Dx.Fn<Dx.DGetDesc>(op, 7)(op, buf);
                    int l = Marshal.ReadInt32(buf, 64), t = Marshal.ReadInt32(buf, 68);
                    int r = Marshal.ReadInt32(buf, 72), b = Marshal.ReadInt32(buf, 76);
                    Marshal.FreeHGlobal(buf);
                    if (l == vb.Left && t == vb.Top && r == vb.Right && b == vb.Bottom) { output = op; adapter = ad; break; }
                    Dx.Rel(op);
                }
                if (output == IntPtr.Zero) Dx.Rel(ad);
            }
            if (output == IntPtr.Zero) return;
            refs.Add(adapter); refs.Add(output);

            IntPtr dev, ctx; int fl;
            if (Dx.D3D11CreateDevice(adapter, 0, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out dev, out fl, out ctx) != 0) return;
            refs.Add(dev); refs.Add(ctx);

            IntPtr out1 = Dx.QI(output, iidOut1);
            if (out1 == IntPtr.Zero) return;
            refs.Add(out1);

            Dx.SwapDesc sd = new Dx.SwapDesc();
            sd.W = (uint)phys.Width; sd.H = (uint)phys.Height; sd.RN = 60; sd.RD = 1;
            sd.Format = 87; sd.SC = 1; sd.Usage = 0x20; sd.Buffers = 1;
            sd.Wnd = hwnd; sd.Windowed = 1; sd.Effect = 0; sd.Flags = 4; // 4 = GDI compatible (for the cursor)
            IntPtr sc;
            if (Dx.Fn<Dx.DCreateSwap>(factory, 10)(factory, dev, ref sd, out sc) != 0) return;
            refs.Add(sc);
            Dx.Fn<Dx.DAssoc>(factory, 8)(factory, hwnd, 3); // no Alt+Enter fullscreen toggle

            IntPtr back;
            if (Dx.Fn<Dx.DGetBuffer>(sc, 9)(sc, 0, ref iidTex, out back) != 0) return;
            refs.Add(back);
            IntPtr surf = Dx.QI(back, iidSurf);
            if (surf != IntPtr.Zero) refs.Add(surf);

            Dx.TexDesc td = new Dx.TexDesc();
            td.W = (uint)vb.Width; td.H = (uint)vb.Height; td.Mips = 1; td.ArrSize = 1; td.Format = 87; td.SC = 1;
            IntPtr desk;
            if (Dx.Fn<Dx.DCreateTex>(dev, 5)(dev, ref td, IntPtr.Zero, out desk) != 0) return;
            refs.Add(desk);

            Dx.DCopyRes copyRes = Dx.Fn<Dx.DCopyRes>(ctx, 47);
            Dx.DCopyReg copyReg = Dx.Fn<Dx.DCopyReg>(ctx, 46);
            Dx.DPresent present = Dx.Fn<Dx.DPresent>(sc, 8);
            Dx.DAcq acq = null; Dx.DRelF relf = null;

            while (!stop)
            {
                if (dup == IntPtr.Zero)
                {
                    if (Dx.Fn<Dx.DDup>(out1, 22)(out1, dev, out dup) != 0) { dup = IntPtr.Zero; Thread.Sleep(250); continue; }
                    acq = Dx.Fn<Dx.DAcq>(dup, 8); relf = Dx.Fn<Dx.DRelF>(dup, 14);
                }
                IntPtr res;
                int hr = acq(dup, 100, info, out res);       // blocks until something changes: idle = ~0% CPU
                if (hr == WAIT_TIMEOUT) continue;
                if (hr != 0) { Dx.Rel(dup); dup = IntPtr.Zero; Thread.Sleep(100); continue; } // lost access (lock screen, UAC...) -> retry
                if (Marshal.ReadInt64(info, 0) != 0)         // LastPresentTime != 0 -> new picture, not just a mouse move
                {
                    IntPtr tex = Dx.QI(res, iidTex);
                    if (tex != IntPtr.Zero) { copyRes(ctx, desk, tex); Dx.Rel(tex); }
                }
                Dx.Rel(res);
                relf(dup);

                copyReg(ctx, back, 0, (uint)cut, 0, 0, desk, 0, IntPtr.Zero);
                if (DrawCursor && surf != IntPtr.Zero) DrawCur(surf, vb, cut);
                if (present(sc, 1, 0) < 0) break;
                Frames++;
            }
        }
        finally
        {
            Dx.Rel(dup);
            for (int i = refs.Count - 1; i >= 0; i--) Dx.Rel(refs[i]);
            Marshal.FreeHGlobal(info);
        }
    }

    // The virtual monitor's hardware cursor is not part of the captured image, so draw it ourselves.
    void DrawCur(IntPtr surf, Rectangle vb, int cut)
    {
        Native.CURSORINFO ci = new Native.CURSORINFO();
        ci.cbSize = Marshal.SizeOf(typeof(Native.CURSORINFO));
        if (!Native.GetCursorInfo(ref ci) || (ci.flags & 1) == 0 || ci.hCursor == IntPtr.Zero) return;
        if (ci.x < vb.Left || ci.x >= vb.Right || ci.y < vb.Top || ci.y >= vb.Bottom) return;

        Point hot;
        if (!hots.TryGetValue(ci.hCursor, out hot))
        {
            Native.ICONINFO ii;
            hot = Point.Empty;
            if (Native.GetIconInfo(ci.hCursor, out ii))
            {
                hot = new Point(ii.xHotspot, ii.yHotspot);
                if (ii.hbmMask != IntPtr.Zero) Native.DeleteObject(ii.hbmMask);
                if (ii.hbmColor != IntPtr.Zero) Native.DeleteObject(ii.hbmColor);
            }
            hots[ci.hCursor] = hot;
        }
        IntPtr hdc;
        if (Dx.Fn<Dx.DGetDC>(surf, 11)(surf, 0, out hdc) != 0) return;
        Native.DrawIconEx(hdc, ci.x - vb.Left + cut - hot.X, ci.y - vb.Top - hot.Y, ci.hCursor, 0, 0, 0, IntPtr.Zero, 3);
        Dx.Fn<Dx.DRelDC>(surf, 12)(surf, IntPtr.Zero);
    }
}

// Full-screen overlay where you drag the line (shown on the physical laptop panel).
class SetupForm : Form
{
    public int Cut;
    Rectangle sb;

    public SetupForm(Rectangle bounds, int initial)
    {
        sb = bounds;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Cursor = Cursors.VSplit;
        Cut = initial > 0 ? initial : bounds.Width * 3 / 10;
    }

    void SetCut(int x)
    {
        Cut = Math.Max(16, Math.Min(sb.Width - 400, x));
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) SetCut(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) SetCut(e.X); }

    protected override bool ProcessCmdKey(ref Message m, Keys k)
    {
        int step = (Control.ModifierKeys & Keys.Shift) != 0 ? 10 : 1;
        if (k == Keys.Left || k == (Keys.Left | Keys.Shift)) { SetCut(Cut - step); return true; }
        if (k == Keys.Right || k == (Keys.Right | Keys.Shift)) { SetCut(Cut + step); return true; }
        if (k == Keys.Enter) { DialogResult = DialogResult.OK; return true; }
        if (k == Keys.Escape) { DialogResult = DialogResult.Cancel; return true; }
        return base.ProcessCmdKey(ref m, k);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Color.FromArgb(30, 34, 48));
        using (Pen grid = new Pen(Color.FromArgb(60, 255, 255, 255)))
        {
            for (int x = 0; x < sb.Width; x += 100) g.DrawLine(grid, x, 0, x, sb.Height);
            for (int y = 0; y < sb.Height; y += 100) g.DrawLine(grid, 0, y, sb.Width, y);
        }
        using (Brush dead = new SolidBrush(Color.FromArgb(150, 200, 30, 30)))
            g.FillRectangle(dead, 0, 0, Cut, sb.Height);
        using (Pen line = new Pen(Color.Cyan, 3))
            g.DrawLine(line, Cut, 0, Cut, sb.Height);

        string txt = string.Format(
            "Drag the cyan line to the edge of the part of your screen that works.\n" +
            "Arrow keys fine-tune (Shift = 10 px).   Enter = save   Esc = cancel\n\n" +
            "Hidden:  {0} px  ({1:0.#}%)\nUsable (virtual display size):  {2} x {3}",
            Cut, 100.0 * Cut / sb.Width, sb.Width - Cut, sb.Height);
        using (Font f = new Font("Segoe UI", 16))
            g.DrawString(txt, f, Brushes.White, Cut + 40, sb.Height / 3);
    }
}

// Black, click-through, no-activate window. It carries the mirrored picture on the laptop panel.
class CoverForm : Form
{
    public CoverForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080 | 0x08000000 | 0x00000020; // TOOLWINDOW | NOACTIVATE | TRANSPARENT
            return cp;
        }
    }
}

// Hidden window that receives the emergency hotkey (Ctrl+Alt+Shift+X = quit and release everything).
class HotKeyWindow : NativeWindow, IDisposable
{
    public event EventHandler Pressed;
    public HotKeyWindow()
    {
        CreateHandle(new CreateParams());
        Native.RegisterHotKey(Handle, 1, 0x4007, 0x58);
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && Pressed != null) Pressed(this, EventArgs.Empty);
        base.WndProc(ref m);
    }
    public void Dispose() { Native.UnregisterHotKey(Handle, 1); DestroyHandle(); }
}

class Zone : ApplicationContext
{
    NotifyIcon tray;
    CoverForm cover = new CoverForm();
    System.Windows.Forms.Timer timer;
    Presenter pres = new Presenter();
    HotKeyWindow hk;
    int cut, W, H, lastStart;
    Rectangle curVb, curPhys;
    string status = "";
    ToolStripMenuItem startupItem, cursorItem;

    public Zone(int cutPx, int w, int h)
    {
        cut = cutPx; W = w; H = h;

        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Re-adjust line...", null, delegate { Reconfigure(); });
        cursorItem = new ToolStripMenuItem("Draw mouse cursor (turn off if you see two)");
        cursorItem.Checked = true;
        cursorItem.Click += delegate { cursorItem.Checked = !cursorItem.Checked; pres.DrawCursor = cursorItem.Checked; };
        menu.Items.Add(cursorItem);
        startupItem = new ToolStripMenuItem("Start with Windows");
        startupItem.Checked = Program.GetStartup();
        startupItem.Click += delegate { startupItem.Checked = !startupItem.Checked; Program.SetStartup(startupItem.Checked); };
        menu.Items.Add(startupItem);
        menu.Items.Add("Exit (Ctrl+Alt+Shift+X)", null, delegate { Quit(); });

        tray = new NotifyIcon();
        tray.Icon = SystemIcons.Application;
        tray.Text = "DeadZone";
        tray.ContextMenuStrip = menu;
        tray.Visible = true;

        hk = new HotKeyWindow();
        hk.Pressed += delegate { Quit(); };

        timer = new System.Windows.Forms.Timer();
        timer.Interval = 1000;
        timer.Tick += delegate { Tick(); };
        timer.Start();
        Tick();
    }

    void SetStatus(string s)
    {
        if (s == status) return;
        status = s;
        tray.Text = "DeadZone - " + s;
    }

    // Once a second (cheap): find the two monitors, (re)start the presenter, keep the mouse on the virtual monitor.
    void Tick()
    {
        Screen phys = null, virt = null;
        foreach (Screen s in Screen.AllScreens)
        {
            if (s.Bounds.Width == W && s.Bounds.Height == H) phys = s;
            else if (s.Bounds.Width == W - cut) virt = s;
        }
        if (phys == null || virt == null)
        {
            SetStatus("waiting for virtual display " + (W - cut) + "x" + H);
            StopPresenting();
            return;
        }
        bool moved = curVb != virt.Bounds || curPhys != phys.Bounds;
        if ((!pres.Alive && Environment.TickCount - lastStart > 3000) || moved) StartPresenting(phys.Bounds, virt.Bounds);

        if (pres.Alive && pres.Frames > 0)
        {
            if (!cover.Visible) cover.Show();
            Native.SetWindowPos(cover.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            Rectangle vb = virt.Bounds;
            Native.RECT clip = new Native.RECT();
            clip.Left = vb.Left; clip.Top = vb.Top; clip.Right = vb.Right; clip.Bottom = vb.Bottom;
            Native.ClipCursor(ref clip);   // re-applied every second: Windows resets it on lock / UAC
            SetStatus("mirroring");
        }
        else
        {
            if (cover.Visible) cover.Hide();
            Native.ClipCursor(IntPtr.Zero);
            SetStatus("starting...");
        }
    }

    void StartPresenting(Rectangle physB, Rectangle vb)
    {
        pres.Stop();
        if (cover.Visible) cover.Hide();
        cover.Bounds = physB;
        IntPtr h = cover.Handle;
        curVb = vb; curPhys = physB; lastStart = Environment.TickCount;
        pres.Start(h, vb, physB.Size, cut);
    }

    void StopPresenting()
    {
        pres.Stop();
        if (cover.Visible) cover.Hide();
        Native.ClipCursor(IntPtr.Zero);
        curVb = Rectangle.Empty;
    }

    void Reconfigure()
    {
        timer.Stop();
        StopPresenting();
        Rectangle b = Screen.PrimaryScreen.Bounds;
        foreach (Screen s in Screen.AllScreens) if (s.Bounds.Width == W && s.Bounds.Height == H) b = s.Bounds;
        int c = Program.RunSetup(b, cut);
        if (c > 0)
        {
            cut = c; W = b.Width; H = b.Height;
            Program.Save(cut, W, H);
            Program.ShowGuide(cut, W, H);
        }
        timer.Start();
        Tick();
    }

    void Quit()
    {
        timer.Stop();
        StopPresenting();
        hk.Dispose();
        cover.Dispose();
        tray.Visible = false;
        tray.Dispose();
        ExitThread();
    }
}

static class Program
{
    static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeadZone");
    static string Cfg = Path.Combine(Dir, "config.txt");
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static int RunSetup(Rectangle bounds, int initial)
    {
        using (SetupForm f = new SetupForm(bounds, initial))
            return f.ShowDialog() == DialogResult.OK ? f.Cut : 0;
    }

    public static void Save(int cut, int w, int h)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Cfg, cut + "," + w + "," + h);
    }

    static bool Load(out int cut, out int w, out int h)
    {
        cut = 0; w = 0; h = 0;
        try
        {
            string[] p = File.ReadAllText(Cfg).Split(',');
            cut = int.Parse(p[0]); w = int.Parse(p[1]); h = int.Parse(p[2]);
            return cut > 0 && w > 0 && h > 0;
        }
        catch (Exception) { return false; }
    }

    public static void ShowGuide(int cut, int w, int h)
    {
        MessageBox.Show(string.Format(
            "Saved. {0} px of the left side will be hidden.\n\n" +
            "One-time setup:\n" +
            "1. Install the Virtual Display Driver:  winget install VirtualDrivers.Virtual-Display-Driver\n" +
            "   Then add a {1} x {2} resolution to it (Virtual Driver Control app, or C:\\VirtualDisplayDriver\\vdd_settings.xml).\n" +
            "2. Settings > System > Display: set the virtual display to {1} x {2}, select it and tick \"Make this my main display\". " +
            "Keep your laptop screen as an extended display (do not disconnect it).\n" +
            "3. DeadZone (tray icon) then shows the virtual display on the working part of your laptop panel.\n\n" +
            "Emergency exit: Ctrl+Alt+Shift+X.",
            cut, w - cut, h), "DeadZone", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static bool GetStartup()
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && k.GetValue("DeadZone") != null;
    }

    public static void SetStartup(bool on)
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (on) k.SetValue("DeadZone", "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue("DeadZone", false);
        }
    }

    [STAThread]
    static void Main()
    {
        bool created;
        using (Mutex m = new Mutex(true, "DeadZone_SingleInstance", out created))
        {
            if (!created) return;
            try { Native.SetProcessDpiAwareness(2); } catch (Exception) { Native.SetProcessDPIAware(); }
            Application.EnableVisualStyles();

            int cut, w, h;
            if (!Load(out cut, out w, out h))
            {
                Rectangle b = Screen.PrimaryScreen.Bounds;
                cut = RunSetup(b, 0);
                if (cut <= 0) return;
                w = b.Width; h = b.Height;
                Save(cut, w, h);
                SetStartup(true);
                ShowGuide(cut, w, h);
            }
            Application.Run(new Zone(cut, w, h));
        }
    }
}
