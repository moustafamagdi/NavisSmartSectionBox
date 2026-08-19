# External Code Review Prompt — Navisworks 2024 Section-Box Face Picking

I need a senior C# / Autodesk Navisworks API review of a custom add-in that moves individual faces of a Navisworks Box section.

## Objective

Improve **hit reliability**. The current tool frequently misses clicks that the user expects to capture, especially around projected face boundaries, narrow projected faces, and overlapping faces. The add-in must remain lightweight: custom box rendering has been removed because it harmed performance on a large federated model.

## Host and constraints

- **Host:** Autodesk Navisworks Manage 2024, managed .NET API, .NET Framework 4.8.
- **Current native clipping API:** `View.GetClippingPlanes()` and `View.TrySetClippingPlanes(jsonClipPlaneSet)` using a `ClipPlaneSet` / `OrientedBox3D` JSON payload.
- **Current interaction API:** a `ToolPlugin` receives `MouseMove`, `MouseDown`, `MouseDrag`, and `MouseUp`; it has `View.ProjectPoint(Point3D, sectionClip, frustumClip)` for world-to-screen projection.
- **No custom viewport renderer:** do not solve this by drawing large pick surfaces or maintaining a high-frequency overlay.
- **Maintain native clipping:** Navisworks remains the clipping engine. Do not replace clipping with a custom renderer.
- **Box geometry:** an axis-aligned or rotated OBB with six faces. The box can use very large civil coordinates (for example X ≈ 2,441,000 and Y ≈ 9,075,000 model units).
- **Expected UX:** normal click chooses a camera-facing face. Holding **Ctrl** should choose from the back/underlay face set when faces overlap.

## Current algorithm

1. Build the six transformed box faces from the current box state.
2. Project all four corners of each face to screen coordinates with `View.ProjectPoint`.
3. Classify each face as front or back using `dot(faceNormal, cameraPosition - faceCenter)`.
4. For normal input, test only front faces. For Ctrl input, test only back faces.
5. Use 2D point-in-convex-polygon plus a fixed 10-pixel edge tolerance.
6. Sort accepted candidates by: inside polygon first, then distance to polygon, then average projected depth.
7. Use the first candidate as the dragged face.
8. Convert mouse motion to a face-coordinate change using the projected face normal.

## Evidence from a real diagnostic capture

The log records `POINTER_DOWN`, candidates, projected 2D polygons, depth, `inside`, edge distance, selected face, and drag results.

- Most intentional clicks are captured, but some user clicks return `candidateCount=0` and `captured=False`.
- Example miss:

```text
POINTER_DOWN screen=(296,757) captured=False selected=none selectionSet=front candidateCount=0
box=min=(2441026.352,9075884.17,1914.232) max=(2441231.675,9075962.517,1968.007)
```

- Some accepted clicks are only near a projected edge, not inside a polygon; for example a 4.031-pixel distance is accepted through the current tolerance.
- Ctrl input can produce multiple valid underlay candidates at the same screen location. Example:

```text
POINTER_DOWN screen=(182,549) captured=True selected=MaxY selectionSet=underlay candidateCount=3
candidates=[MaxY inside=True depth=0.369 | MinZ inside=True depth=0.409 | MinX inside=True depth=0.8]
```

- Projected depth values may be negative or positive, depending on the view and clipping state. The selection ordering must be robust and must not assume a simplistic depth sign convention.
- The box can become visually thin in screen space after the user moves faces, so a fixed edge tolerance alone may be inadequate.

## What I need from you

Please provide an evidence-based, production-quality recommendation and C#-oriented implementation plan.

1. **Root-cause analysis:** Identify likely reasons that a projected-polygon picker misses expected clicks, including faces partly outside the viewport, near-plane/frustum clipping, projection winding, self/edge overlap, screen-space tolerance, and invalid/ambiguous projected depth.
2. **Preferred picking algorithm:** Recommend the most reliable algorithm supported by Navisworks 2024. In particular, assess whether the correct solution is screen-ray versus oriented bounding-box face intersection, and explain how to obtain or reconstruct a world ray if the managed API exposes only `ProjectPoint`.
3. **Fallback algorithm:** If a true screen ray cannot be obtained through supported Navisworks APIs, propose a robust 2D algorithm that improves on the current point-in-polygon approach. Include clipping of projected polygons to the viewport, tolerance scaling, closest-point logic, and deterministic candidate ordering.
4. **Ctrl underlay behavior:** Define a deterministic policy for normal front-face selection and Ctrl back-face selection when multiple faces overlap. Explain when to pick nearest, farthest, or cycle candidates, and how to make the result understandable without a visual overlay.
5. **Drag mathematics:** Review how to calculate signed face movement from mouse motion robustly for perspective and orthographic views, without errors caused by large global model coordinates.
6. **Code-level guidance:** Provide pseudocode or C# for the proposed picker and drag conversion. State any Navisworks managed or COM APIs needed, and clearly distinguish supported APIs from unsupported UI automation or private APIs.
7. **Test plan:** Propose deterministic tests for perspective/orthographic cameras, rotated boxes, faces partly off-screen, thin faces, viewport edges, overlapping front faces, Ctrl underlay picks, and large coordinate values.

## Non-goals

- Do not redesign the complete add-in or introduce custom model rendering.
- Do not rely on simulated mouse/ribbon clicks, private Navisworks APIs, or fragile UI automation.
- Do not assume Revit APIs are available.

## Requested response format

Please return:

1. A concise diagnosis.
2. The recommended algorithm and why it is superior.
3. C#-level implementation guidance.
4. A fallback if the preferred Navisworks API capability is unavailable.
5. A focused test matrix.

I can provide the full diagnostic log and the relevant current C# classes (`FaceHitTester`, `CameraProjection`, `DragController`, `SectionBoxMath`, and `SectionBoxToolPlugin`) if needed.
