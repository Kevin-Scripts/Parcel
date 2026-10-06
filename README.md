# Parcel

# Made using Astra and Gpt-6 Luna.

Open **Parcel.exe** in this folder. This is a standalone, portable Windows desktop application, with no command window or installation step. It uses the Windows .NET Framework runtime (4.5 or later). The refreshed dark dashboard matches the kevin-advancedstorageunits UI: charcoal surfaces, a silver accent, subtle borders, rounded cards and Segoe UI typography. The application icon is embedded in the executable.

1. Select the resource's `fxmanifest.lua` when prompted, or cancel and use **Select folder**.
2. Choose **Escrow** or **Source code** and check the resource name.
3. Review the included/excluded file list. Adjust exclusion patterns and files to keep editable, then click **Refresh preview**. To exclude from the preview list, select one or more included files (Ctrl/Shift for several), then right-click or click **Exclude selected...** and choose **Exclude only this file** or **Exclude entire folder**; the pattern is added and the preview refreshes.
4. Click **Create ZIP**. If you chose a **Save ZIPs to** folder, the ZIP is saved there automatically with a timestamped name (the folder is remembered the next time the app opens; **Clear** returns to being asked each time). Otherwise choose a new destination filename. Then upload the result to Cfx Assets. **Show ZIP in folder** reveals the completed file.

The application never deletes resource files or overwrites an existing ZIP. It changes only the manifest inside the new ZIP. Existing manifest escrow exclusions are preserved; added editable-file patterns extend them. Source mode adds `*` and `**/*` so all files stay unencrypted.

If the resource has `packaging.json`, the app reads its include lists, required files and mode-specific defaults. Otherwise it includes the resource's files with common development files excluded. In escrow mode it excludes known UI source/build configuration paths under `web`, `ui` or `html` only when that directory contains `dist/index.html`. Review these defaults for custom resource layouts. It packages the existing UI build and does not run npm. An explicit local `ui_page` must exist in the package.

Exclusion patterns use `/` separators, `*` for any characters (including separators) and `?` for one character. Excluded directory rows represent the entire folder; their contents are not scanned. Hidden/dot paths, node_modules, releases, this application, and symbolic links/junctions are always skipped. Editable-file patterns use Cfx's manifest glob syntax. UI rule edits apply to the current selection only; changing resource or mode reloads its defaults. Edit `packaging.json` to persist your rules.

The ZIP contains one folder named after the resource. Cfx performs encryption after upload; NUI JavaScript is not protected by escrow. The app rejects ZIPs at or above 1,000,000,000 bytes. It does not execute Lua manifests or validate all dynamic manifest paths, dependencies or in-game behavior.

## Build from source

From this folder, run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`. The build creates `Parcel.exe` here, embeds `assets\Parcel.ico`, and uses the .NET Framework compiler bundled with Windows; it requires no NuGet packages. Add `-Test` to compile and run packaging and UI checks. Tests create their own fixtures in a temporary `build\` folder that is deleted afterwards and refresh `assets\ui-preview.png`; no tablet checkout is needed.

Project layout: `Parcel.exe` and `build.ps1` in the root, application source in `src\`, tests in `tests\`, and the icon and UI preview in `assets\`. The generated application is a single EXE; you can copy it elsewhere without these source files.
