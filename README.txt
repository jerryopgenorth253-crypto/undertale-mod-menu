UT Save Studio Pro Max

This is a local Undertale save editor for FUN, LV, EXP, kills, gold, plot, and the murder-route override flag.
The EXE also includes Inventory Forge for all regular item IDs plus experimental raw attack-token IDs for modded builds.
The Chaos Console adds player randomizers for rooms, stats, route state, battle flags, secret FUN values, inventory, and raw file0 lines.
Feature Vault 100K+ currently loads 100,219 searchable one-click tools for power presets, LV ladders, FUN values, route control, room warps, inventory kits, battle flags, world state, phone/menu state, chaos tools, and generated 100K feature matrices.
GameJolt Mod Hub loads the Undertale tag from GameJolt, opens the selected project page, and installs a downloaded ZIP/folder into a managed mod library. Standalone fangames are copied as their own folder. data.win-style mods are layered onto a fresh copy of your Undertale game folder so the original game folder is not overwritten.
LV, HP, DMG, EXP, and gold controls are uncapped up to 999,999,999. The EXE has an Uncap Power button for setting all five instantly.
The main window now shows the big number boxes for LEVEL, HP, EXP, GOLD, and DMG. Other features are exposed as large buttons, on/off toggle buttons, or the searchable/paged Feature Vault.
Latest visual pass: Pro Max header, richer stat cards, stronger hover feedback, paged 100K Feature Vault, and cleaner control-center layout.
Watch Game toggle: waits for an Undertale process/window, writes the edited save once with a backup, then refreshes the save file while the game is running. This is not process injection; if Undertale already cached old values, reload/restart the save screen.
Live Hook V2: this is the safe replacement for raw process injection. Click Install Live Hook, choose the game's data.win, and the app patches a tiny UndertaleModTool hook into the game so Undertale reads %LOCALAPPDATA%\UNDERTALE\codex_live.ini while it is running. Write Save updates that live config, and Watch Game keeps refreshing it. The hook forces DMG during live polling, battle startup, battle stat reset, and attack calculation. A data.win.codex-backup-yyyyMMdd-HHmmss backup is created beside the chosen data.win before patching.

Run the native EXE:
1. Double-click UndertaleSaveStudioPro.exe

Run the browser version:
1. Double-click start-editor.bat
2. Open http://127.0.0.1:17380

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
- DMG is based on file0 attack power lines 5 and 6, and Live Hook V2 forces battle attack power when installed.
- EXP is file0 line 10.
- Kills is file0 line 12.
- The murder-route override is global flag 26, which is file0 line 57.

Chaos notes:
- rooms.txt beside the EXE gives the room randomizer real room names.
- Boss attack tokens are raw IDs for modded builds. Vanilla Undertale does not turn inventory slots into attacks by itself.
- Raw Line Lab can edit any line or flag. Use backups if a chaos value makes the game unhappy.

GameJolt notes:
- Click GameJolt Mod Hub, pick a project, then Open Selected Page to download it from GameJolt.
- The hub does not bypass GameJolt download pages or account prompts.
- Installed mods are stored in:
%USERPROFILE%\Downloads\UndertaleGameJoltMods
