using System;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;

namespace SmartSectionBox.Interaction
{
    public enum DragState
    {
        Idle,
        HoveringFace,
        Dragging,
        Cancelled
    }

    /// <summary>
    /// Holds one captured face throughout a drag. Once a calibrated world ray is available,
    /// every update is measured from the drag-start point on a fixed camera-parallel reference
    /// plane, never by accumulating individual pixel deltas.
    /// </summary>
    public sealed class DragController
    {
        private const double HeadOnAlignmentLimit = 0.92;

        private readonly SectionBoxService service;
        private readonly CameraProjection projection;
        private readonly CameraRayBuilder rayBuilder;
        private SectionBoxState initialState;
        private SectionBoxState workingState;
        private SectionBoxFace draggedFace;
        private int mouseStartX;
        private int mouseStartY;
        private ScreenPoint screenNormal;
        private bool rayDragActive;
        private Vector3 dragReferencePlanePoint;
        private Vector3 dragReferencePlaneNormal;
        private Vector3 dragStartPoint;
        private CameraRayCalibration dragCalibration;

        public DragController(SectionBoxService service, CameraProjection projection)
            : this(service, projection, new CameraRayBuilder())
        {
        }

        internal DragController(SectionBoxService service, CameraProjection projection, CameraRayBuilder rayBuilder)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
            this.rayBuilder = rayBuilder ?? throw new ArgumentNullException(nameof(rayBuilder));
        }

        public DragState State { get; private set; }
        public FaceHoverState Hover { get; private set; } = FaceHoverState.None;
        public bool LiveUpdates { get; set; } = true;
        public double ShiftMultiplier { get; set; } = 2.0;
        // Retained as a non-breaking public property for existing callers. Front-facing-only
        // selection does not use Ctrl during a captured drag.
        public double CtrlMultiplier { get; set; } = 0.25;
        public SectionBoxFaceId DraggedFaceId => draggedFace == null ? default(SectionBoxFaceId) : draggedFace.Id;
        public int MouseStartX => mouseStartX;
        public int MouseStartY => mouseStartY;
        public ScreenPoint ScreenNormal => screenNormal;
        public bool UsesCalibratedRayDrag => rayDragActive;
        public CameraRayCalibration DragCalibration => dragCalibration;
        public double InitialCoordinate => draggedFace == null || initialState == null ? 0 : initialState.GetFaceCoordinate(draggedFace.Id);
        public double WorkingCoordinate => draggedFace == null || workingState == null ? 0 : workingState.GetFaceCoordinate(draggedFace.Id);
        public SectionBoxState WorkingStateSnapshot => workingState == null ? null : workingState.Clone();

        public void UpdateHover(FaceHitResult hit)
        {
            if (State == DragState.Dragging) return;
            if (hit == null)
            {
                State = DragState.Idle;
                Hover = FaceHoverState.None;
                return;
            }

            State = DragState.HoveringFace;
            Hover = FaceHoverState.FromFace(hit.Face, service.GetCurrentBox()?.GetFaceCoordinate(hit.Face.Id) ?? 0);
        }

        public bool Begin(FaceHitResult hit, int x, int y, View view)
        {
            if (hit == null || hit.Face == null || view == null) return false;
            var state = service.GetCurrentBox();
            if (state == null || !state.Enabled) return false;

            initialState = state.Clone();
            workingState = state.Clone();
            draggedFace = hit.Face;
            mouseStartX = x;
            mouseStartY = y;
            screenNormal = projection.GetProjectedNormalDirection(draggedFace, view);
            rayDragActive = TryBeginRayDrag(hit, x, y, view);
            State = DragState.Dragging;
            Hover = FaceHoverState.FromFace(draggedFace, initialState.GetFaceCoordinate(draggedFace.Id));
            return true;
        }

        public bool Update(int x, int y, KeyModifiers modifiers, View view)
        {
            if (State != DragState.Dragging || draggedFace == null || workingState == null || view == null) return false;

            double outwardDistance;
            if (!TryGetRayOutwardDistance(x, y, modifiers, view, out outwardDistance))
            {
                outwardDistance = GetProjectedNormalOutwardDistance(x, y, modifiers, view);
            }

            workingState = initialState.Clone();
            SectionBoxMath.MoveFaceAlongOutwardNormal(workingState, draggedFace.Id, outwardDistance, service.MinimumBoxThickness);
            Hover = FaceHoverState.FromFace(draggedFace, workingState.GetFaceCoordinate(draggedFace.Id));
            if (LiveUpdates) service.SetBox(workingState);
            return true;
        }

        public bool Commit()
        {
            if (State != DragState.Dragging || workingState == null) return false;
            var applied = service.ApplyPending(workingState);
            Reset();
            return applied;
        }

        public bool Cancel()
        {
            if (State != DragState.Dragging || initialState == null) return false;
            State = DragState.Cancelled;
            var restored = service.ApplyPending(initialState);
            Reset();
            return restored;
        }

        public void Reset()
        {
            State = DragState.Idle;
            Hover = FaceHoverState.None;
            initialState = null;
            workingState = null;
            draggedFace = null;
            rayDragActive = false;
            dragCalibration = null;
        }

        private bool TryBeginRayDrag(FaceHitResult hit, int x, int y, View view)
        {
            dragCalibration = null;
            if (hit.PickerMode != "ray") return false;

            CameraRay ray;
            CameraRayCalibration calibration;
            if (!rayBuilder.TryCreateRay(view, x, y, out ray, out calibration))
            {
                dragCalibration = calibration;
                return false;
            }

            // Intersecting every drag ray with the *face plane* and then dotting two resulting
            // points with the face normal is a mathematical zero: both points lie on that plane.
            // The fixed reference plane must instead be camera-parallel and pass through the
            // initial face hit. It yields a non-accumulating cursor displacement that can be
            // resolved along the fixed face normal.
            var faceNormal = draggedFace.Normal.Normalized();
            if (Math.Abs(Vector3.Dot(faceNormal, ray.Forward)) >= HeadOnAlignmentLimit)
            {
                // A face viewed almost head-on has no reliable in-plane screen component along
                // its normal. Preserve the old explicit screen-up fallback for that singularity.
                dragCalibration = calibration;
                return false;
            }

            Vector3 startPoint;
            double distance;
            var referencePoint = hit.HitPoint;
            if (!ray.TryIntersectPlane(referencePoint, ray.Forward, out startPoint, out distance))
            {
                dragCalibration = calibration;
                return false;
            }

            dragReferencePlanePoint = referencePoint;
            dragReferencePlaneNormal = ray.Forward;
            dragStartPoint = startPoint;
            dragCalibration = calibration;
            return true;
        }

        private bool TryGetRayOutwardDistance(int x, int y, KeyModifiers modifiers, View view, out double outwardDistance)
        {
            outwardDistance = 0;
            if (!rayDragActive) return false;

            CameraRay ray;
            CameraRayCalibration calibration;
            if (!rayBuilder.TryCreateRay(view, x, y, out ray, out calibration))
            {
                // An unexpected calibration failure mid-drag must not make the face jump. The
                // caller can safely use the established fallback based on the same start point.
                rayDragActive = false;
                dragCalibration = calibration;
                return false;
            }

            Vector3 currentPoint;
            double distance;
            if (!ray.TryIntersectPlane(dragReferencePlanePoint, dragReferencePlaneNormal, out currentPoint, out distance)) return false;

            var multiplier = modifiers.HasFlag(KeyModifiers.Shift) ? ShiftMultiplier : 1.0;
            var signedWorldOffset = Vector3.Dot(currentPoint - dragStartPoint, draggedFace.Normal.Normalized()) * multiplier;
            outwardDistance = signedWorldOffset;
            dragCalibration = calibration;
            return true;
        }

        private double GetProjectedNormalOutwardDistance(int x, int y, KeyModifiers modifiers, View view)
        {
            var dx = x - mouseStartX;
            var dy = y - mouseStartY;
            var axisLength = Math.Sqrt(screenNormal.X * screenNormal.X + screenNormal.Y * screenNormal.Y);
            double signedPixels;
            if (axisLength >= 2.0)
            {
                signedPixels = (dx * screenNormal.X + dy * screenNormal.Y) / axisLength;
            }
            else
            {
                // Looking directly at a face makes its normal project to a point. Use a stable
                // screen-up fallback while preserving camera-scaled world sensitivity.
                signedPixels = -dy;
            }

            var multiplier = modifiers.HasFlag(KeyModifiers.Shift) ? ShiftMultiplier : 1.0;
            return projection.ScreenPixelsToWorldDistance(signedPixels * multiplier, draggedFace.Center, view);
        }
    }
}
