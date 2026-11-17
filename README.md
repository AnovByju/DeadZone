# DeadZone v3.1 — Installation and User Guide

DeadZone is a Windows desktop-mirroring utility for a laptop panel with a damaged strip on the left. It uses a **Virtual Display Driver (VDD)** monitor as the Windows primary desktop, then mirrors that virtual desktop onto the working portion of the laptop's physical LCD. The damaged strip is covered in black.

> **Important:** DeadZone is not a hardware repair and does not change the physical panel's native resolution. Windows uses two displays: the VDD virtual display as the main desktop, and the laptop LCD as the output panel. DeadZone copies the virtual desktop to the usable section of the LCD. The VDD monitor must be configured to the exact working-area resolution before mirroring can start.

## 1. What DeadZone does

- Displays a full-screen setup grid so you can drag a vertical line to the boundary between the damaged and working areas.
- Saves the cut width and the original panel dimensions in `%APPDATA%\DeadZone\config.txt`.
- Expects a VDD virtual monitor with these dimensions:

  `virtual width = physical panel width - damaged-left width`

  `virtual height = physical panel height`

- Requires the VDD monitor to be the Windows **main display** so the Start menu, taskbar and newly opened applications use the virtual desktop.
- Captures the VDD desktop and mirrors it onto the working section of the laptop LCD; the left cut-out section is filled with black.
- Clips the mouse to the virtual desktop while mirroring is active.
- Attempts to move ordinary/borderless full-screen windows that open over the physical laptop panel onto the virtual display.
- Provides a tray menu, startup option, cursor option, re-adjustment command and emergency exit hotkey.

## 2. What it does not do

- It does not make Windows report the laptop's physical LCD as a smaller, single physical monitor. Windows sees a separate virtual monitor and a physical monitor.
- It cannot reliably redirect exclusive-fullscreen games or applications that take direct control of the display. Set those applications to **Borderless Windowed** or **Windowed Fullscreen**.
- It cannot mirror the Windows lock screen or sign-in screen. Those run on a protected desktop unavailable to a normal user-mode application.
- It does not guarantee support for every GPU, graphics driver, multi-monitor layout or display-capture configuration. Test it on the target laptop before relying on it.

## 3. Requirements

- Windows 10 or Windows 11.
- The DeadZone v3.1 source package fully extracted to a writable local folder. Do not run `build.bat` from inside the ZIP archive.
- A working Virtual Display Driver (VDD) virtual monitor.
- Permission to install a display driver and change display settings.
- A compatible GPU/driver that supports the DXGI Desktop Duplication and Direct3D 11 operations used by DeadZone.
- Internet access for the first .NET SDK publish/build if the Windows .NET Framework compiler is unavailable.

VDD is a separate project and is **not bundled** with DeadZone. Official project and setup links:

- VDD project and current installation instructions: https://github.com/VirtualDrivers/Virtual-Display-Driver
- VDD releases and Virtual Driver Control (VDC): https://github.com/VirtualDrivers/Virtual-Display-Driver/releases
- VDD installation wiki: https://github.com/VirtualDrivers/Virtual-Display-Driver/wiki/Installation

## 4. Build DeadZone.exe

The ZIP contains `DeadZone.cs`, `DeadZone.csproj`, `build.bat` and `DisplayCheck.ps1`.

1. Download `DeadZone-v3.1-Windows.zip` and extract the complete `DeadZone-v3.1` folder, for example to `Downloads` or `Documents`.
2. Open the extracted folder. Confirm that `DeadZone.cs`, `DeadZone.csproj` and `build.bat` are in the same folder.
3. Double-click `build.bat`.
4. The builder first tries the .NET Framework C# compiler. If that is unavailable, it tries the .NET SDK.
5. If prompted to install the Microsoft .NET 10 SDK using WinGet, enter `Y` only if you approve the installation. Alternatively, install the SDK yourself and run `build.bat` again.
6. Wait for the message `BUILD SUCCEEDED`.
7. The resulting `DeadZone.exe` should appear in the same `DeadZone-v3.1` folder.
8. If the build fails, no new executable is produced. Send `build-csc-log.txt` or `build-dotnet-log.txt` (whichever contains the failure) when requesting help.

Do not use an older `DeadZone.exe` from a previous folder as a replacement for the newly built executable. The v3.1 ZIP is a source/build package; it is not a precompiled v3.1 executable.

## 5. Install and enable the Virtual Display Driver

DeadZone cannot create the virtual monitor by itself. Install VDD separately.

### Option A — WinGet

1. Open Windows Terminal or PowerShell.
2. Run:

   ```powershell
   winget install --id=VirtualDrivers.Virtual-Display-Driver -e
   ```

3. Follow the installer prompts.
4. Download the separate **Virtual Driver Control (VDC)** application from the official VDD Releases page linked above.
5. Extract and launch VDC. Use its **Install** control to install/enable the VDD driver, following the prompts in that version of VDC.
6. Open Device Manager and Windows Settings > System > Display to confirm that Windows detects the virtual display.

If Windows reports a missing `vcruntime140.dll`, install the current Microsoft Visual C++ Redistributable from Microsoft's official page: https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist

### Option B — Official VDD installer

Use the current installer supplied on the official VDD Releases page and follow its installation instructions. Avoid manual INF installation unless you are comfortable with Device Manager driver installation and the VDD project's manual-install guide.

## 6. Choose the usable-area width

You must save the damaged-left cut first, because DeadZone calculates the exact virtual display width from that cut.

1. Keep the physical laptop LCD as the Windows main display while doing the initial setup if that makes the desktop easier to use.
2. Run `DeadZone.exe`.
3. A full-screen grid appears on the physical laptop LCD. Drag the cyan vertical line to the boundary where the undamaged pixels begin.
4. Use the left/right arrow keys for one-pixel adjustments. Hold Shift while pressing an arrow key to move in larger increments.
5. Press Enter to save. Press Esc to cancel.
6. DeadZone saves the cut and original dimensions to:

   `%APPDATA%\DeadZone\config.txt`

7. After saving, read the exact required virtual resolution using the PowerShell command below.

   ```powershell
   $p = (Get-Content "$env:APPDATA\DeadZone\config.txt").Split(',')
   [pscustomobject]@{
       DamagedLeftPixels = [int]$p[0]
       PhysicalWidth     = [int]$p[1]
       PhysicalHeight    = [int]$p[2]
       RequiredVDDWidth  = ([int]$p[1] - [int]$p[0])
       RequiredVDDHeight = [int]$p[2]
   } | Format-List
   ```

### Example only

For a 1920 x 1080 physical panel with a 30% damaged-left strip:

- Approximate cut: 576 pixels.
- Required VDD mode: **1344 x 1080**.

Use the exact values printed by the PowerShell command, not the example values. The cut can be different from exactly 30% depending on where the working pixels begin.

## 7. Configure the VDD resolution

1. Open VDC and locate the virtual display configuration for your installed VDD version.
2. Add/enable a mode equal to the exact `RequiredVDDWidth x RequiredVDDHeight` calculated in Step 6. Keep the height equal to the physical panel height.
3. If your VDC version does not expose custom resolution controls, consult the VDD project's current instructions for editing `C:\VirtualDisplayDriver\vdd_settings.xml`. The XML format and available options can vary by VDD release; do not paste an XML fragment from an unrelated version.
4. Apply the change and restart/re-enable the virtual display or reboot if the VDD version requests it.
5. Open **Settings > System > Display**. Select the virtual display and choose the required resolution under **Display resolution**.
6. Select **Extend these displays**. Do not choose Duplicate.
7. At this stage, verify that the virtual display really runs at the required width and the full physical-panel height.

**Important:** Your earlier diagnostic showed a virtual display at **800 x 600**. That is not the expected mode for a 1920 x 1080 panel with a 30% cut. DeadZone will continue showing `waiting for virtual display 1344x1080` (or the dimensions from your saved configuration) until VDD exposes and Windows selects the exact required mode.

## 8. Set the virtual monitor as the main display

Do this only after the VDD monitor is running at the exact required resolution.

1. Open **Settings > System > Display**.
2. Click the virtual display in the monitor diagram. Use **Identify** if you are unsure which rectangle it is.
3. Expand **Multiple displays** if necessary.
4. Enable **Make this my main display** for the virtual display.
5. Keep the physical laptop LCD enabled as an extended display. Do not disconnect or disable it.
6. Leave the display arrangement in **Extend these displays** mode.

The Windows taskbar and Start menu will belong to the virtual primary monitor. Once DeadZone successfully mirrors the virtual desktop, those UI elements should appear in the working part of the laptop LCD. Before mirroring starts, the taskbar may be visible only on the small virtual monitor, which can make setup inconvenient.

## 9. Start DeadZone and verify mirroring

1. Run the newly built `DeadZone.exe`.
2. Look in the notification area beside the clock. If necessary, click the `^` hidden-icons arrow to find the DeadZone icon.
3. Hover over the icon to read its status.
4. The expected working status is:

   `mirroring - fullscreen windows redirected`

5. The damaged-left section should be black, and the virtual desktop should appear on the remaining right-hand portion of the physical LCD.
6. Test with ordinary windows first, then a browser/video player using borderless fullscreen.
7. If the cursor appears twice, right-click the DeadZone tray icon and turn off **Draw mouse cursor**.

## 10. Tray menu and keyboard controls

Right-click the DeadZone notification-area icon for these options:

- **Re-adjust line...** — choose a new damaged-left boundary. If the usable width changes, update the VDD resolution to match the new calculated size and restart DeadZone.
- **Show setup instructions...** — reopens the setup summary.
- **Draw mouse cursor** — turns DeadZone's cursor drawing on or off if the cursor appears duplicated.
- **Start with Windows** — toggles automatic startup.
- **Exit** — stops mirroring and releases the mouse restriction.

Emergency exit: press **Ctrl + Alt + Shift + X**. This stops DeadZone, hides its cover window and releases cursor clipping.

## 11. Your current 800 x 600 / taskbar issue

The diagnostic output you shared showed:

```text
DISPLAY1  Primary=False  1920 x 1080
DISPLAY6  Primary=True    800 x 600
```

This means Windows currently has a 1920 x 1080 secondary display and an 800 x 600 primary display. The output alone does not prove which display is the VDD monitor, but it is consistent with VDD being selected at 800 x 600. DeadZone needs a virtual display matching the width and height saved in `config.txt`.

To check the expected size:

1. Open PowerShell in the extracted DeadZone folder.
2. Run the configuration command in Step 6.
3. Compare `RequiredVDDWidth` and `RequiredVDDHeight` with the virtual display shown in Settings > System > Display.
4. Change the VDD mode to that exact resolution before making it the main display.
5. Run the diagnostic script from the extracted folder:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\DisplayCheck.ps1"
   ```

   The VDD display should report the exact required dimensions and `Primary=True`; the physical LCD should remain enabled at its native dimensions.

If the taskbar is difficult to reach while the virtual monitor is primary, temporarily open Display Settings with **Win + R**, enter `ms-settings:display`, and set the physical laptop LCD as the main display while you correct the VDD resolution. Once the VDD mode is correct, set VDD as the main display again and restart DeadZone.

## 12. Status messages and troubleshooting

| Tray status | Meaning / action |
|---|---|
| `waiting for virtual display WIDTHxHEIGHT` | Windows does not currently expose a monitor with the exact required dimensions. Add/select the correct VDD mode. |
| `make virtual display the main display` | The correctly sized virtual monitor exists, but Windows has not marked it as the primary display. |
| `starting...` | DeadZone is trying to initialize DXGI/D3D capture. Give it a short moment. |
| `mirror error - ...` | Capture initialization failed. Record the complete status text, check GPU drivers and confirm the VDD monitor is active. |
| `mirroring - fullscreen windows redirected` | The mirror is running. Test a normal window and a borderless-fullscreen application. |

If the display becomes confusing, use the DeadZone emergency hotkey first. You can also close DeadZone from its tray menu if available. Then open **Settings > System > Display**, select the physical laptop LCD and make it the main display. Do not uninstall or disable the physical laptop panel.

If Windows becomes unusable after a VDD driver installation or update, follow the VDD project's current recovery instructions. The VDD project documents Safe Mode removal as a recovery option for cases where display priority is scrambled or Windows boots to a black screen. Do not remove your laptop's Intel/AMD/NVIDIA graphics driver as a troubleshooting shortcut.

## 13. Known limitations and safety notes

- The utility has not been validated on every laptop or GPU model. Test cautiously.
- A virtual display driver changes the Windows display topology. Keep the VDD installer/release information available and know how to restore the physical LCD as the main display.
- Exclusive fullscreen games may still use the physical panel; select borderless/windowed fullscreen in those applications.
- The protected Windows lock/sign-in screen is not mirrored.
- Screen capture may pause or reset during secure desktop transitions, display sleep, driver reset or GPU changes.
- Do not install display-driver updates from unknown sites. Follow the laptop/GPU manufacturer's instructions.
- The VDD project advises uninstalling VDD before major GPU/chipset driver updates and reinstalling it afterward if required by its current release notes.

## 14. Uninstall / restore normal display use

1. Press **Ctrl + Alt + Shift + X** or right-click the DeadZone tray icon and select **Exit**.
2. Open **Settings > System > Display**.
3. Select the physical laptop LCD and enable **Make this my main display**.
4. Keep or disconnect the virtual display as desired. If you no longer need VDD, uninstall it through VDC or the Windows app/uninstaller provided by the VDD release.
5. To stop DeadZone from launching automatically, use the DeadZone tray menu > **Start with Windows** to turn it off before exiting.
6. Optional: delete `%APPDATA%\DeadZone\` to remove the saved cut configuration. This does not uninstall VDD.

---

DeadZone is an experimental utility provided without warranty. It is not affiliated with the Virtual Display Driver project. Use the official VDD links above for driver installation, release notes and driver-specific configuration details.
