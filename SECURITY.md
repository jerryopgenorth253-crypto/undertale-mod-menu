# Safety And Permissions

This project is a local fan utility. It edits files on your own computer and can download a replacement EXE only from the GitHub release URL configured in `update.ini`.

## Auto-Update Behavior

- Checks the configured GitHub release URL
- Downloads the release EXE to a temporary folder
- Compares SHA-256 hashes
- Replaces the running EXE only when the downloaded EXE is different
- Keeps the old EXE as `UndertaleSaveStudioPro.exe.previous`

## What Is Not Included

- No Undertale game files
- No paid game assets
- No downloaded GameJolt mod files
- No GameJolt download bypassing

## Reporting Issues

Open a GitHub issue with:

- What you clicked
- What folder or save file you were editing
- Any error text shown by the app
- Whether you can reproduce it after restoring from backup
