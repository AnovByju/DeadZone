DeadZone
Use the working part of a damaged laptop display as your Windows desktop.

DeadZone is an experimental Windows utility for laptops whose LCD panel has a physically damaged strip along one side. It uses a virtual monitor as the Windows desktop and mirrors that virtual monitor onto the working portion of the laptop's physical panel.
[!IMPORTANT]
DeadZone is experimental. The supplied C# source and batch files have not been verified on a real Windows laptop as part of this README preparation. The lock-screen helper and display-mirroring path in particular need real-hardware testing. Back up important data and make sure you have a recovery route before installing a display driver or the Windows service.

Contents
- How DeadZone works
- Requirements
- Project files
- Installation
- First-time setup
- Using DeadZone
- Lock-screen helper service
- Performance and limitations
- Troubleshooting
- Updating DeadZone
- Uninstalling
- How configuration is stored
- Credits and related projects
How DeadZone works
A normal Windows application cannot redefine the physical dimensions of a laptop's built-in LCD panel. A conventional custom resolution also does not reliably make Windows place the desktop only on the undamaged portion of that panel.

DeadZone takes a different approach:
1. Choose the damaged strip. On first launch, a full-screen setup grid appears. Drag the cyan vertical line to the point where the usable part of the LCD begins.
2. Create a virtual monitor. A separate virtual display is configured to the usable width and the panel's full height.
3. Make the virtual monitor the Windows primary display. Windows renders the desktop, taskbar, application windows and normal full-screen content on this smaller virtual monitor.
4. Mirror it to the laptop panel. DeadZone captures frames from the virtual monitor using the Windows Desktop Duplication API and Direct3D 11. It presents those frames on the laptop panel, offset to the right of the damaged strip.
5. Keep input on the virtual monitor. DeadZone clips the mouse to the virtual display. The mirror draws the virtual display's mouse cursor on the physical panel.
6. Move misplaced application windows. A lightweight window check runs every 300 ms and attempts to move visible top-level application windows that open on the original laptop panel onto the virtual display.
For example, with a physical panel of 1920 x 1080 and a left-side damaged strip of 576 pixels, the virtual display must be configured to:
1920 - 576 = 1344 pixels wide
1080 pixels high

Required virtual resolution: 1344 x 1080
DeadZone does not alter the LCD hardware or change the physical monitor's EDID. Windows uses the virtual monitor as the desktop display; the original laptop panel remains connected as the surface onto which DeadZone mirrors the picture.
Requirements
- Windows 10 or Windows 11.
- A Direct3D 11-compatible graphics device and a working graphics driver.
- The Virtual Display Driver (VDD).
- The separate Virtual Driver Control application to manage virtual displays and their resolutions.
- The latest supported Microsoft Visual C++ Redistributable, required by the Virtual Display Driver. Install it if the driver reports a missing vcruntime140.dll dependency.
- The Windows .NET Framework C# compiler used by build.bat (csc.exe, normally found under %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\ or %WINDIR%\Microsoft.NET\Framework\v4.0.30319\).
- Administrator permission to install the virtual display driver and the optional DeadZone Windows service.
Display setup assumptions: DeadZone is designed for one damaged laptop panel plus one virtual display. On first launch it measures the current primary display. Set the laptop panel as the primary display before running DeadZone for the first time. Disconnect or temporarily disable unrelated external monitors during initial configuration if possible, especially monitors that use the same resolution as either DeadZone display.
Project files
Place these files together in one folder. The names matter because the batch files refer to them directly.
DeadZone/
├── DeadZone.cs
├── build.bat
├── install_service.bat
├── uninstall_service.bat
└── README.md
If you downloaded the source files with browser-added suffixes, rename them before building:
- DeadZone(2).cs → DeadZone.cs
- build(3).bat → build.bat
Keep install_service.bat and uninstall_service.bat with those exact names. The build script expects DeadZone.cs in the same folder. The service installer expects DeadZone.exe in that same folder.
Installation
1. Download the DeadZone files
Download or clone this repository. Extract all project files into a permanent folder. For example:
C:\DeadZone
Do not put the project in a temporary Downloads extraction folder if you plan to install the lock-screen service. The service stores the executable's path and expects it to remain there.
2. Build DeadZone.exe
1. Open C:\DeadZone in File Explorer.
2. Click the address bar, type cmd, and press Enter. This opens Command Prompt in the project folder.
3. Run:
build.bat
4. Wait for the message:
Built DeadZone.exe
5. Confirm that DeadZone.exe appears in the project folder.
The build script uses the C# compiler included with the .NET Framework and references Windows Forms, Drawing and ServiceProcess. You do not need Visual Studio to run this supplied build script. If the compiler cannot be found, check that the .NET Framework compiler is present at one of the paths listed in Requirements.
3. Install the Virtual Display Driver
DeadZone needs a virtual monitor before mirroring can begin.

Option A — install the driver with WinGet

Open Terminal or Command Prompt and run:
winget install --id=VirtualDrivers.Virtual-Display-Driver -e
This installs the Virtual Display Driver package. It does not mean that the separate Virtual Driver Control application will necessarily appear in the Start menu.

Option B — use Virtual Driver Control
1. Open the Virtual Display Driver GitHub repository and its Releases page.
2. Download the current Virtual Driver Control package linked by the project, or use the Virtual Driver Control repository and its Releases page.
3. Extract the downloaded archive to a permanent folder.
4. Run the Virtual Driver Control application from the extracted folder. It is a separate application; it may not be installed as a conventional Start-menu program.
5. Use its driver controls to install the Virtual Display Driver if it is not already installed.
The upstream driver project documents both the WinGet package and the separate control application. It also documents C:\VirtualDisplayDriver\vdd_settings.xml as the driver's settings file. Prefer the control application's display/resolution editor instead of manually editing the XML unless you understand the driver's configuration format. The upstream project also advises uninstalling VDD before major GPU/chipset driver updates, then reinstalling it afterward if needed.
4. Check that the virtual display is available
1. Open Device Manager and expand Display adapters to check that the virtual display driver is installed.
2. Open Settings → System → Display.
3. Confirm that Windows lists the laptop panel and the virtual monitor.
4. In Virtual Driver Control, add/enable one virtual display if one is not already present.
Do not select Disconnect this display for the laptop's physical panel. DeadZone needs that panel to remain connected so it can display the mirror.
First-time setup
Complete these steps in order.
1. Set the laptop panel as the primary display
Before the first DeadZone launch, open Settings → System → Display, select the built-in laptop panel, and enable Make this my main display. This matters because the first-run setup measures the current primary display as the physical panel.
2. Mark the damaged area
1. Double-click DeadZone.exe.
2. A full-screen grid appears on the primary display, with a cyan vertical line.
3. Drag the line to the boundary between the damaged strip and the working part of the panel.
4. Use the Left Arrow and Right Arrow keys for one-pixel adjustments. Hold Shift while pressing an arrow key to move by 10 pixels.
5. Press Enter to save the position. Press Esc to cancel.
The setup screen shows the hidden width, its percentage of the panel, and the required virtual resolution. The virtual width is calculated as:
virtual width = physical panel width - hidden strip width
virtual height = physical panel height
The current code restricts the cut to at least 16 pixels and leaves at least 400 pixels of usable width.
3. Configure the virtual monitor to the exact resolution
Use the resolution displayed by DeadZone. For example, if the setup window reports 1344 x 1080, configure the VDD virtual monitor to 1344 x 1080.
1. Open Virtual Driver Control.
2. Open its display/resolution settings.
3. Add the exact width and height reported by DeadZone as a supported virtual display mode.
4. Apply/save the VDD configuration.
5. Open Settings → System → Display in Windows and select the virtual monitor.
6. Set its display resolution to the exact DeadZone resolution.
The virtual monitor's width and height must match DeadZone's calculated values exactly. If they do not match, DeadZone will remain in its waiting state rather than start mirroring.
4. Make the virtual monitor the main display
1. In Settings → System → Display, select the virtual monitor.
2. Enable Make this my main display.
3. Under Multiple displays, choose Extend these displays if Windows asks how to use the screens.
4. Keep the physical laptop panel enabled and connected as an extended display. Do not duplicate the displays and do not disconnect the laptop panel.
5. Arrange the monitor rectangles in Display Settings as needed for pointer movement. The virtual monitor must remain the primary display.
5. Start mirroring
1. Launch DeadZone.exe if it is not already running.
2. Wait for the tray icon tooltip to report mirroring.
3. The virtual desktop should now appear on the working part of the laptop panel, with the damaged strip covered.
When setup is correct, the Windows desktop, taskbar and normal application windows are rendered by Windows on the smaller virtual monitor. The original laptop panel is used as the mirror surface.
Using DeadZone
DeadZone runs in the Windows notification area (system tray), near the clock. Right-click the DeadZone tray icon to access these commands:
Menu item	Purpose
Re-adjust line...	Reopen the setup grid and change the width of the hidden strip.
Draw mouse cursor	Toggle DeadZone's cursor drawing. Turn this off if you see two cursors.
Start with Windows	Add or remove DeadZone from the current user's Windows startup registry key.
Exit	Stop mirroring, release the mouse, and close DeadZone.


The emergency hotkey is:
Ctrl + Alt + Shift + X
Use it to exit DeadZone and release its mouse clipping while you are signed in to the normal Windows desktop.
Full-screen applications
Because the virtual monitor is the primary Windows display, applications that use the normal Windows display path should see the smaller virtual resolution as the desktop display. Use the application's regular full-screen button on the virtual display rather than the old window-resizing workaround.

DeadZone also checks every 300 ms for visible top-level application windows whose centre is on the physical laptop panel. It attempts to move those windows to the virtual monitor. This is a best-effort correction for applications that reopen on the wrong monitor; it is not a driver-level guarantee and may not work with every protected, elevated, exclusive-full-screen or unusual application window.
Lock-screen helper service
The optional service is intended to keep the mirror available on Windows' secure desktops, including the lock/sign-in screen and UAC prompts. It uses a Windows service named DeadZoneSvc to launch a helper process in the active console session. The helper watches the active input desktop and attempts to mirror the virtual monitor when Windows switches away from the normal Default desktop.
[!WARNING]
The service runs with SYSTEM-level privileges. Review DeadZone.cs, install_service.bat and uninstall_service.bat before installing it. The secure-desktop functionality is experimental and has not been confirmed on all Windows versions, graphics drivers or sign-in configurations. Do not rely on it as your only way to access the computer.

Install the service
First confirm that ordinary desktop mirroring works. Then:
1. Keep the project in a permanent folder such as C:\DeadZone.
2. Confirm that DeadZone.exe is in the same folder as install_service.bat.
3. Double-click install_service.bat.
4. Approve the Windows administrator prompt. The script attempts to elevate itself if it is not already running as administrator.
5. Confirm that the script reports completion.
6. Press Win + L to test the lock screen.
The service is configured for automatic startup. Do not move, rename or delete DeadZone.exe after installing the service. The service's executable path points to that file.
Remove the service
1. Sign in to Windows.
2. Run uninstall_service.bat.
3. Approve the administrator prompt.
4. Confirm that the DeadZoneSvc service is stopped and removed.
If the lock screen remains black, do not assume the service is working. Sign back in, run the uninstaller, and use the troubleshooting steps below. Secure-desktop capture can behave differently across Windows builds and graphics drivers.
Performance and limitations
Performance design
- Desktop frames are captured using DXGI Desktop Duplication. The capture call waits for a frame change with a timeout instead of continuously taking screenshots in a tight loop.
- Frames are copied with Direct3D 11 rather than being processed as ordinary CPU bitmaps.
- The window-placement check runs every 300 ms; the main display/status check runs every second.
- The presenter is configured for a 60 Hz swap chain.
This design is intended to avoid the high-frequency fullscreen-window resizing loop used by earlier prototypes. Actual CPU/GPU usage, latency, temperature and battery impact depend on the laptop, GPU, panel refresh rate and driver. No performance benchmark is currently available, so low overhead is a design goal, not a measured guarantee.
Current limitations
- Not a physical panel repair: DeadZone cannot restore damaged LCD pixels or change the laptop panel's native hardware resolution.
- Requires a virtual display driver: Windows must have a working VDD virtual monitor with the exact calculated resolution.
- One-panel setup: the current code is designed around one physical laptop panel and one virtual monitor. Extra displays, or other displays sharing the same dimensions, may confuse its dimension-based display detection.
- Primary display required: the virtual monitor must be the Windows primary display for normal desktop use.
- Physical panel must stay connected: DeadZone needs it as the mirror target. Do not disable or disconnect it in Display Settings.
- Window relocation is best-effort: unusual windows, protected processes, elevated applications and exclusive-full-screen software may not be moved successfully.
- Secure desktop support is experimental: lock screen, sign-in and UAC mirroring depends on the service/helper and Windows desktop behavior. It is not guaranteed.
- Boot and firmware screens are not covered: the Windows program and driver cannot mirror the BIOS/UEFI setup screen or early boot screens before Windows and its services start.
- Refresh rate: the current mirror swap chain is configured at 60 Hz. Other refresh rates have not been validated.
- No multi-monitor workflow guarantee: the current display matching logic identifies screens by width and height, not by a unique hardware display identifier.
Troubleshooting
DeadZone says waiting for virtual display ...
- Confirm that the Virtual Display Driver is installed and enabled.
- Confirm that a virtual monitor exists in Windows Display Settings.
- Confirm that its resolution exactly matches the width and height shown by DeadZone.
- Confirm that the laptop panel remains connected and has its original resolution.
- Avoid using another display with the same dimensions while troubleshooting.
DeadZone says make the virtual display your MAIN display
Open Settings → System → Display, select the virtual monitor, and enable Make this my main display. The physical panel must remain connected but must not be the primary display.
The Virtual Driver Control app is missing
Installing the driver with WinGet does not guarantee that the separate control application is installed as a Start-menu app. Download the control application from the Virtual Driver Control releases, extract it, and run it from the extracted folder. See the Virtual Display Driver project for its current installation instructions.
DeadZone.exe is not created
- Confirm that the source file is named DeadZone.cs, not DeadZone(2).cs.
- Confirm that the build script is named build.bat.
- Open Command Prompt in the project folder and run build.bat again.
- Check whether csc.exe exists in one of the .NET Framework paths listed under Requirements.
- Read the compiler error printed above Build failed..
The laptop panel goes black or the display arrangement becomes unusable
- While signed in, try Ctrl + Alt + Shift + X to close DeadZone and release the mouse.
- If Windows is still visible on another monitor, use Display Settings to make the laptop panel the main display and disable/remove the virtual display.
- If a driver-related black screen or display-priority problem prevents normal access, use Windows Safe Mode to remove or disable the Virtual Display Driver. The VDD project also recommends Safe Mode recovery for driver/display problems.
- Keep another display or a Windows recovery route available before experimenting with display drivers.
The emergency hotkey is handled by the normal DeadZone process; it may not be available on the secure lock/sign-in desktop.
Two mouse cursors appear
Right-click the DeadZone tray icon and clear Draw mouse cursor. Some virtual-display configurations already provide a hardware cursor, so the extra cursor overlay may not be needed.
Applications still open on the laptop panel
- Wait briefly for the 300 ms window-placement check.
- Confirm that the virtual monitor is the primary display.
- Try restoring and reopening the application after changing the primary display.
- Some applications explicitly select a monitor or use exclusive-full-screen modes and may not be relocatable by this user-mode window check.
The lock screen is still black
- First verify that normal desktop mirroring works.
- Confirm that DeadZoneSvc is installed and running in Windows Services (services.msc).
- Sign back in and review the service installation output.
- Run uninstall_service.bat to remove the experimental service if it does not work reliably.
DeadZone causes lag or flicker
The current design does not continuously resize full-screen application windows. If lag occurs, check GPU driver health, confirm that the virtual display is running at the expected resolution, and test without other screen-capture/overlay software. The 300 ms window check can still have a cost on systems with many windows; performance has not yet been benchmarked.
Updating DeadZone
If you installed the optional service:
1. Run uninstall_service.bat as administrator.
2. Exit DeadZone from the tray or press Ctrl + Alt + Shift + X.
3. Replace the source/scripts with the new repository versions, keeping the required file names.
4. Run build.bat again.
5. Launch the newly built DeadZone.exe and confirm that desktop mirroring works.
6. Run install_service.bat again only if you still want the lock-screen helper.
Your normal user configuration is stored separately, so rebuilding the executable should not normally reset the saved cut position.
Uninstalling
1. Run uninstall_service.bat as administrator if the service was installed.
2. Exit DeadZone using the tray menu or Ctrl + Alt + Shift + X.
3. Open Settings → System → Display and set the laptop's physical panel as the main display.
4. In Virtual Driver Control, remove/disable the virtual monitor and uninstall the Virtual Display Driver if you no longer need it. Follow the driver's current uninstall instructions.
5. In DeadZone's tray menu, turn off Start with Windows if it is enabled.
6. Delete the DeadZone project folder after the service has been removed.
7. To remove saved settings as well, delete these folders if they exist:
%APPDATA%\DeadZone
%PROGRAMDATA%\DeadZone
How configuration is stored
DeadZone stores the cut width and original panel dimensions as comma-separated values:
cutPixels,panelWidth,panelHeight
The configuration is written to two locations:
- Current user configuration: %APPDATA%\DeadZone\config.txt
- Shared configuration for the SYSTEM helper: %PROGRAMDATA%\DeadZone\config.txt
The current-user startup option is stored in the Windows registry at:
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
The service name is DeadZoneSvc.
Credits and related projects
DeadZone depends on the following separate project:
- Virtual Display Driver — VirtualDrivers
- Virtual Driver Control — VirtualDrivers
Please review their documentation, licenses and release notes before installing or redistributing their driver or control software. DeadZone's source code and batch files should be accompanied by an explicit project license before public redistribution; do not assume that a dependency's license automatically applies to DeadZone.
Project status: experimental / testing required on real Windows hardware.
