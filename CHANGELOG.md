# Changelog

All notable changes to Broken Screen Resizer. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Türkçe özet için [README](README.md#sürüm-geçmişi).

> **Version numbers were reorganized with v1.0.** Earlier releases were renumbered into the 0.x range; their previous numbers are shown in brackets.

## [1.0] - 2026-10-18

First stable release.

### Added
- **Installer** (`BrokenScreenResizer-1.0-Setup.exe`): installs the app per user without administrator rights. If the app is already installed, it updates it in place and keeps the settings. It closes any running copy before replacing files, adds a Start menu entry and appears with its version in Windows "Apps". Uninstalling from Windows also reverts every system change the app made.
- **Version-aware startup**: opening a newer version closes a running older version (including renamed or portable copies) and takes its place instead of just showing the old window. The "Start with Windows" entry is moved to the newest exe.
- Version information in the exe (product name, version, copyright) and the version number in the window title and tray tooltip.
- Release files are built by GitHub Actions, published with `SHA256SUMS.txt` and signed build provenance attestations (`gh attestation verify`).
- Portable ZIP with the app, `Uninstall.bat`, the driver package (`driver\`), the license and a short readme.
- READMEs in Turkish, English, Russian and Simplified Chinese; security policy; issue templates.

### Changed
- No default cut at first start: the monitor starts at full size and you mark the broken area yourself.
- The red on-screen preview appears only while you adjust and disappears a few seconds later; the app never paints the screen at startup.
- **Apply** is disabled until a broken area is marked.
- The uninstaller no longer deletes itself with a hidden command (a pattern antivirus software flags); it opens the program folder instead.
- The uninstaller no longer removes the developer's driver-signing certificate from the current user's personal store.

### Fixed
- The big resolution label in the monitor drawing no longer overlaps the side handles in narrow windows.

## [0.7] - 2026-10-18 *(previously v1.6)*

### Removed
- Driver mode. Software mode and virtual monitor remain; crops left by older versions can still be reverted.

### Added
- Complete uninstall (`--uninstall`, `Uninstall.bat`, in-app link, tray menu).
- Responsive, resizable window for small screens.

### Fixed
- The per-edge values, slider and preview drawing now stay in sync while dragging.
- No spurious horizontal scroll bar in narrow windows.

## [0.6] - 2026-10-18 *(previously v1.5)*

### Changed
- New name: **Broken Screen Resizer** (formerly *Ekran Kurtar*). Settings, the startup entry and the old driver are migrated automatically.

### Added
- Multiple broken edges: all four edges can be cut independently, so the working area can be any rectangle.
- Drag edges, corners or the whole area in the preview.

## [0.5] - 2026-10-18 *(previously v1.4)*

### Fixed
- Stutter in virtual monitor mode: mirroring and the mouse lock run on separate threads.
- Windows stuck behind the physical monitor (visible but not clickable) are moved to the virtual monitor.
- Opening the app again brings its window into view; `--reset` closes the running copy cleanly.

## [0.4] - 2026-10-18 *(previously v1.3)*

### Added
- The app's own virtual display driver (IddCx), embedded in the exe and installed on first use. `--uninstall-driver` removes it.

## [0.3] - 2026-10-18 *(previously v1.2)*

### Added
- Virtual monitor method (experimental, with the external Virtual Display Driver).
- NVIDIA Warp fallback for driver mode.

### Changed
- Software mode became the default.

## [0.2] - 2026-10-18 *(previously v1.1)*

### Added
- New interface with visual setup and live on-screen preview.
- "How it works" panel with animations.
- Four languages: Türkçe, English, 中文, Русский.

## [0.1] - 2026-10-18 *(previously v0.4)*

First release, named *Ekran Kurtar*: driver mode and software mode, with a 15-second confirmation for every change.

[1.0]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v1.0
[0.7]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.7
[0.6]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.6
[0.5]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.5
[0.4]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.4
[0.3]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.3
[0.2]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.2
[0.1]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.1
