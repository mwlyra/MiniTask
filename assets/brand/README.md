# MiniTask identity

The red replay mark pairs a circular arrow with a play triangle. Warm red echoes the recorder button; ivory keeps the symbol clear on both toolbar themes. The mark works without the app name in the title bar, taskbar and system tray.

- `minitask.svg`: original editable vector; transparent outside the red tile.
- `minitask-1024.png`: large transparent PNG for branding and listings.
- `minitask-256.png`: compact PNG for documentation and previews.
- `../../src/MiniTask.Desktop/MiniTask.ico`: Windows icon with 16, 20, 24, 32, 40, 48, 64, 128 and 256 px frames.

Keep the mark square, preserve its colors and surrounding transparency, and avoid stretching or adding text inside it. Use “MiniTask” beside it when the full name is needed.

To regenerate the PNG and ICO files from the SVG on Windows:

```powershell
pwsh -NoProfile -STA -File tools/Build-Icon.ps1
```

The checked-in exports are ready to use; rebuilding the application does not require regenerating them. The artwork uses original vector paths and no external images, fonts or packages.
