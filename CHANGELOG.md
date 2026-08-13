# Changelog

## 0.1.1

- Reopen the AlienFX HID controller after Windows resumes from sleep or hibernation, then restore the saved colour and brightness with a delayed retry.

## 0.1.0

- Added direct persistent AlienFX APIv4 HID control for `VID_187C&PID_0550`.
- Added four-zone colour, full brightness, configurable dim brightness, and off states.
- Restored the `Fn+F5` brightness cycle without an AWCC runtime dependency.
- Added hidden per-user startup, settings migration, diagnosis, conservative AWCC removal, and uninstall scripts.
- Added English and Simplified Chinese documentation and reproducible packaging.
