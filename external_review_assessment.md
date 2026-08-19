# Assessment of the External Hit-Testing Review

## Recommendation

The external review correctly identifies the principal limitation of the existing picker: it projects face corners, then performs 2D polygon inclusion against projections that may be unstable when a face is partly behind the camera, clipped by the frustum, or extremely thin in screen space. A world-space ray intersected with the six oriented box planes is the preferred replacement.

The Navisworks 2024 managed API provides enough documented camera state to make a calibrated implementation feasible. `Viewpoint.Position`, `Viewpoint.Rotation`, `Viewpoint.FocalDistance`, `Viewpoint.VerticalExtentAtFocalDistance`, `Viewpoint.HorizontalExtentAtFocalDistance`, and the perspective/orthographic projection enum are available. The camera rotation is documented as a quaternion that rotates the base camera orientation of forward `-Z`, right `+X`, and up `+Y`. The implementation should derive a world ray from that basis and use `View.ProjectPoint` as a round-trip calibration check at every relevant viewpoint change.

## Important corrections and safeguards

The external review’s general direction is sound, but two statements require care. First, a `double`-precision subtraction at civil coordinates around 2.4 million does not by itself make camera-facing classification unreliable; the current depth proxy is the more significant ordering problem. The new implementation will nevertheless translate all plane-intersection work into a local frame around the box center to minimise numerical error. Second, Navisworks does not expose the suggested `ViewDirection` and `UpDirection` properties directly through the managed API surface inspected so far. The camera basis must therefore be reconstructed from the documented `Rotation3D` quaternion rather than assumed to be directly available.

## Proposed implementation

1. Add a `CameraRay` utility that derives perspective and orthographic rays from `Viewpoint` position, quaternion rotation, focal distance, and focal-plane extents.
2. Add a calibration probe that projects points on candidate rays through `View.ProjectPoint`; if round-trip error exceeds a fixed small threshold, it records diagnostics and falls back to the present 2D picker rather than applying uncertain geometry.
3. Replace face projection hit testing with ray-versus-plane intersection followed by local face-coordinate bounds checks. Store real ray distance `t` for ordering.
4. Normal input selects the nearest valid front-facing face. Ctrl input selects the underlay candidates in ray-distance order; repeated Ctrl clicks at almost the same screen location cycle deterministically through that ordered list.
5. Replace screen-normal drag conversion with a fixed-plane ray intersection from drag start. Calculate each face offset from the initial hit point, not by accumulating per-frame pixel deltas.
6. Retain the existing diagnostics, adding camera-calibration state, ray `t`, local face coordinates, and candidate order so the next capture can prove behaviour.

## Acceptance criteria

The updated picker should capture visible portions of faces even when some face corners are outside the viewport, distinguish multiple underlay faces deterministically, remain stable in perspective and orthographic views, and avoid additional custom viewport rendering.

## Required host validation

The sandbox cannot run Navisworks. The camera-ray calibration and final picker must therefore be accepted in Navisworks 2024 using the existing opt-in interaction diagnostics and the supplied test matrix.
