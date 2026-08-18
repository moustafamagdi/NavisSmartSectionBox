# Navisworks 2024 Bundle Installation

This folder contains the correct **Navisworks-specific** application bundle structure. It corrects the earlier generic manifest guidance.

## Build and copy

1. Build `SmartSectionBox.sln` in **Release | Any CPU**.
2. Copy the entire `Deployment\SmartSectionBox.bundle` folder to one of these locations:

   ```text
   C:\ProgramData\Autodesk\ApplicationPlugins\
   ```

   or, for a per-user install without administrator access:

   ```text
   %APPDATA%\Autodesk\ApplicationPlugins\
   ```

3. Copy the build output:

   ```text
   SmartSectionBox\bin\Release\SmartSectionBox.ADSK.dll
   ```

   into:

   ```text
   SmartSectionBox.bundle\Contents\2024\SmartSectionBox.ADSK.dll
   ```

4. Confirm this exact final layout:

   ```text
   SmartSectionBox.bundle\
   ├── PackageContents.xml
   └── Contents\
       └── 2024\
           └── SmartSectionBox.ADSK.dll
   ```

5. Fully close Navisworks. In Task Manager, make sure no `Roamer.exe` process remains. Start **Navisworks Manage 2024** or **Navisworks Simulate 2024** again.

## Why the manifest matters

`PackageContents.xml` is configured specifically for Navisworks 2024:

| Manifest setting | Required value |
|---|---|
| Managed plug-in type | `AppType="ManagedPlugin"` |
| Product platforms | `Platform="NAVMAN|NAVSIM"` |
| Navisworks 2024 series | `SeriesMin="Nw21"` and `SeriesMax="Nw21"` |
| DLL path | `./Contents/2024/SmartSectionBox.ADSK.dll` |

Do **not** use a generic `Platform="Navisworks"`, `SeriesMin="2024"`, or `Contents\Windows` manifest for this Navisworks package. Those values prevent the bundle from being recognized by the Navisworks add-in loader.

## If the add-in is still absent

First open **Tools → Global Options → Interface → Add-Ins** (if available) and confirm external add-ins are not disabled. Then ensure the following conditions are true:

1. `PackageContents.xml` is at the `.bundle` root, not inside `Contents`.
2. The bundle directory ends in `.bundle`; Windows Explorer must not have silently named it `SmartSectionBox.bundle.bundle` or `SmartSectionBox.bundle.txt`.
3. The DLL name and its manifest `ModuleName` match exactly.
4. The DLL was compiled against **Navisworks 2024**, not another major version.
5. The full folder and DLL are readable by the Windows account that starts Navisworks.
6. You restarted Navisworks after copying the bundle.

For direct loader diagnostics, look under `%AppData%\NavisworksSmartSectionBox\Logs` after the add-in loads. If there is no log at all, Navisworks did not discover the package; inspect the bundle folder and `PackageContents.xml` first.
