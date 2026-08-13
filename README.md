# Dell G15 5515 Backlight Control

[简体中文说明](README.zh-CN.md) · [Download releases](https://github.com/cnlove7777777-art/Dell-G15-5515-Backlight-Control/releases)

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4) ![License](https://img.shields.io/badge/license-MIT-green) ![Hardware](https://img.shields.io/badge/controller-187C%3A0550-orange)

A tiny Windows tray controller for the four-zone RGB keyboard fitted to the tested Dell G15 5515 Ryzen configuration. It restores a useful `Fn+F5` brightness cycle without installing Alienware Command Center (AWCC).

## What it does

- Controls four zones independently or sets them all to white.
- Cycles bright → configurable dim → off with `Fn+F5`.
- Writes directly to the AlienFX APIv4 HID controller, so no background PowerShell window or helper process is used during normal operation.
- Starts invisibly at user logon through a per-user scheduled task.
- Stores settings in `%LOCALAPPDATA%\G15Backlight\settings.ini`.

## Compatibility boundary

This release is intentionally narrow. It is tested with:

- Dell G15 5515, Ryzen 7 5800H configuration
- Four-zone keyboard controller `VID_187C&PID_0550`
- AlienFX APIv4 light IDs `8, 9, 10, 11`
- Windows 10/11 x64 and .NET Framework 4.x

The product name alone is **not** enough. Other G15 keyboards, including single-colour, APIv5, different zone maps, and newer models, must not be forced unless their HID protocol has been verified. Please attach the JSON output from `Diagnose.ps1 -Json` to a compatibility issue.

## Install

Download and extract a release, then double-click `Install.cmd`. The installer:

1. detects the exact supported HID controller;
2. audits AWCC conflicts;
3. copies one EXE to `%LOCALAPPDATA%\G15Backlight`;
4. migrates the older `AlienFX-Backlight\settings.ini` if present;
5. creates a hidden current-user logon task and opens settings.

If AWCC is detected, installation stops instead of allowing two programs to fight over the same controller. Run this first to see what remains:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Remove-AWCC.ps1 -Mode Audit
```

Registered MSI/Appx packages can be removed explicitly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Remove-AWCC.ps1 `
  -Mode UninstallRegistered -ConfirmAwccRemoval
```

The cleanup helper does not force-delete services, drivers, registry keys, or folders. Broken MSI registrations such as error `1612` need case-by-case repair; pretending that every half-uninstalled AWCC is identical would be a rather adventurous installer design.

## Use

- `Fn+F5`: bright → dim → off
- `Ctrl+Shift+F5`: alternative cycle shortcut
- Double-click the tray icon: open settings
- Start `G15Backlight.exe` again: ask the existing tray instance to open settings

At logon, the app applies the saved state immediately and retries after 12 seconds in case the HID stack was not ready yet.
After sleep or hibernation, it reopens the HID device on power-resume or session-unlock and restores the saved state. Recovery results are recorded in `%LOCALAPPDATA%\G15Backlight\events.log`.

## Build

No Visual Studio installation is required on a standard Windows 10/11 x64 system with .NET Framework 4.x:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
```

The output is `dist\G15Backlight.exe`. Build a distributable ZIP with `scripts\Package.ps1`.

## Uninstall

Double-click `Uninstall.cmd`, or keep the settings file with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Uninstall.ps1 -KeepSettings
```

The uninstaller removes only the exact scheduled task and `%LOCALAPPDATA%\G15Backlight`. It does not touch AWCC or the legacy settings directory.

## Credits and licence

The HID protocol implementation was derived from the MIT-licensed [alienfx-tools](https://github.com/T-Troll/alienfx-tools) project by Rik Lain. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). This project is distributed under the MIT License.

This is an independent community project and is not affiliated with or endorsed by Dell Technologies or Alienware.

## Contributing

Compatibility reports are welcome. Please run `scripts\Diagnose.ps1 -Json`, remove personal paths if desired, and include the output with the exact laptop model and keyboard type. See [CONTRIBUTING.md](CONTRIBUTING.md) before proposing a new controller or zone map.
