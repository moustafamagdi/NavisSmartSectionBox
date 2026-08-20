# Smart Section Box for Navisworks 2024

**Smart Section Box** is a C# add-in for Autodesk Navisworks Manage or Simulate 2024. It makes **direct viewport face dragging** the primary section-box workflow: grab a native section-box face in the 3D view, drag it, and release to commit the exact final clipping boundary. The compact dock pane contains only activation, status, and opt-in diagnostics; all box editing occurs in the viewport.

> The add-in targets **Navisworks 2024** and **.NET Framework 4.8**. Autodesk states that the Navisworks 2024 SDK is built against .NET Framework 4.8 and should be compiled with Visual Studio 2022. [1]

## What Is Implemented

| Capability | Implementation |
|---|---|
| Direct manipulation | `ToolPlugin` hover, mouse capture, absolute drag transaction, Escape cancellation, final mouse-up commit, and Shift coarse multiplier. Only camera-facing faces are eligible; orbit the view to reach another side. |
| Face targeting | A calibrated camera ray intersects the six oriented-box planes in world space. Hits are bounded in face-local UV coordinates, filtered by ray-facing direction, and ordered by true ray distance `t`. |
| Clipping | The public 2024 `View.GetClippingPlanes`, `TrySetClippingPlanes`, and `SetClippingPlanes` JSON contract is used. Native JSON is preserved as a template, avoiding a hard dependency on an undocumented DTO. |
| Camera behavior | `Viewpoint.Position`, quaternion `Rotation`, focal-plane extents, and projection type construct perspective and orthographic rays. `View.ProjectPoint` verifies each camera state before ray picking is enabled. |
| Box safety | Only the selected oriented face moves; the opposite oriented face remains fixed. Minimum thickness clamps prevent inversion. |
| Rotation | Corner creation, face normals, hit testing, and drag deltas use the same X-Y-Z Euler transform. A drag moves the raw box centre along the captured rotated normal and changes only that axis’s half-extent, preserving the opposite oriented face. |
| Dock pane | Official `DockPanePlugin` plus `ElementHost` WPF hosting; only activation, status, and opt-in diagnostics are exposed. |
| Coordinate fields, sliders, and presets | Deliberately omitted from the user interface; native viewport face dragging is the sole editing workflow. |
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
| Mouse move | `bool ToolPlugin.MouseMove(View, KeyModifiers, int, int, double)` | Hover |
| Mouse up | `bool ToolPlugin.MouseUp(View, KeyModifiers, ushort, int, int, double)` | Exact final apply |
| Escape | `bool ToolPlugin.KeyDown(View, KeyModifiers, ushort, double)` | Transaction cancellation |
| Cursor | `Cursor ToolPlugin.GetCursor(View, KeyModifiers)` | `CursorManager` |
| Overlay | `void ToolPlugin.OverlayRender(View, Graphics)` | Reserved supported hook; no unsupported window overlay is used. |
| Docking | `DockPanePlugin.CreateControlPane()` / `DestroyControlPane(Control)` | `SmartSectionBoxDockPanePlugin` |
| Tool activation | `Application.MainDocument.Tool.SetCustomToolPlugin(...)` | `SmartSectionBoxAddin` |

> **Important:** Autodesk documents the clipping parameter as a JSON `ClipPlaneSet`, but does not publish a field-by-field Box schema in the public 2024 API reference. The add-in therefore prefers the live native JSON returned by `GetClippingPlanes()`, discovers the box coordinate member, alters only Min/Max, rotation, and enabled values, and validates every write through `TrySetClippingPlanes`. A fallback envelope is isolated in `SectionBoxJsonAdapter` solely for first-time box creation and is never treated as a substitute for a live native payload. This approach follows the documented get-then-set workflow also described in current Navisworks API discussion. [3]

## Build

Install **Navisworks Manage or Simulate 2024 or 2027** and Visual Studio 2022 with the **.NET desktop development** workload. Navisworks managed API assemblies are major-version-specific, so the solution compiles the shared source against a separate host reference for each supported release.

1. Open `SmartSectionBox.sln` in Visual Studio 2022.
2. Select the configuration matching the installed Navisworks host:

   | Installed host | Visual Studio configuration | Default API folder | Output folder |
   |---|---|---|---|
   | Navisworks 2024 | `Release | Any CPU` | `C:\Program Files\Autodesk\Navisworks Manage 2024` | `bin\Release` |
   | Navisworks 2027 | `Release2027 | Any CPU` | `C:\Program Files\Autodesk\Navisworks Manage 2027` | `bin\Release2027` |

3. For a Simulate installation or a custom directory, set `NavisworksInstallDir` to the matching host folder, for example:

   ```text
   /p:NavisworksInstallDir="C:\Program Files\Autodesk\Navisworks Simulate 2027"
   ```

4. Build the selected configuration. The API references deliberately set `Private=False`; the Navisworks host supplies the runtime assemblies. Never compile the 2027 configuration using the 2024 API DLL.
5. To deploy automatically during a local build, add:

   ```text
   /p:DeployPlugin=true
   ```

   The configuration copies the matching DLL to the version-specific folder below:

   ```text
   %AppData%\Autodesk\ApplicationPlugins\SmartSectionBox.bundle\
   ├── PackageContents.xml
   └── Contents\
       ├── 2024\SmartSectionBox.ADSK.dll
       └── 2027\SmartSectionBox.ADSK.dll
   ```

   The manifest uses `AppType="ManagedPlugin"`, `Platform="NAVMAN|NAVSIM"`, and separate runtime series (`Nw21` for 2024 and `Nw24` for 2027). Override `PluginBundleDir` for a machine-wide deployment, such as `C:\ProgramData\Autodesk\ApplicationPlugins\SmartSectionBox.bundle`.

## Install and Activate

Use the checked-in [`Deployment/SmartSectionBox.bundle`](Deployment/SmartSectionBox.bundle) template rather than copying a DLL to a legacy product `Plugins` folder. Place every available release-specific DLL in its matching `Contents` version folder, retain `PackageContents.xml` at the bundle root, then fully restart Navisworks. See [`Deployment/README.md`](Deployment/README.md) for the exact folder tree and diagnostics. Once the bundle is discovered, locate **Smart Section Box** in the Navisworks plug-in/ribbon command list. The command opens the minimal dock pane. Activation adopts an existing native Box section, or creates a box around currently selected elements; it does not create a model-wide box.

Click **Activate Smart Section Box** after selecting elements, or after creating a native Navisworks Box section. The add-in preserves an existing native box when present; otherwise, it emits the verified `ClipPlaneSet`/`OrientedBox3D` schema to fit the selected elements. If the host rejects a write, inspect the add-in log before retrying.

### Navisworks 2027 Host Validation

After building `Release2027`, install the bundle with the DLL in `Contents\\2027` and start Navisworks Manage or Simulate 2027. Confirm that the **Smart Section Box** command appears, activation can adopt an existing native Box section, and `Record face-pull diagnostics` reports `picker=ray` and `calibration=valid` during a drag. Test Perspective and Orthographic views, then drag every visible Min/Max X, Y, and Z face of both an axis-aligned and a rotated native box. Confirm that the selected oriented face moves while its opposite face remains fixed. This host test is required because the proprietary 2027 API assembly and renderer are unavailable in the Linux validation workspace.

## Direct Face Dragging Workflow

The dock pane is intentionally a minimal launcher. All section-box editing happens directly in the 3D viewport.

1. Either select one or more model elements, **or** create a standard Box section through Navisworks first.
2. Run **Smart Section Box** and click **Activate Smart Section Box**. When a native box exists, the tool adopts it unchanged. When no native box exists, the tool fits a new box to the current element selection. If neither condition is met, it gives an instruction and does not create a model-wide box.
3. Navisworks remains the internal clipping engine. To preserve viewport performance on large federated models, Smart Section Box does **not** draw a custom box overlay. The light-blue hover status row in the dock pane reports the face that will be captured, such as `Face: +X (1968.007)`, without adding any viewport rendering.
4. Direct interaction uses the current section-box geometry internally. Each press selects only the nearest valid **camera-facing** face. The tool never selects a hidden or underlay face. To edit another side, orbit the Navisworks view until that face is visible and camera-facing, then drag it normally.
5. Hold **Shift** for the configurable coarse multiplier (default 2.0). Release to apply the final state immediately. Press **Esc** instead to restore the state at mouse-down.

> The native Navisworks box is deliberately not placed into Move mode after activation, and no custom wireframe is rendered. This performance-focused mode updates clipping without additional viewport drawing.

When the pointer is not over a face and no face drag is active, mouse callbacks return `false`; Navisworks navigation and normal input remain available. Autodesk’s input sample demonstrates this custom-tool pattern and calls `RequestDelayedRedraw(ViewRedrawRequests.Render)` after interaction changes.

## Face-Pull Diagnostics and Calibration

The **Record face-pull diagnostics** checkbox is off by default. Turn it on only while investigating a face-selection issue, activate **Smart Section Box**, perform one or more click-and-drag attempts, and turn it off again. The trace is written to:

```text
%AppData%\NavisworksSmartSectionBox\Logs\smart-section-box-YYYY-MM-DD.log
```

Each `FACE_DIAGNOSTIC` entry records the screen click point, picker mode, calibration result, selected candidate index, true ray distance, face-local UV hit coordinates, world tolerance, facing classification, and the start/final coordinate after the drag. Fallback entries additionally include the legacy projected polygon data. The trace intentionally excludes model properties and item-selection data.

A typical sequence has `POINTER_DOWN`, `DRAG_BEGIN`, and `DRAG_END` entries. Send only those `FACE_DIAGNOSTIC` lines, together with a screenshot of the view, to calibrate hit selection for the camera angle and box geometry that produced the unexpected result.

## Projection and Drag Mathematics

The tool does not map mouse X to model X or mouse Y to model Y. It reconstructs a world ray from the active `Viewpoint` camera position, quaternion rotation, focal distance, focal-plane extents, and projection mode. Candidate rays are self-checked through `View.ProjectPoint`; the ray picker is used only if the projected round trip is within 1.5 pixels. The hit tester intersects that ray with each oriented-box face plane, checks the world hit in face-local UV coordinates, converts the desired pixel tolerance into world units at the actual hit distance, and sorts valid candidates by true ray distance `t`. If host calibration does not verify, the implementation explicitly falls back to the retained 2D projected-polygon picker rather than applying an unverified ray.

For drag motion, the tool captures the picked face and a calibrated ray at mouse-down. Subsequent rays intersect a **fixed camera-parallel reference plane** through the initial hit; their displacement from the original hit is resolved along the fixed face normal and applied absolutely from the drag-start box state. This avoids accumulated-delta drift and does not suffer from world-origin magnitude. A literal re-intersection with the face plane would always yield zero normal displacement because both points lie on the same plane, so it is intentionally not used. Near head-on faces remain a geometric singularity and use the existing stable screen-up fallback with camera-scaled sensitivity.

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
│   ├── CameraRayBuilder.cs        # Calibrated perspective/orthographic rays
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
| Perspective and orthographic projection | **CONFIRMED** | Calibrated ray construction is verified by `View.ProjectPoint`; an explicit 2D fallback is used when that verification fails. |
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
| Basic box operations | Adopt a native Box section; Fit to Selection; direct drags of Min/Max X, Y, and Z; Escape cancellation. Verify that no model-wide box is created without a selection. |
| Camera | Perspective and orthographic views; front, side, top, arbitrary orbit; close and distant camera. Confirm the diagnostics show `picker=ray` and `calibration=valid`. |
| Geometry | Small, large, asymmetric, and arbitrarily rotated section boxes; faces partly outside the viewport; a face with a corner behind the camera. |
| Models | Single NWD, federated NWD, large coordinate offset, and selection across models. |
| Interaction | Hover, face mouse-down, live drag, mouse-up final exactness, Escape cancel, Shift, camera orbit to expose each box side, and navigation away from faces. |
| JSON compatibility | Create a native Box in the target Navisworks build, Refresh, drag every face, inspect `%AppData%\NavisworksSmartSectionBox\Logs` for rejection messages, and retain an anonymized `GetClippingPlanes()` sample for regression tests. |

## Troubleshooting

| Symptom | Action |
|---|---|
| The command does not appear | Confirm the DLL is in a Navisworks 2024 plug-in folder and that it was compiled against the matching Manage/Simulate 2024 API DLL. |
| Build fails resolving `Autodesk.Navisworks.Api.dll` | Set `NavisworksInstallDir` to the installed product folder. |
| First box creation fails | Select valid model elements, activate the tool, then inspect the add-in log if Navisworks rejects the verified `ClipPlaneSet`/`OrientedBox3D` payload. |
| A face does not capture | Confirm an element is selected or a native Navisworks Box section already exists, then click **Activate Smart Section Box**. Orbit the view until the intended box face is visible and camera-facing, then click well inside that face. |
| Navigation is blocked | Verify a mouse button was released. Press **Esc** to cancel the drag transaction. |
| UI and viewport differ | Reactivate the tool to adopt the current native box, then inspect the diagnostics log if clipping does not match the current section-box state. |
| The pane is clipped or controls overlap | Deploy the current DLL, delete the prior `SmartSectionBox.bundle`, then recreate the bundle from `Deployment/SmartSectionBox.bundle`. The revised pane has no sliders and uses a responsive host. |
| Activation reports no target | Select at least one model element, or create a native Navisworks Box section, then activate the tool again. |
| The wrong face is selected | Drags use the nearest valid camera-facing ray hit only. Orbit the view until the intended face is visible, then click well inside its center. Enable diagnostics and confirm `picker=ray` with `calibration=valid`; share those `FACE_DIAGNOSTIC` lines and a viewport screenshot if selection is still unexpected. |
| Diagnostics show `picker=fallback-2d` | The current camera state did not round-trip through `View.ProjectPoint` within the safety threshold. Share the `FACE_DIAGNOSTIC` lines and viewport screenshot; do not tune selection tolerance blindly. |
| A face pulls in the opposite direction | Capture diagnostics and record the `driver` value. Oblique ray drags use a fixed camera-reference plane; direct-on faces use the documented screen-up fallback. |

## References

[1]: https://blog.autodesk.io/navisworks-2024-sdk-is-posted/ "Navisworks 2024 SDK is posted — Autodesk Developer Blog"
[2]: https://aps.autodesk.com/developer/overview/navisworks-api "Navisworks API — Autodesk Platform Services"
[3]: https://www.linkedin.com/pulse/navisworks-api-sectioning-control-net-gavin-yang-li-fvyxc "Navisworks API Sectioning Control with .NET"
[4]: https://forums.autodesk.com/t5/navisworks-api-forum/enable-section-box-by-using-the-api/td-p/4966628 "Enable section box by using the API — Autodesk Community"
