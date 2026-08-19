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

    public sealed class DragController
    {
        private readonly SectionBoxService service;
        private readonly CameraProjection projection;
        private SectionBoxState initialState;
        private SectionBoxState workingState;
        private SectionBoxFace draggedFace;
        private int mouseStartX;
        private int mouseStartY;
        private ScreenPoint screenNormal;

        public DragController(SectionBoxService service, CameraProjection projection)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }

        public DragState State { get; private set; }
        public FaceHoverState Hover { get; private set; } = FaceHoverState.None;
        public bool LiveUpdates { get; set; } = true;
        public double ShiftMultiplier { get; set; } = 2.0;
        public double CtrlMultiplier { get; set; } = 0.25;
        public SectionBoxFaceId DraggedFaceId => draggedFace == null ? default(SectionBoxFaceId) : draggedFace.Id;
        public int MouseStartX => mouseStartX;
        public int MouseStartY => mouseStartY;
        public ScreenPoint ScreenNormal => screenNormal;
        public double InitialCoordinate => draggedFace == null || initialState == null ? 0 : initialState.GetFaceCoordinate(draggedFace.Id);
        public double WorkingCoordinate => draggedFace == null || workingState == null ? 0 : workingState.GetFaceCoordinate(draggedFace.Id);

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
            if (hit == null || view == null) return false;
            var state = service.GetCurrentBox();
            if (state == null || !state.Enabled) return false;

            initialState = state.Clone();
            workingState = state.Clone();
            draggedFace = hit.Face;
            mouseStartX = x;
            mouseStartY = y;
            screenNormal = projection.GetProjectedNormalDirection(draggedFace, view);
            State = DragState.Dragging;
            Hover = FaceHoverState.FromFace(draggedFace, initialState.GetFaceCoordinate(draggedFace.Id));
            return true;
        }

        public bool Update(int x, int y, KeyModifiers modifiers, View view)
        {
            if (State != DragState.Dragging || draggedFace == null || workingState == null || view == null) return false;

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

            var multiplier = modifiers.HasFlag(KeyModifiers.Shift) ? ShiftMultiplier :
                             modifiers.HasFlag(KeyModifiers.Ctrl) ? CtrlMultiplier : 1.0;
            var worldDistance = projection.ScreenPixelsToWorldDistance(signedPixels * multiplier, draggedFace.Center, view);
            var worldMovement = draggedFace.Normal * worldDistance;
            var localMovement = SectionBoxMath.InverseRotateLocal(worldMovement, initialState);
            var coordinateDelta = AxisComponent(localMovement, draggedFace.Axis);
            var updatedCoordinate = initialState.GetFaceCoordinate(draggedFace.Id) + coordinateDelta;

            workingState = initialState.Clone();
            workingState.SetFaceCoordinate(draggedFace.Id, updatedCoordinate, service.MinimumBoxThickness);
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
        }

        private static double AxisComponent(Vector3 vector, SectionBoxAxis axis)
        {
            switch (axis)
            {
                case SectionBoxAxis.X: return vector.X;
                case SectionBoxAxis.Y: return vector.Y;
                case SectionBoxAxis.Z: return vector.Z;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }
    }
}
