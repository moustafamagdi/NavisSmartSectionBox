# Navisworks 2024 API Verification Notes

This working note records the API evidence used to implement the add-in. The build workspace is Linux and does not contain a Navisworks installation, so it cannot capture a document-specific `GetClippingPlanes()` value. To avoid inventing a schema, the add-in persists the native JSON envelope returned by Navisworks and updates only the recognized box coordinate tokens.

| Area | Verified 2024 SDK evidence | Implementation decision |
|---|---|---|
| Runtime | Autodesk's Navisworks 2024 SDK is compiled for .NET Framework 4.8 and is intended for Visual Studio 2022. | The solution targets .NET Framework 4.8 and uses legacy non-SDK-style project format for Visual Studio/Navisworks compatibility. |
| Clipping | `View.GetClippingPlanes()` returns `string` described as a JSON `ClipPlaneSet`; `View.SetClippingPlanes(string)` and `View.TrySetClippingPlanes(string)` are available. | The sectioning adapter uses a validated JSON `ClipPlaneSet` envelope; `TrySetClippingPlanes` is the non-throwing hot-path call. |
| Input | `ToolPlugin.MouseDown(View, KeyModifiers, ushort, int, int, double)`, `MouseMove(View, KeyModifiers, int, int, double)`, `MouseDrag(View, KeyModifiers, int, int, double)`, and `MouseUp(...)` are documented. The return value determines whether the tool consumes an interaction. | The tool returns `true` only while it owns a face drag and returns `false` otherwise, preserving normal Navisworks navigation away from faces. |
| Cursor | `ToolPlugin.GetCursor(View, KeyModifiers)` returns the Navisworks `Cursor` type; the default is `Cursor.Unhandled`. | The tool returns resize cursors only during hover/drag and returns `Cursor.Unhandled` elsewhere. |
| Viewport and camera | `View.Width`, `View.Height`, `View.ProjectPoint(Point3D, bool, bool)`, `Viewpoint.Position`, quaternion `Viewpoint.Rotation`, `FocalDistance`, focal-plane extents, and `ViewpointProjection` are documented. `Rotation3D` exposes components as `A`, `B`, `C`, and `D`. | A world ray is reconstructed from the supported camera state, then accepted only when it round-trips through `ProjectPoint` within the calibration threshold. |
| Rendering | `ToolPlugin.OverlayRender(View, Graphics)` is documented as custom drawing over the main render. | No custom rendering is used; the native Navisworks section-box visualization remains responsible for visual feedback. |
| Dock pane | `DockPanePlugin` exposes `CreateControlPane`, `DestroyControlPane`, and `Visible`; Autodesk's WPF sample hosts a WPF `UserControl` through `WindowsFormsIntegration.ElementHost`. | The dock pane follows the supported ElementHost pattern. |

The official SDK also contains an input-and-render sample which activates a `ToolPlugin` via `Application.MainDocument.Tool.SetCustomToolPlugin(...)` and triggers overlays with `RequestDelayedRedraw(ViewRedrawRequests.Render)`.

> The official API reference describes the clipping payload only as a JSON `ClipPlaneSet`; it does not publish the field-level box schema. Consequently, first runtime activation logs and retains the native document payload for inspection, and all writes are passed through `TrySetClippingPlanes` for validation.

## Sources

[1]: https://blog.autodesk.io/navisworks-2024-sdk-is-posted/ "Navisworks 2024 SDK is posted — Autodesk Developer Blog"
[2]: https://aps.autodesk.com/developer/overview/navisworks-api "Navisworks API — Autodesk Platform Services"
[3]: https://www.linkedin.com/pulse/navisworks-api-sectioning-control-net-gavin-yang-li-fvyxc "Navisworks API Sectioning Control with .NET"
[4]: https://forums.autodesk.com/t5/navisworks-api-forum/enable-section-box-by-using-the-api/td-p/4966628 "Enable section box by using the API — Autodesk Community"

The extracted official 2024 SDK is retained locally under `/home/ubuntu/navisworks-sdk-2024` for reproducibility and was used to verify the exact API signatures listed above.

## Visual inspection note

The published box-mode example visibly confirms a root `Type` of `ClipPlaneSet`, a nested box object, and min/max coordinate arrays. The screenshot is not sufficiently legible to treat every field name or rotation representation as authoritative. The implementation therefore uses field discovery against `GetClippingPlanes()` and validates every write through `TrySetClippingPlanes` instead of copying an unverified image transcription.

## Bundle discovery correction

Autodesk’s current Navisworks publisher guidance confirms that Manage and Simulate discover local applications from an Autodesk ApplicationPlugins `.bundle` containing `PackageContents.xml` and `Contents`. Its 2025 forum example further shows a working Navisworks managed-plugin manifest using `AppType="ManagedPlugin"`, `Platform="NAVMAN|NAVSIM"`, and Navisworks series codes (2024 is `Nw21`; 2025 is `Nw22`). The earlier generic manifest recommendation did not use this Navisworks-specific schema and should not be used.

[5]: https://aps.autodesk.com/marketplace/publisher-center/navisworks-publisher-guidelines "Navisworks publisher guidelines — Autodesk Platform Services"
[6]: https://forums.autodesk.com/t5/navisworks-api-forum/how-to-deploy-a-plug-in-for-navis-2025/td-p/13694530 "How to deploy a plug in for Navis 2025? — Autodesk Community"

## Native box payload implementation correction

The current sectioning reference confirms that Navisworks box mode is controlled by a `jsonClipPlaneSet` whose box location is represented directly by `Max` and `Min` points, while the top-level `Enabled` key activates sectioning. Plane mode instead uses a `Planes` array. The implementation must therefore recognize and emit direct `Min`/`Max` box members rather than relying on the unverified fallback `OrientedBox`/`Box` envelope. The actual host payload should still be captured through `GetClippingPlanes()` when possible and retained as the template.

[7]: https://www.linkedin.com/pulse/navisworks-api-sectioning-control-net-gavin-yang-li-fvyxc "Navisworks API Sectioning Control with .NET"

## Exact box JSON schema observed

The full-resolution box-mode illustration shows the concrete payload shape used by Navisworks: root `{ "Type": "ClipPlaneSet", "Version": 1, "OrientedBox": { "Type": "OrientedBox3D", "Version": 1, "Box": [[minX,minY,minZ],[maxX,maxY,maxZ]], "Rotation": [x,y,z] }, "Enable": true }`. The root activation property is **`Enable`** (not `Enabled`), and both the root and `OrientedBox` include `Type` and `Version`. The fallback encoder must emit this schema exactly.
