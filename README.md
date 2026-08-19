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

   This creates the Navisworks 2024 application bundle below:

   ```text
   %AppData%\Autodesk\ApplicationPlugins\SmartSectionBox.bundle\
   ├── PackageContents.xml
   └── Contents\2024\SmartSectionBox.ADSK.dll
   ```

   The bundle manifest uses `AppType="ManagedPlugin"`, `Platform="NAVMAN|NAVSIM"`, and Navisworks 2024 series `Nw21`. Override `PluginBundleDir` for a machine-wide deployment, such as `C:\ProgramData\Autodesk\ApplicationPlugins\SmartSectionBox.bundle`.

## Install and Activate

Use the checked-in [`Deployment/SmartSectionBox.bundle`](Deployment/SmartSectionBox.bundle) template rather than copying a DLL to a legacy product `Plugins` folder. Copy the Release DLL into `Contents\2024`, retain `PackageContents.xml` at the bundle root, then fully restart Navisworks. See [`Deployment/README.md`](Deployment/README.md) for the exact folder tree and diagnostics. Once the bundle is discovered, locate **Smart Section Box** in the Navisworks plug-in/ribbon command list. The command opens the minimal dock pane. Activation adopts an existing native Box section, or creates a box around currently selected elements; it does not create a model-wide box.

Click **Activate Smart Section Box** after selecting elements, or after creating a native Navisworks Box section. The add-in preserves an existing native box when present; otherwise, it emits the verified `ClipPlaneSet`/`OrientedBox3D` schema to fit the selected elements. If the host rejects a write, inspect the add-in log before retrying.

## Direct Face Dragging Workflow

The dock pane is intentionally a minimal launcher. All section-box editing happens directly in the 3D viewport.

1. Either select one or more model elements, **or** create a standard Box section through Navisworks first.
2. Run **Smart Section Box** and click **Activate Smart Section Box**. When a native box exists, the tool adopts it unchanged. When no native box exists, the tool fits a new box to the current element selection. If neither condition is met, it gives an instruction and does not create a model-wide box.
3. Navisworks remains the internal clipping engine. To preserve viewport performance on large federated models, Smart Section Box does **not** draw a custom box overlay.
4. Direct interaction still uses the current section-box geometry internally. When faces overlap from the current camera angle, hold **Ctrl** while pressing and dragging to choose from the invisible back/underlay face set.
5. Hold **Shift** for the configurable coarse multiplier (default 2.0). Release to apply the final state immediately. Press **Esc** instead to restore the state at mouse-down.

> The native Navisworks box is deliberately not placed into Move mode after activation, and no custom wireframe is rendered. This performance-focused mode updates clipping without additional viewport drawing.

When the pointer is not over a face and no face drag is active, mouse callbacks return `false`; Navisworks navigation and normal input remain available. Autodesk’s input sample demonstrates this custom-tool pattern and calls `RequestDelayedRedraw(ViewRedrawRequests.Render)` after interaction changes.

## Face-Pull Diagnostics and Calibration

The **Record face-pull diagnostics** checkbox is off by default. Turn it on only while investigating a face-selection issue, then use **Enable Face Pull in 3D View**, perform one or more click-and-drag attempts, and turn it off again. The trace is written to:

```text
%AppData%\NavisworksSmartSectionBox\Logs\smart-section-box-YYYY-MM-DD.log
```

Each `FACE_DIAGNOSTIC` entry records the screen click point, the complete projected candidate-face list, whether the pointer was inside a candidate or merely close to an edge, edge distance, projection depth, projected face polygons, the selected face, and the start/final coordinate after the drag. It intentionally excludes model properties and item-selection data.

A typical sequence has `POINTER_DOWN`, `DRAG_BEGIN`, and `DRAG_END` entries. Send only those `FACE_DIAGNOSTIC` lines, together with a screenshot of the view, to calibrate hit selection for the camera angle and box geometry that produced the unexpected result.

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
| First box creation fails | Select valid model elements, activate the tool, then inspect the add-in log if Navisworks rejects the verified `ClipPlaneSet`/`OrientedBox3D` payload. |
| A face does not capture | Confirm an element is selected or a native Navisworks Box section already exists, then click **Activate Smart Section Box**. Hold **Ctrl** for the invisible overlapping back-face set. |
| Navigation is blocked | Verify a mouse button was released. Press **Esc** to cancel the drag transaction. |
| UI and viewport differ | Reactivate the tool to adopt the current native box, then inspect the diagnostics log if clipping does not match the current section-box state. |
| The pane is clipped or controls overlap | Deploy the current DLL, delete the prior `SmartSectionBox.bundle`, then recreate the bundle from `Deployment/SmartSectionBox.bundle`. The revised pane has no sliders and uses a responsive host. |
| Activation reports no target | Select at least one model element, or create a native Navisworks Box section, then activate the tool again. |
| The wrong face is selected | Normal drags use the nearest camera-facing face; hold **Ctrl** for the invisible underlay set. Enable **Record face-pull diagnostics** and share the `FACE_DIAGNOSTIC` lines plus a viewport screenshot if selection is still unexpected. |
| A face pulls in the opposite direction | Install the current update, which derives screen direction from local face dimensions rather than distance from world origin. Capture diagnostics if a direction inversion persists. |

## References

[1]: https://blog.autodesk.io/navisworks-2024-sdk-is-posted/ "Navisworks 2024 SDK is posted — Autodesk Developer Blog"
[2]: https://aps.autodesk.com/developer/overview/navisworks-api "Navisworks API — Autodesk Platform Services"
[3]: https://www.linkedin.com/pulse/navisworks-api-sectioning-control-net-gavin-yang-li-fvyxc "Navisworks API Sectioning Control with .NET"
[4]: https://forums.autodesk.com/t5/navisworks-api-forum/enable-section-box-by-using-the-api/td-p/4966628 "Enable section box by using the API — Autodesk Community"
