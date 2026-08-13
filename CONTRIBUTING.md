# Contributing

Thanks for helping improve Dell G15 keyboard backlight compatibility.

## Compatibility reports

Run the following in PowerShell and attach the output to a GitHub issue:

```powershell
.\scripts\Diagnose.ps1 -Json
```

Include the exact laptop model, CPU configuration, keyboard type, BIOS version, and whether AWCC is installed. Remove personal paths or identifiers before posting if necessary.

## Safety boundary

Do not add a model based only on its marketing name. A new device mapping should include verified USB VID/PID, AlienFX API generation, HID report length, light IDs, and real-hardware tests for colour, bright, dim, and off states.

Pull requests should keep installation per-user, avoid force-deleting vendor drivers or registry data, and preserve the explicit confirmation boundary for AWCC removal.

## Local checks

```powershell
.\scripts\Build.ps1
.\scripts\Diagnose.ps1 -Json
.\scripts\Package.ps1 -Version dev
```

Please describe the tested hardware and results in the pull request.
