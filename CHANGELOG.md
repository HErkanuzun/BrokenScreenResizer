# Changelog

All notable changes to Broken Screen Resizer. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Türkçe özet için [README](README.md#sürüm-geçmişi).

> **Version numbers were reorganized with v1.0.** Earlier releases were renumbered into the 0.x range; their previous numbers are shown in brackets.

## [1.6] - 2026-10-18 *(preview)*

Pre-release from the `develop` branch. The stable release is still [1.01](#101---2026-10-18).

### Added
- **Windows tweaks** (new tab): dock the taskbar to a different edge on each monitor (bottom, top, left, right, or no taskbar).
  - A drawing of the monitors: click an edge to move that monitor's taskbar there, or use the side buttons for the selected monitor.
  - The main monitor uses the Windows taskbar, moved with Windows 11's `TaskbarLocation` setting. If that Windows build doesn't support it yet, the app says so and leaves the main taskbar where it was.
  - Monitors on a different edge get the app's own taskbar: Start button, that monitor's windows with icons (click to activate or minimize, right-click menu), flashing alerts, clock and date. It reserves screen space as an AppBar and hides during full-screen apps. Windows' secondary taskbars are turned off while it runs.
  - Icon alignment (center or left) and window names on the app's taskbars.
  - The original taskbar settings are saved and restored by Revert, emergency reset (`--reset`) and uninstall. When the app closes, the Windows taskbars come back; at startup the layout is applied again.

### Changed
- The tabs are now "Broken screen", "Screen Multiplier" and "Windows tweaks".

## [1.5] - 2026-10-18 *(preview)*

Pre-release from the `develop` branch. The stable release is still [1.01](#101---2026-10-18).

### Added
- **Screen Multiplier** (new tab): splits a large monitor into up to 8 virtual screens, each a separate monitor in Windows.
  - Monitor-shaped editor: drag screens and their edges or corners, with snapping to neighbours and an 8-pixel grid. **Add screen** and **Remove screen** buttons.
  - Collisions: dragging into a neighbour first stops and shakes. Dragging the same way again pushes the neighbour and shrinks it to at least 640×360.
  - Nine templates (2 side by side, 2 stacked, 3 columns, 1 large + 2, 2 × 2, 1 large + 3, 3 + 2, 3 × 2, 4 × 2). Templates that don't fit the monitor are disabled.
  - Recommendations from pixel density: the monitor's physical size is read from its EDID (or entered by hand) and three tiers are suggested: *no quality loss*, *recommended* and *maximum*. For a 55″ 4K TV this gives 2 / 4 / 6.
  - A note with the recommended maximum, and a distribution bar and table showing each screen's resolution, share of the monitor, size in inches and quality.
  - The setting is restored at startup like the other modes. Revert, emergency reset and uninstall undo it.
- The window title shows "(preview)" on preview builds.

### Changed
- **Virtual display driver 2.0** (protocol 2): up to 8 monitors (was 4), and several monitors of the same size can be open together. Older app versions keep working with the new driver. The app offers to update an older installed driver when Screen Multiplier needs it.
- The virtual monitor mirror can now copy several virtual monitors at once.
- CI publishes preview versions as GitHub pre-releases that are not marked "latest". It also builds the `develop` branch.

### Fixed
- Release builds made on GitHub embedded the driver's `.inf` with Windows line endings, so it no longer matched the signed catalog. Installing the virtual display driver from those files could then fail with a signature error. The signed driver package is now stored byte-for-byte (`.gitattributes`), and CI checks it against the catalog before building.
- A few driver messages that appeared in English in the Chinese and Russian interface are now translated.

## [1.01] - 2026-10-18

### Changed
- **Centered layout:** in wide and fullscreen windows the main column and the "How it works" panel are centered together instead of leaving empty space on the right.
- **Clear locked state:** while a setting is applied (or the monitor was cropped earlier), the controls that can't be changed until Revert are shown faded ("misty"): the monitor drawing, edge picker, slider, −/+ buttons, preview switch and method cards. The monitor picker is faded too while a setting is active. The monitor drawing shows a "Locked · press Revert to change" label, the hint below it explains what to do, and Revert becomes the highlighted button.

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

[1.01]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v1.01
[1.0]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v1.0
[0.7]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.7
[0.6]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.6
[0.5]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.5
[0.4]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.4
[0.3]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.3
[0.2]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.2
[0.1]: https://github.com/HErkanuzun/BrokenScreenResizer/releases/tag/v0.1
