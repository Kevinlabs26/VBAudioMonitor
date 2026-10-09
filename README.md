# VB Audio Monitor

![VB Audio Monitor icon](assets/app.png)

Hear audio sent to a VB-CABLE virtual playback device through your physical headphones or speakers.

VB Audio Monitor is a Windows desktop utility with a shared-mode WASAPI audio relay. It captures the selected virtual playback endpoint, selects a stereo channel pair, and forwards the audio to a physical output device. It does not access your microphone, record audio to files, or change the Windows default audio device.

## Requirements

- Windows 10 or Windows 11, x64.
- An installed and enabled VB-CABLE driver, or a compatible virtual playback endpoint.
- Physical headphones or speakers.

The packaged executable is self-contained: you do not need to install the .NET runtime. The virtual audio driver is not included.

## Install and update

Download `VBAudioMonitor-win-Setup.exe` from [GitHub Releases](https://github.com/Kevinlabs26/VBAudioMonitor/releases) and double-click it. It installs for your Windows account without requiring administrator access, creates desktop/Start menu shortcuts, and adds an uninstall entry in Windows Settings. The VB-CABLE driver must still be installed separately.

The installed edition checks the public `Kevinlabs26/VBAudioMonitor` GitHub Releases on startup and every six hours while running. Stable updates download in the background and are verified by Velopack. A prepared update is applied on the next launch. **Software updates** in the footer or **Check for updates** in the tray opens update status; **Install now** saves settings, stops monitoring, and restarts the app. Downloads do not stop monitoring. Network errors allow retry and do not affect audio routing.

A second instance does not apply updates while the first is running. Closing the window hides it to the tray; exit from the app or tray menu before relaunching to apply an update. A restart opens the app without automatically starting audio monitoring.

Installed settings are stored in `%AppData%\VBAudioMonitor\settings.json`, outside the versioned application directory, so upgrades and uninstalling the app preserve them. To migrate from an older portable copy, exit both copies and copy its `settings.json` into that directory before launching the installed app. Back up an existing destination file first.

## Portable download

Download `VBAudioMonitor-win-x64-portable.zip` from GitHub Releases, extract the entire archive into a writable folder, and double-click `VBAudioMonitor.exe`. This edition does not install shortcuts or enable automatic updates; use the setup edition for those features.

The public ZIP includes the English README, icon assets and dependency license notices. It excludes personal settings, saved presets, device IDs and verification output. On first use, select your own devices and language. A `settings.json` file is created beside the executable when you change settings.

The executable currently has no Authenticode signature. The SHA-256 file checks download integrity; it is not a code-signing certificate.

## Quick start

1. Exit any older instance from its notification-area menu. Closing its window only hides it to the tray.
2. Install the setup edition and open its shortcut, or open `VBAudioMonitor.exe` from the extracted portable folder. Local builds place the executable in `release-installable`.
3. Use the language selector at the bottom right to choose **English**, **Français**, or **简体中文**. Chinese is the default, including when loading older settings.
4. In your browser or playback app, choose the virtual cable as the speaker/output device.
5. In **Audio source · Virtual cable**, select the **same playback device name**.
6. In **Monitor output · Headphones or speakers**, choose your physical headphones or speakers.
7. Click **Start monitoring**, play audio, and adjust **Monitor volume**.

```text
Browser/player → CABLE Input or CABLE In 16ch → VB Audio Monitor → Headphones/speakers
```

`CABLE Input` and `CABLE In 16ch` are different endpoints. Match the exact name rather than choosing any device containing “CABLE”. Leave the browser microphone set to your real microphone. This app lists playback endpoints; the recording endpoint called `CABLE Output` is not the source used here.

## Features

- **Live language switching:** Chinese, English and French cover the main window, status messages, tooltips, tray menu, preset dialogs and diagnostics. Switching languages preserves the route and does not stop monitoring. Your choice is saved.
- **Separate monitor volume:** changes affect this app's physical output, leaving the original virtual cable audio unchanged.
- **Mute memory per output:** headphones and speakers remember their volume and the level used before muting. A new output starts at 50%.
- **Smooth gain changes:** approximately 200 ms of fade-in on start and 30 ms of smoothing when adjusting volume. Output buffers can cause a short delay before mute is audible.
- **Stereo metering:** left/right levels, peak hold and a clipping indicator. Advanced settings select the source channel pair; mono is sent to both ears.
- **Buffer settings:** 60 ms for lower buffering, 100 ms for a balanced default, or 200 ms for greater tolerance of interruptions. Stop monitoring before changing channels or the buffer.
- **Device recovery:** an interrupted connection waits for its original devices and resumes when they return. **Cancel recovery** stops waiting. Audio is not automatically redirected to another output.
- **Named presets:** save devices, channels, buffer, volume and pre-mute volume. Select a preset from the window or tray. Applying it stops monitoring; confirm the output and click **Start monitoring**. Saving the same name updates it; deletion requires confirmation.
- **Read-only diagnostics:** inspect device availability, Windows volume/mute, channel format, current levels and the last error. Reports are selectable and scrollable. The check does not play test sounds or change device settings.
- **Tray operation and themes:** closing the window keeps monitoring in the background. Double-click the tray icon to restore it. **Exit app** stops monitoring and exits. Light and dark themes are available.

Device names, user-defined preset names and messages supplied by Windows or audio drivers retain their original text. Standard Windows message-box buttons follow the Windows display language.

## Settings and upgrades

Settings include devices, channel pair, buffer, theme, language, per-output volume memory and presets. Installed copies use `%AppData%\VBAudioMonitor\settings.json`; portable copies use `settings.json` beside the executable. Changes are saved automatically and before hiding or exiting. Keep the settings folder writable; a status message appears if saving fails.

The build script copies earlier portable settings into the local build folder only when it has no settings file. Public installers, update packages and ZIP downloads exclude those settings. When upgrading a portable copy manually, exit the old app and copy its `settings.json` beside the new executable before starting. Keep the old file as a backup.

Only one normal instance can run at a time. A second instance displays a notice to check the taskbar or notification area. After opening the app, start monitoring manually.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| No sound and no meter activity | Start playback. Match the browser output and this app's source to the same CABLE device. Verify the source channels. |
| Meter moves but you hear nothing | Choose the physical output, raise monitor volume, unmute the app, and check the output's Windows volume and mute state. |
| Crackling or interruptions | Stop, open Advanced settings, choose the 200 ms buffer, then restart. |
| Waiting for devices | Reconnect the original devices. To change devices, cancel recovery, refresh, select them and start again. |
| Saved device or channels unavailable | Reconnect and refresh, or select available devices/channels manually. The saved identity is retained instead of silently substituting another device. |
| Device in use or cannot start | Check other apps' exclusive audio modes, refresh and retry. Use Check audio for details. |
| Settings could not be saved | Check write access to `%AppData%\VBAudioMonitor` for installed copies. For portable copies, use a writable app folder. |

**Queued audio** is the audio duration currently buffered by this app, not end-to-end listening latency. **Buffer resets** counts excessive queues being cleared, not packet loss. Diagnostic output levels may include other apps' audio and cannot alone prove the relay is working. A historical error may already be resolved.

## Build and verification

Development requires the .NET 8 SDK on Windows. The project uses `NAudio.Wasapi` 2.2.1 and `Velopack` 1.2.0. Audio monitoring and translations work offline; automatic updates contact GitHub.

```powershell
# Restore and compile
dotnet build VBAudioMonitor.csproj -c Release

# Hidden UI/regression checks: no audio playback or settings writes
dotnet run --project VBAudioMonitor.csproj -c Release --no-build -- --check

# Verify downloading a built package from a local feed without applying it
dotnet run --project VBAudioMonitor.csproj -c Release --no-build -- --check --update-feed .\dist\github-release\1.1.0

# Build the installer, update packages, index and portable ZIP
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1

# Build the next release (increase the version for every public update)
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Version 1.1.1
```

Checks write `verification-result.json`, device metadata and screenshots into the working directory. They cover all three languages in light/dark themes at normal/narrow window sizes, language formatting/state preservation, presets, mute memory, window resource disposal, audio-provider logic, update retries/concurrency, installed settings paths, update dialog layout and icon integrity. They do not contact the live update feed, play audio, write user preferences, or establish real hardware playback quality or layout behavior when moving between monitors with different DPI settings. The optional `--check --audio` mode exercises actual audio routes; use it only when you intend to run a live audio check.

### Translations

Translations are in `src/styles/UiText.cs`. Preserve numbered placeholders such as `{0}` and their format specifiers. Device IDs and user data are not translated. The saved `Language` is `zh-CN`, `en`, or `fr`; unsupported values fall back to Chinese.

### Icon assets

The blue waveform design is retained. `assets/app.ico` contains transparent 32-bit frames at **16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels**. The executable, window and tray use the same icon resource. `assets/app.png` is the 256 px preview.

To regenerate assets from the drawing code in `Theme.DrawMark`, run this from the repository root:

```powershell
dotnet run --project VBAudioMonitor.csproj -c Release -- --export-icon
# Rebuild/publish afterwards to embed the updated ICO.
```

Third-party notices and .NET license files are included in the release folder.

### Publish a GitHub Release

Build artifacts for version `1.1.0` are in `dist/github-release/1.1.0`. Create a stable GitHub Release in `Kevinlabs26/VBAudioMonitor`, use a matching tag such as `v1.1.0`, and upload **every file in that folder**. The setup executable is for new installs; the full `.nupkg` and `releases.win.json` are required for existing installs to update. Upload the generated asset manifest, compatibility index and checksum file as well. Publishing only the setup executable does not provide an update feed. Upload all assets before marking the release public; drafts and prereleases are not used by this stable update channel.

The build also creates `dist/VBAudioMonitor-win-x64-portable.zip` and its `.sha256` checksum, which can be attached as optional downloads. The `release-installable` folder retains your local portable settings for personal use; publish files from `dist/github-release/<version>` and the public ZIP, rather than that local folder. Generated releases, packaging tools, downloaded dependencies and checks are excluded by `.gitignore`. Local rebuilds replace that version's generated artifacts; once a version is public, increase the version instead of replacing its downloads.

The setup executable and application currently have no Authenticode signature. Checksums and package verification protect file integrity but do not identify the publisher through a Windows code-signing certificate.
