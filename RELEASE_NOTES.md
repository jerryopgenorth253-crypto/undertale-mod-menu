# Release Notes

## Clean Feature Tabs Build

### New

- Removed the visible Chaos tab/buttons from the main player UI
- Replaced generated `100K` Feature tabs with curated sections: Player Presets, Route Setup, Room Teleports, Inventory Loadouts, FUN Events, Story Flags, World Settings, and Phone & Menu
- Rebuilt Feature buttons as professional cards with a category label, clear action name, and direct description
- Removed generated Matrix/random-number feature builders so the Features menu no longer shows placeholder bulk buttons
- Build manifest bumped to `202605091735`

## Cleaner Tools Build

### New

- Room teleport in Chaos Console now has search, a room list, safe random, any-room random, and clearer status text
- Feature Vault generated tools now use plain names such as Boost Stats, Change Route, Teleport, Fill Inventory, Set Flags, and Full Chaos
- Feature Vault buttons now show a short description directly on each button
- Removed the confusing public-facing "Matrix" labels from generated Feature Vault categories
- Build manifest bumped to `202605091700`

## Neon Feature Vault Build

### New

- Thumbnail-inspired neon dashboard style
- Stacked glowing player-number cards on the left
- Big center Feature Vault hero panel
- Preset tiles for Fresh Start, Ruins Boost, Judgement, Sans Practice, Omega Ready, and Absolute Max
- Bottom action strip for Chaos, Forge, God, and Random
- Build manifest bumped to `202605091500`

## Easy Player Mode Build

### New

- Clearer first-run flow with Load Save, pick values/tools, and Write Save steps
- Quick Start panel for Load, Safe Start, Make OP, Guide, and Write
- New Player Guide pop-up with plain-language tips for safe save editing
- Friendlier button names and tooltips for common player actions
- Build manifest bumped to `202605091200`

## Pro Max Build

### New

- Native Windows app with a cleaner Pro Max layout
- Large editable fields for level, HP, EXP, gold, and damage
- Uncapped player-number controls up to `999,999,999`
- Inventory Forge for regular items and raw mod-token IDs
- Chaos Console for randomizers and raw save-line experiments
- Feature Vault 100K+ with searchable one-click presets
- GameJolt Mod Hub for browsing Undertale-tagged projects and installing downloaded mod packages
- GitHub self-updater with startup checks, manual check button, and `update.ini`

### Save Safety

- Backup files are created before live save writes
- GameJolt `data.win`-style mod installs are layered onto a copied Undertale folder
- Standalone fangames are copied into a separate managed library

### Included Files

```text
UndertaleSaveStudioPro.exe
update.ini
build.ps1
README.md
LICENSE.txt
```

This repository does not include Undertale assets, Undertale game files, or downloaded GameJolt mod files.
