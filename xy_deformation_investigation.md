# X/Y Section-Box Deformation Investigation

## Reported behavior

The user reports that dragging native section-box faces on **X** or **Y** visibly changes the box incorrectly, while **Z** behaves as expected. The issue persists after simplifying face selection to camera-facing faces, so it is independent of Ctrl/underlay selection.

## Native payload evidence

Navisworks Box mode uses `ClipPlaneSet` with an `OrientedBox3D` containing `Box` Min/Max coordinates and a `Rotation` payload. Public examples confirm that changing Min/Max controls the box faces, while the .NET API exposes this only through clipping JSON rather than a strongly typed section-box editor. [1] [2]

## Root cause in the current drag mapping

The drag controller resolves the pointer displacement along a rotated world-space face normal, but it previously applied that distance by changing only one raw Min/Max component. For a rotated oriented box, that moves the raw box centre on an unrotated global axis instead of along the face normal. The effect is most visible on X/Y when the box has plan rotation; Z can appear correct because its normal often remains aligned with world Z.

## Correct update model

For an outward face displacement `d` along the captured world-space face normal `n`:

- Shift the raw box centre by `n × d / 2`.
- Increase/decrease the captured local half-extent by `d / 2`.
- Rebuild Min/Max around the shifted centre while preserving rotation and the other half-extents.

This keeps the opposite oriented face stationary and moves only the captured face in rendered world space. When rotation is zero, the model reduces exactly to changing only the selected raw Min or Max coordinate.

## References

[1]: https://forums.autodesk.com/t5/navisworks-api-forum/enable-section-box-by-using-the-api/td-p/4966628 "Autodesk Community — Enable section box by using the API"
[2]: https://www.linkedin.com/pulse/navisworks-api-sectioning-control-net-gavin-yang-li-fvyxc "Navisworks API Sectioning Control with .NET"
