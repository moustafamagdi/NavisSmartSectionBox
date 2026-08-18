# Smart Section Box for Navisworks 2024

**Smart Section Box** is a C# add-in for Autodesk Navisworks Manage or Simulate 2024. It makes **direct viewport face dragging** the primary section-box workflow: hover a projected face, press the left mouse button, drag that face, and release to commit the exact final clipping boundary. The dock pane is a compact companion for precision entry, fitting, presets, and status feedback.

> The add-in targets **Navisworks 2024** and **.NET Framework 4.8**. Autodesk states that the Navisworks 2024 SDK is built against .NET Framework 4.8 and should be compiled with Visual Studio 2022. [1]

## What Is Implemented

| Capability | Implementation |
|---|---|
| Direct manipulation | `ToolPlugin` face hover, mouse capture, drag transaction, escape cancellation, final mouse-up commit, Shift/coarse and Ctrl/fine multipliers. |
| Face targeting | All six faces are built from eight rotated-box corners, projected through Navisworks `View.ProjectPoint`, polygon-tested over the full projected face, then selected deterministically by distance and depth. |
| Clipping | The public 2024 `View.GetClippingPlanes`, `TrySetClippingPlanes`, and `SetClippingPlanes` JSON contract is used. Native JSON is preserved as a template, avoiding a hard dependency on an undocumented DTO. |
| Camera behavior | World-to-screen uses Navisworks projection. Perspective drag scale uses the face-camera distance; orthographic scale uses visible view height. |
| Box safety | Only the selected local face coordinate changes. Minimum thickness clamps prevent min/max inversion. |
| Rotation | Corner creation, face normals, hit testing, and drag deltas use the same X-Y-Z Euler transform. Drag deltas are inverse-rotated into box-local coordinates before a face coordinate changes. |
| Dock pane | Official `DockPanePlugin` plus `ElementHost` WPF hosting; bidirectional synchronization with the same `SectionBoxService` used by the tool. |
| Presets | Document-scoped JSON presets under `%AppData%\NavisworksSmartSectionBox\Presets\<document-hash>`. |
| Diagnostics | Rolling log files under `%AppData%\NavisworksSmartSectionBox\Logs`. |

## API Verification

The implementation is grounded in the **official Navisworks 2024 SDK**, which was downloaded and inspected during development. The SDK installs its API reference, developer guide, and samples with Navisworks Manage/Simulate. [2]

| API area | Verified C# signature / supported pattern | Where it is used |
|---|---|---|
| Clipping read | `string View.GetClippingPlanes()` | `SectionBoxService.GetCurrentBox()` |
| Clipping write | `void View.SetClippingPlanes(string jsonClipPlaneSet)` | Documented reference; the implementation takes the safer hot-path below. |
| Clipping write, non-throwing | `bool View.TrySetClippingPlanes(string jsonClipPlaneSet)` | `SectionBoxService.SetBox()` |
| Projection | `ProjectionResult View.ProjectPoint(Point3D point, bool sectionClip, bool frustumClip)` | `CameraProjection.WorldToScreen()` |
| Viewport dimensions | `View.Width`, `View.Height` | Camera scaling |
| Mouse down | `bool ToolPlugin.MouseDown(View, KeyModifiers, ushort, int, int, double)` | Face capture |
| Mouse move | `bool ToolPlugin.MouseMove(View, KeyModifiers, ushort, int, int, double)` | Hover and drag |
| Mouse up | `bool ToolPlugin.MouseUp(View, KeyModifiers, ushort, int, int, double)` | Exact final apply |
| Escape | `bool ToolPlugin.KeyDown(View, KeyModifiers, ushort, double)` | Transaction cancellation |
| Cursor | `Cursor ToolPlugin.GetCursor(View, KeyModifiers)` | `CursorManager` |
| Overlay | `void ToolPlugin.OverlayRender(View, Graphics)` | Reserved supported hook; no unsupported window overlay is used. |
| Docking | `DockPanePlugin.CreateControlPane()` / `DestroyControlPane(Control)` | `SmartSectionBoxDockPanePlugin` |
| Tool activation | `Application.MainDocument.Tool.SetCustomToolPlugin(...)` | `SmartSectionBoxAddin` |

> **Important:** Autodesk documents the clipping parameter as a JSON `ClipPlaneSet`, but does not publish a field-by-field Box schema in the public 2024 API reference. The add-in therefore prefers the live native JSON returned by `GetClippingPlanes()`, discovers the box coordinate member, alters only Min/Max, rotation, and enabled values, and validates every write through `TrySetClippingPlanes`. A fallback envelope is isolated in `SectionBoxJsonAdapter` solely for first-time box creation and is never treated as a substitute for a live native payload. This approach follows the documented get-then-set workflow also described in current Navisworks API discussion. [3]

## Build

Install **Navisworks Manage 2024** or **Navisworks Simulate 2024** and Visual Studio 2022 with the **.NET desktop development** workload.

1. Open `SmartSectionBox.sln` in Visual Studio 2022.
2. Confirm `SmartSectionBox\SmartSectionBox.csproj` has the correct `NavisworksInstallDir`. By default it targets:

   ```text
   C:\Program Files\Autodesk\Navisworks Manage 2024
   ```

   For Simulate, set an MSBuild property such as:

   ```text
   /p:NavisworksInstallDir="C:\Program Files\Autodesk\Navisworks Simulate 2024"
   ```

3. Build **Release | Any CPU**. The Navisworks API references deliberately set `Private=False`; the host supplies the runtime assemblies.
4. To deploy automatically during a local build, use:

   ```text
   /p:DeployPlugin=true
   ```

   The default deployment folder is:

   ```text
   %AppData%\Autodesk\Navisworks Manage 2024\Plugins\SmartSectionBox\
   ```

   You may override it with `PluginDeployDir` when using Simulate or an enterprise plug-in deployment location.

## Install and Activate

Copy `SmartSectionBox.ADSK.dll` (and its PDB for debugging) to a Navisworks 2024 plug-in folder, then restart Navisworks. Locate **Smart Section Box** in the plug-in/ribbon command list and run it. The command opens the dock pane, registers the custom tool, enables clipping, and tries to create a box around the active model when no native box payload is available.

The first click on **Fit to Model** is the recommended initialization path. If Navisworks rejects the first fallback JSON payload, create a native box once through the Navisworks sectioning UI, click **Refresh**, and retry. The add-in then uses the exact payload returned by that installation and document.

## Direct Face Dragging Workflow

1. Run **Smart Section Box** and use **Fit to Model** or **Fit to Selection**.
2. Move the pointer over a section-box face in the active 3D viewport.
3. The tool identifies the face from the projected quadrilateral, not merely its center, and returns a handled cursor.
4. Press the left mouse button over the face. The tool takes a copy of the initial section-box state but does not mutate it at mouse-down.
5. Drag. The active face alone moves along its transformed normal. The dock pane updates from the same authoritative state source.
6. Hold **Shift** for the configurable coarse multiplier (default 2.0) or **Ctrl** for the configurable fine multiplier (default 0.25).
7. Release to apply the exact final state immediately. Press **Esc** instead to restore the initial state.

When the pointer is not over a face and no face drag is active, mouse callbacks return `false`; Navisworks navigation and normal input remain available. Autodesk’s input sample demonstrates this custom-tool pattern and calls `RequestDelayedRedraw(ViewRedrawRequests.Render)` after interaction changes.

## Projection and Drag Mathematics

The tool does not map mouse X to model X or mouse Y to model Y. `View.ProjectPoint` supplies face-corner screen coordinates in the active perspective or orthographic view. The hit tester uses a point-in-polygon test, tolerates near edges, and sorts overlapping candidates deterministically by edge distance, projected depth, and face identity.

For drag motion, the tool projects the current face normal into screen space. Mouse displacement projected onto that direction becomes a camera-scaled world distance. For a perspective camera, one pixel at the face is derived from the vertical visible extent scaled by camera-to-face distance divided by focal distance. For an orthographic camera, the visible height remains fixed. The resulting world vector is inverse-rotated back into box-local space and only the grabbed local face coordinate is modified. A direct-on face view projects the normal almost to a point; the controller handles that degenerate case with a stable screen-up fallback while preserving camera-scaled sensitivity.

## Architecture

```text
SmartSectionBox/
├── Core/
│   ├── SectionBoxState.cs            # Authoritative UI-independent state
│   ├── SectionBoxFace.cs             # Six face identities and vector type
│   ├── SectionBoxMath.cs             # Rotated corners / normals / bounds
│   ├── SectionBoxJsonAdapter.cs      # Native ClipPlaneSet JSON discovery
│   └── SectionBoxService.cs          # Safe Navisworks clipping service
├── Interaction/
│   ├── CameraProjection.cs
│   ├── FaceHitTester.cs
│   ├── DragController.cs
│   ├── CursorManager.cs
│   └── SectionBoxToolPlugin.cs
├── UI/
│   ├── SectionBoxDockPane.xaml
│   ├── SectionBoxDockPane.xaml.cs
│   ├── SmartSectionBoxDockPanePlugin.cs
│   └── ViewModels/
├── Persistence/
│   └── PresetStore.cs
├── Plugin/
│   ├── SmartSectionBoxRuntime.cs
│   └── SmartSectionBoxAddin.cs
└── Infrastructure/
    └── Logger.cs
```

## Supported and Limited Requirements

| Requirement | Status | Notes |
|---|---|---|
| Real viewport mouse input | **CONFIRMED** | Official 2024 `ToolPlugin` mouse callbacks are used. |
| Drag one box face | **CONFIRMED** | Transaction state and one-face local-coordinate edits are implemented. |
| Live clipping | **CONFIRMED** | `TrySetClippingPlanes` is throttled to 75 ms and mouse-up forces the final update. |
| Perspective and orthographic projection | **CONFIRMED** | Uses `View.ProjectPoint` and `ViewpointProjection`. |
| Rotated geometry / interaction architecture | **CONFIRMED** | X-Y-Z rotations affect corners, normals, hit tests, and local drag deltas. Native rotation writes require the live payload to expose the expected rotation array. |
| WPF dock pane | **CONFIRMED** | Uses Autodesk’s supported `DockPanePlugin`/`ElementHost` pattern. |
| Custom 3D overlay | **POSSIBLE WITH WORKAROUND** | `OverlayRender` is officially available, but a highlighted polygon is deliberately not drawn until it can be validated against the installed host. Cursor plus live dock-pane status provide supported feedback without HWND hacks. |
| Axis-specific resize cursors | **NOT SUPPORTED BY PUBLIC CURSOR ENUM** | The documented enum exposes `Unhandled`, `Handled`, and `HyperHand`, not directional resize glyphs. The add-in uses `Handled` only over draggable faces. |
| Box clipping direction inversion | **NOT APPLICABLE TO CLOSED BOX** | Direction inversion is meaningful for a plane. The UI keeps the requested action but provides a clear non-destructive status message for box mode. |
| Saved Viewpoint clipping integration | **DEFERRED** | Native viewpoints already retain their own section state. The custom document-scoped preset system is implemented and avoids altering user saved viewpoints. |
| Unit-format conversion | **LIMITED** | Values are explicitly presented in the Navisworks model coordinate units used by the bounding boxes and clipping JSON. The 2024 API exposes the `Units` enum, but cross-model display conversion has not been hardcoded; this avoids silently converting federated coordinates incorrectly. |

## Validation Matrix

Validate the compiled plug-in in an installed Navisworks 2024 host before production release. The Linux build workspace used to generate this repository cannot launch Navisworks or load the proprietary host DLLs.

| Area | Required test |
|---|---|
| Basic box operations | Fit to Model; Fit to Selection; Min/Max X, Y, and Z direct drags; Reset. |
| Camera | Perspective, Orthographic, front, side, top, arbitrary orbit, close and distant camera. |
| Geometry | Small, large, asymmetric, and rotated section boxes. |
| Models | Single NWD, federated NWD, large coordinate offset, and selection across models. |
| Interaction | Hover, face mouse-down, live drag, mouse-up final exactness, Escape cancel, Shift, Ctrl, and navigation away from faces. |
| JSON compatibility | Create a native Box in the target Navisworks build, Refresh, drag every face, inspect `%AppData%\NavisworksSmartSectionBox\Logs` for rejection messages, and retain an anonymized `GetClippingPlanes()` sample for regression tests. |

## Troubleshooting

| Symptom | Action |
|---|---|
| The command does not appear | Confirm the DLL is in a Navisworks 2024 plug-in folder and that it was compiled against the matching Manage/Simulate 2024 API DLL. |
| Build fails resolving `Autodesk.Navisworks.Api.dll` | Set `NavisworksInstallDir` to the installed product folder. |
| First box creation fails | Use Navisworks sectioning UI to create a native Box once, run **Refresh**, then use **Fit to Model**. Check the log file. |
| A face does not capture | Confirm clipping is enabled and that the pointer is inside or close to a projected face; test after Refresh. |
| Navigation is blocked | Verify a mouse button was released. Press **Esc** to cancel the drag transaction. |
| UI and viewport differ | Click **Refresh**. The next native payload becomes the source template. |

## References

[1]: https://blog.autodesk.io/navisworks-2024-sdk-is-posted/ "Navisworks 2024 SDK is posted — Autodesk Developer Blog"
[2]: https://aps.autodesk.com/developer/overview/navisworks-api "Navisworks API — Autodesk Platform Services"
[3]: https://www.linkedin.com/pulse/navisworks-api-sectioning-control-net-gavin-yang-li-fvyxc "Navisworks API Sectioning Control with .NET"
[4]: https://forums.autodesk.com/t5/navisworks-api-forum/enable-section-box-by-using-the-api/td-p/4966628 "Enable section box by using the API — Autodesk Community"
