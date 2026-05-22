# Feature Vault Mobile

This is the phone/tablet version of Undertale Mod Menu.

It is a browser/PWA save editor, not a Windows EXE. Phones cannot run WinForms EXEs or patch a PC `data.win`, so Live Hook installation still has to be done with the Windows app. The mobile version can edit the same save/config files and export them back out.

## Files

```text
index.html
styles.css
app.js
manifest.webmanifest
sw.js
mobile-icon.svg
```

## Use On Mobile

1. Open `index.html` in a phone browser, or host the folder on GitHub Pages.
2. Tap **Import**.
3. Pick `file0`, `undertale.ini`, and optionally `codex_live.ini`.
4. Change values and toggles.
5. Tap **Save Files**.
6. Move the downloaded files back into Undertale's save folder or use them with the PC app.

## Mobile Features

- LV, HP, EXP, gold, DMG, kills
- FUN slider and random FUN
- Pacifist, Neutral, Genocide, Custom routes
- Route flag slider
- Fresh Start, Omega Ready, Max All, Random Run
- Room teleport values
- Plot, time, and raw line editor
- WASD Move setting for `codex_live.ini`
- Mirror `file9`
- No Caps / vanilla caps toggle
- Offline-ready PWA cache when served from HTTPS or localhost

## Platform Limit

Mobile browsers can edit and download files. They cannot inject into Undertale, watch a running Windows game, or patch `data.win`. Use the Windows EXE for Live Hook install.
