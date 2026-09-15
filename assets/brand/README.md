# MiniTask identity

The classic cassette recorder combines a beveled gray shell, a muted blue label, two tape reels and a red recording button. Its dark outlines and small highlights use the toolbar's existing palette and old-school Windows utility style. The silhouette sits directly on a transparent background and works without the app name in the title bar, taskbar and system tray.

- `minitask.svg`: original editable vector; transparent around the cassette and recording button.
- `minitask-1024.png`: large transparent PNG for branding and listings.
- `minitask-256.png`: compact PNG for documentation and previews.
- `../../src/MiniTask.Desktop/MiniTask.ico`: Windows icon with 16, 20, 24, 32, 40, 48, 64, 128 and 256 px frames.

Keep the canvas square, preserve its colors and surrounding transparency, and avoid stretching or adding text inside it. Use “MiniTask” beside it when the full name is needed.

To regenerate the PNG and ICO files from the SVG on Windows:

```powershell
pwsh -NoProfile -STA -File tools/Build-Icon.ps1
```

The checked-in exports are ready to use; rebuilding the application does not require regenerating them. The artwork uses original vector paths and no external images, fonts or packages.
