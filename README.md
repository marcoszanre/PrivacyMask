PrivacyMask
===========

Small WinForms app that creates a transparent overlay window which can be arranged by the user and that protects the screen capture (WDA_MONITOR / WDA_EXCLUDEFROMCAPTURE).

Requirements
- Windows 10 (2004+) for WDA_EXCLUDEFROMCAPTURE
- .NET 8 SDK

Quick run (PowerShell)

Run the app from source:

```powershell
# From project root
dotnet run
```

Publish a single-file release (x64):

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:PublishTrimmed=true
```

Files added by the helper scripts
- `run.ps1` — runs the project and ensures no stale process is running
- `publish.ps1` — publishes single-file release to `bin/Release/` directory

Usage
- Ctrl+Alt+1 → toggle black-in-capture mode
- Ctrl+Alt+2 → toggle exclude-from-capture (Win10 2004+)
- Ctrl+Alt+Q → exit

Notes
- When the overlay is click-through, the app keeps itself topmost and attempts to retain focus so hotkeys remain active. The overlay is not a security mechanism — someone can still photograph the screen.
