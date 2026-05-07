# GitHub Upload Steps

## Option 1: Put the whole project on GitHub

1. Go to GitHub and create a new repository.
2. Choose `Add file` -> `Upload files`.
3. Upload the files from this folder, including:

```text
UndertaleSaveStudioPro.cs
UndertaleSaveStudioPro.exe
README.md
LICENSE.txt
RELEASE_NOTES.md
build.ps1
rooms.txt
InstallCodexLiveHook.csx
.github/workflows/build.yml
```

4. Commit the upload.

The EXE is tiny enough for GitHub, and the included GitHub Actions workflow will rebuild it on Windows after pushes.

## Option 2: Post just the EXE as a release

1. Open your GitHub repository.
2. Go to `Releases`.
3. Click `Draft a new release`.
4. Upload `UndertaleSaveStudioPro.exe` as the release asset.
5. Use `RELEASE_NOTES.md` for the release description.

## Important

Do not upload your local `backups` folder or downloaded GameJolt mods. The `.gitignore` is already set up to keep those out.
