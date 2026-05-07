# Undertale Save Studio Pro Max

Unofficial local Windows tool for editing Undertale save values, experimenting with route state, and managing Undertale fangame/mod downloads.

## Download

Use `UndertaleSaveStudioPro.exe` from this repository, or build it yourself with `build.ps1`.

## Main Features

- Big editable number boxes for `LEVEL`, `HP`, `EXP`, `GOLD`, and `DMG`
- Uncapped values up to `999,999,999`
- FUN value, route state, kills, room, plot, and raw save-line editing
- Inventory Forge for regular Undertale item IDs and experimental raw mod-token IDs
- Chaos Console randomizers for rooms, stats, inventory, flags, and battle-style state
- Feature Vault 100K+ with searchable one-click presets
- Watch Game toggle that waits for Undertale and refreshes edited save/live config values
- Live Hook V2 installer for patched local `data.win` builds
- GameJolt Mod Hub for browsing Undertale-tagged GameJolt projects and installing downloaded ZIP/folder mods into a managed library

## Running

Double-click:

```text
UndertaleSaveStudioPro.exe
```

The app reads and writes these local files:

```text
%LOCALAPPDATA%\UNDERTALE\undertale.ini
%LOCALAPPDATA%\UNDERTALE\file0
%LOCALAPPDATA%\UNDERTALE\file9
%LOCALAPPDATA%\UNDERTALE\codex_live.ini
```

Backups are created before save writes.

## Building

On Windows, run:

```powershell
.\build.ps1
```

This uses the built-in .NET Framework C# compiler and produces:

```text
UndertaleSaveStudioPro.exe
```

## GameJolt Mod Hub

The hub loads the Undertale tag from GameJolt, opens the selected project page, and installs a downloaded ZIP/folder into:

```text
%USERPROFILE%\Downloads\UndertaleGameJoltMods
```

Standalone fangames are copied as their own folder. `data.win`-style mods are layered onto a fresh copy of your selected Undertale game folder so the original game folder is not overwritten.

The hub does not bypass GameJolt download pages, account prompts, creator permissions, or project-specific install instructions.

## Notes

This is an unofficial fan tool. It does not include Undertale game files or GameJolt project files. Use it with your own legitimate copy of Undertale and with mods you are allowed to download/use.
