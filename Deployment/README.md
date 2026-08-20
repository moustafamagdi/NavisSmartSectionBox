# Navisworks 2024 and 2027 Bundle Installation

This folder contains one **Navisworks-specific** application bundle that can serve Navisworks Manage or Simulate **2024** and **2027**. The two product releases must load different DLLs because Autodesk’s managed Navisworks API is major-version-specific. The bundle manifest selects the correct DLL automatically.

## Build the matching binary

Open `SmartSectionBox.sln` in Visual Studio 2022 and select the configuration that matches the installed Navisworks host.

| Installed host | Visual Studio configuration | Output DLL | Bundle destination |
|---|---|---|---|
| Navisworks Manage or Simulate 2024 | `Release | Any CPU` | `SmartSectionBox\bin\Release\SmartSectionBox.ADSK.dll` | `Contents\2024\SmartSectionBox.ADSK.dll` |
| Navisworks Manage or Simulate 2027 | `Release2027 | Any CPU` | `SmartSectionBox\bin\Release2027\SmartSectionBox.ADSK.dll` | `Contents\2027\SmartSectionBox.ADSK.dll` |

The 2027 configuration automatically expects its API reference at:

```text
C:\Program Files\Autodesk\Navisworks Manage 2027\Autodesk.Navisworks.Api.dll
```

For a Simulate installation or a non-default location, set the project’s `NavisworksInstallDir` MSBuild property to the folder containing `Autodesk.Navisworks.Api.dll` before building. Do not compile a 2027 DLL using the 2024 API assembly.

## Install the shared bundle

1. Copy the entire `Deployment\SmartSectionBox.bundle` folder to one of the following locations:

   ```text
   C:\ProgramData\Autodesk\ApplicationPlugins\
   ```

   Or, for a per-user installation without administrator access:

   ```text
   %APPDATA%\Autodesk\ApplicationPlugins\
   ```

2. Copy each available build output into its matching `Contents` folder. A dual-version deployment has this layout:

   ```text
   SmartSectionBox.bundle\
   ├── PackageContents.xml
   └── Contents\
       ├── 2024\
       │   └── SmartSectionBox.ADSK.dll
       └── 2027\
           └── SmartSectionBox.ADSK.dll
   ```

3. Fully close Navisworks. In Task Manager, confirm that no `Roamer.exe` process remains, then start the required Navisworks version.

## Manifest routing

| Host version | Manifest runtime series | Managed DLL path |
|---|---|---|
| Navisworks 2024 | `SeriesMin="Nw21"` and `SeriesMax="Nw21"` | `./Contents/2024/SmartSectionBox.ADSK.dll` |
| Navisworks 2027 | `SeriesMin="Nw24"` and `SeriesMax="Nw24"` | `./Contents/2027/SmartSectionBox.ADSK.dll` |

Both components use `AppType="ManagedPlugin"` and `Platform="NAVMAN|NAVSIM"`, so each supports Navisworks Manage and Simulate. The active Navisworks host evaluates the runtime series and loads only its matching DLL.

> Do not use a generic `Platform="Navisworks"`, `SeriesMin="2027"`, or `Contents\Windows` manifest. Navisworks uses its own series codes and application-bundle layout.

## If the add-in is absent

First open **Tools → Global Options → Interface → Add-Ins** where available and confirm external add-ins are not disabled. Then verify that `PackageContents.xml` is at the `.bundle` root, the bundle name ends in exactly one `.bundle` suffix, the matching DLL exists in its version folder, the DLL was compiled against the same Navisworks major version, and the Windows account starting Navisworks can read the bundle. Restart Navisworks after every DLL or manifest update.

For loader and interaction diagnostics, inspect `%AppData%\NavisworksSmartSectionBox\Logs` after the add-in loads. If no log exists, Navisworks did not discover the bundle; inspect the manifest and version-folder placement first.
