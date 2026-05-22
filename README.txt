UT Save Studio Pro Max

This is a local Undertale save editor for FUN, LV, EXP, kills, gold, plot, and the murder-route override flag.
The EXE also includes Inventory Forge for all regular item IDs plus experimental raw attack-token IDs for modded builds.
The visible Chaos tab/buttons have been removed from the main player UI.
Clean Feature Tools now uses curated, named sections for player presets, route setup, room teleports, inventory loadouts, FUN events, story flags, world settings, and phone/menu state.
GameJolt Mod Hub loads the Undertale tag from GameJolt, opens the selected project page, and installs a downloaded ZIP/folder into a managed mod library. Standalone fangames are copied as their own folder. data.win-style mods are layered onto a fresh copy of your Undertale game folder so the original game folder is not overwritten.
GitHub Auto-Update checks update.ini every time the EXE starts. Point it at your GitHub release asset and the app will download a changed EXE, close, replace itself, and restart. The previous EXE is kept as UndertaleSaveStudioPro.exe.previous.
LV, HP, DMG, EXP, and gold controls are uncapped up to 999,999,999. The EXE has an Uncap Power button for setting all five instantly.
The main window now shows the big number boxes for LEVEL, HP, EXP, GOLD, and DMG. Other features are exposed as large buttons, on/off toggle buttons, or the searchable/paged Feature Tools window.
WASD Move toggle: writes a controls setting to codex_live.ini. Install or reinstall Live Hook V3, then restart Undertale from the patched folder to use W/A/S/D for normal rooms and battle soul movement.
Latest visual pass: professional Feature Tools cards, curated category tabs, no generated 100K placeholder lists, and cleaner control-center layout.
Watch Game toggle: waits for an Undertale process/window, writes the edited save once with a backup, then refreshes the save file while the game is running. This is not process injection; if Undertale already cached old values, reload/restart the save screen.
Live Hook V3: this is the safe replacement for raw process injection. Click Install Live Hook, choose the game's data.win, and the app patches a tiny UndertaleModTool hook into the game so Undertale reads %LOCALAPPDATA%\UNDERTALE\codex_live.ini while it is running. Write Save updates that live config, Watch Game keeps refreshing it, and WASD Move maps W/A/S/D to arrow movement. The hook forces DMG during live polling, battle startup, battle stat reset, and attack calculation. A data.win.codex-backup-yyyyMMdd-HHmmss backup is created beside the chosen data.win before patching.

Mobile edition: open index.html on a phone or host the repo as a web page. The mobile build is touch-first and can import file0, undertale.ini, and codex_live.ini, edit LV/HP/EXP/gold/DMG/FUN/routes/rooms/flags/WASD, and download the changed files. Phones cannot run the Windows EXE or patch a PC data.win, so Live Hook install remains Windows-only.

Run the native EXE:
1. Double-click UndertaleSaveStudioPro.exe

Run the browser version:
1. Double-click start-editor.bat
2. Open http://127.0.0.1:17380

Run the mobile web version:
1. Open index.html from a phone browser, or host the folder on GitHub Pages.
2. Tap Import, choose your save/config files, edit, then tap Save Files.

The app reads and writes:
%LOCALAPPDATA%\UNDERTALE\undertale.ini
%LOCALAPPDATA%\UNDERTALE\file0
%LOCALAPPDATA%\UNDERTALE\file9
%LOCALAPPDATA%\UNDERTALE\codex_live.ini

Backups are created in:
undertale-fun-route-editor\backups

Route notes:
- FUN is stored in undertale.ini.
- LV is file0 line 2.
- DMG is based on file0 attack power lines 5 and 6, and Live Hook V3 forces battle attack power when installed.
- EXP is file0 line 10.
- Kills is file0 line 12.
- The murder-route override is global flag 26, which is file0 line 57.

Feature notes:
- Boss attack tokens are raw IDs for modded builds. Vanilla Undertale does not turn inventory slots into attacks by itself.
- Feature Tools edits the save in memory first. Press Write Save only after checking the values.

GameJolt notes:
- Click GameJolt Mod Hub, pick a project, then Open Selected Page to download it from GameJolt.
- The hub does not bypass GameJolt download pages or account prompts.
- Installed mods are stored in:
%USERPROFILE%\Downloads\UndertaleGameJoltMods

GitHub update notes:
- Edit update.ini beside the EXE after you create your GitHub repository/release.
- This build is configured for jerryopgenorth253-crypto/undertale-mod-menu.
- Set Owner, Repo, and AssetName, or paste a direct latest-release EXE URL into DownloadUrl if you move repositories later.
- The updater compares SHA-256 hashes, so it only replaces the EXE when the GitHub copy is actually different.
