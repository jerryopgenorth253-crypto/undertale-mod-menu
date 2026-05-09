# Undertale Mod Menu

![Windows](https://img.shields.io/badge/platform-Windows-2f7df6)
![Build](https://img.shields.io/badge/build-.NET%20Framework-7d70ff)
![Unofficial](https://img.shields.io/badge/status-unofficial%20fan%20tool-ff3f5c)

**Undertale Mod Menu** is a local Windows utility for editing Undertale save data, testing route states, managing inventory experiments, and installing downloaded Undertale fangame/mod packages in a safer, organized way.

It does **not** include Undertale game files, paid content, or downloaded GameJolt projects.

## Quick Start

1. Download `UndertaleSaveStudioPro.exe` from the latest GitHub Release.
2. Put `update.ini` beside the EXE.
3. Double-click `UndertaleSaveStudioPro.exe`.
4. Use **1 Load Save**, pick a route or preset, then use **3 Write Save** only after checking the values you changed.

Backups are created before save writes.

## Highlights

| Area | What It Does |
| --- | --- |
| Neon Feature Vault UI | Uses the thumbnail-inspired dark arcade look with glowing number cards, a big center hero, and bright action tiles. |
| Easy Player Mode | Adds a first-run guide, Quick Start buttons, simple step labels, and tooltips for new players. |
| Clean Feature Tools | Replaces generated 100K tabs with curated player-facing sections and professional feature cards. |
| Player numbers | Edit level, HP, EXP, gold, and damage with large uncapped inputs. |
| Route tools | Adjust FUN, route state, kills, murder-route flag, plot, room, and time. |
| Inventory Forge | Add regular Undertale items and experimental raw mod-token IDs. |
| Feature Tools | Pick named actions for player presets, route setup, room teleports, inventory loadouts, FUN events, story flags, and world settings. |
| GameJolt Mod Hub | Browse Undertale-tagged GameJolt projects, then install downloaded ZIP/folder mods into a managed library. |
| GitHub updater | Checks the latest GitHub release EXE and self-updates when a newer build exists. |

## Save Files

The app works with the normal local Undertale save folder:

```text
%LOCALAPPDATA%\UNDERTALE\
```

It can read/write:

```text
undertale.ini
file0
file9
codex_live.ini
```

## GameJolt Mod Hub

The mod hub loads Undertale-tagged projects from GameJolt and opens the selected project page in your browser. After you download a ZIP or folder, the app can install it into:

```text
%USERPROFILE%\Downloads\UndertaleGameJoltMods
```

Standalone fangames are copied as their own folder. `data.win`-style mods are layered onto a fresh copy of your selected Undertale game folder, so the original folder is not overwritten.

The hub does not bypass GameJolt download pages, login prompts, creator permissions, or project-specific install instructions.

## Auto-Update

This build is configured for:

```text
jerryopgenorth253-crypto/undertale-mod-menu
```

`update.ini` and the remote manifest can point at the repo EXE while a release is being prepared:

```text
https://raw.githubusercontent.com/jerryopgenorth253-crypto/undertale-mod-menu/main/UndertaleSaveStudioPro.exe
```

Before downloading, the app checks a remote build manifest:

```text
https://raw.githubusercontent.com/jerryopgenorth253-crypto/undertale-mod-menu/main/update-manifest.ini
```

It can also use the latest GitHub Release asset:

```text
https://github.com/jerryopgenorth253-crypto/undertale-mod-menu/releases/latest/download/UndertaleSaveStudioPro.exe
```

When the release EXE hash is different from the running EXE, the app downloads it, closes, replaces itself, saves the old file as `UndertaleSaveStudioPro.exe.previous`, and restarts.

## Build From Source

On Windows:

```powershell
.\build.ps1
```

The build script uses the built-in .NET Framework C# compiler and outputs:

```text
UndertaleSaveStudioPro.exe
```

GitHub Actions is included at:

```text
.github/workflows/build.yml
```

## Release Checklist

1. Run `.\build.ps1`.
2. Create a new GitHub Release.
3. Attach `UndertaleSaveStudioPro.exe`.
4. Attach or include `update.ini`.
5. Publish the release.

After that, existing users can receive the update through the built-in updater.

## Important Notes

- This is an unofficial fan project.
- Use it with your own legitimate copy of Undertale.
- Some save values can make a save unstable; backups are there for a reason.
- Raw attack-token IDs are for modded builds. Vanilla Undertale does not turn inventory slots into battle attacks by itself.
