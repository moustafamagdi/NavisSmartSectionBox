using System;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using SmartSectionBox.Core;
using SmartSectionBox.Infrastructure;
using SmartSectionBox.Plugin;

namespace SmartSectionBox.Interaction
{
    [Plugin("SmartSectionBox.SectionBoxTool", "MSSB", DisplayName = "Smart Section Box Tool")]
    public sealed class SectionBoxToolPlugin : ToolPlugin
    {
        private const ushort LeftMouseButton = 1;
        private const ushort EscapeVirtualKey = 0x1B;
        private const int HoverCaptureTolerancePixels = 14;
        private readonly CameraProjection projection = new CameraProjection();
        private readonly CameraRayBuilder rayBuilder = new CameraRayBuilder();
        private FaceHitTester hitTester;
        private DragController dragController;
        private FaceHitResult lastHoverHit;
        private int lastHoverX;
        private int lastHoverY;
        private bool ownsMouseSequence;

        public static event EventHandler<FaceHoverState> HoverChanged;

        private void EnsureController()
        {
            if (hitTester != null) return;
            hitTester = new FaceHitTester(projection, rayBuilder);
            dragController = new DragController(SmartSectionBoxRuntime.Service, projection, rayBuilder);
            dragController.LiveUpdates = SmartSectionBoxRuntime.LiveUpdates;
        }

        public override bool MouseMove(View view, KeyModifiers modifiers, int x, int y, double timeOffset)
        {
            try
            {
                EnsureController();
                dragController.LiveUpdates = SmartSectionBoxRuntime.LiveUpdates;
                if (dragController.State == DragState.Dragging) return ownsMouseSequence;

                var state = SmartSectionBoxRuntime.Service.GetCurrentBox();
                var probe = hitTester.Probe(state, view, x, y, 10.0);
                RememberHoverHit(probe.Selected, x, y);
                dragController.UpdateHover(probe.Selected);
                PublishHover(dragController.Hover);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("MouseMove failed in Smart Section Box tool.", ex);
                return false;
            }
        }

        public override bool MouseDrag(View view, KeyModifiers modifiers, int x, int y, double timeOffset)
        {
            try
            {
                EnsureController();
                if (dragController.State != DragState.Dragging) return ownsMouseSequence;
                var handled = dragController.Update(x, y, modifiers, view);
                PublishHover(dragController.Hover);
                view.RequestDelayedRedraw(ViewRedrawRequests.Render);
                return handled || ownsMouseSequence;
            }
            catch (Exception ex)
            {
                Logger.Error("MouseDrag failed in Smart Section Box tool.", ex);
                return ownsMouseSequence;
            }
        }

        public override bool MouseDown(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset)
        {
            try
            {
                EnsureController();
                if (button != LeftMouseButton) return false;
                var state = SmartSectionBoxRuntime.Service.GetCurrentBox();
                var probe = hitTester.Probe(state, view, x, y, 10.0);
                var hit = hitTester.SelectCandidate(probe);
                var captureSource = "fresh-probe";
                if (hit == null && TryGetRecentHoverHit(x, y, out hit))
                {
                    // ProjectPoint can change transiently between MouseMove and MouseDown. The
                    // cursor already received this exact face as pre-selection feedback, so
                    // reuse it inside a tiny screen window rather than falling through to the
                    // native section-box transform tool.
                    probe.Selected = hit;
                    probe.SelectedIndex = -1;
                    captureSource = "hover-cache";
                }

                var captured = dragController.Begin(hit, x, y, view);
                ownsMouseSequence = captured;
                InteractionDiagnostics.LogPointerDown(x, y, probe, state, captured, captureSource);
                if (captured)
                {
                    InteractionDiagnostics.LogDragBegin(x, y, hit, dragController.ScreenNormal, dragController.InitialCoordinate, dragController.UsesCalibratedRayDrag, dragController.DragCalibration);
                    PublishHover(dragController.Hover);
                    view.RequestDelayedRedraw(ViewRedrawRequests.Render);
                }
                return captured;
            }
            catch (Exception ex)
            {
                Logger.Error("MouseDown failed in Smart Section Box tool.", ex);
                return false;
            }
        }

        public override bool MouseUp(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset)
        {
            try
            {
                EnsureController();
                if (button != LeftMouseButton) return false;
                if (dragController.State != DragState.Dragging)
                {
                    var consumed = ownsMouseSequence;
                    ownsMouseSequence = false;
                    return consumed;
                }

                dragController.Update(x, y, modifiers, view);
                var face = dragController.DraggedFaceId;
                var initialCoordinate = dragController.InitialCoordinate;
                var finalCoordinate = dragController.WorkingCoordinate;
                var finalState = dragController.WorkingStateSnapshot;
                var committed = dragController.Commit();
                var sequenceOwned = ownsMouseSequence;
                ownsMouseSequence = false;
                InteractionDiagnostics.LogDragEnd(dragController.MouseStartX, dragController.MouseStartY, x, y, face, initialCoordinate, finalCoordinate, finalState, committed);
                PublishHover(FaceHoverState.None);
                view.RequestDelayedRedraw(ViewRedrawRequests.Render);
                return committed || sequenceOwned;
            }
            catch (Exception ex)
            {
                Logger.Error("MouseUp failed in Smart Section Box tool.", ex);
                var consumed = ownsMouseSequence;
                ownsMouseSequence = false;
                return consumed;
            }
        }

        public override bool KeyDown(View view, KeyModifiers modifier, ushort key, double timeOffset)
        {
            try
            {
                EnsureController();
                if (key != EscapeVirtualKey || dragController.State != DragState.Dragging) return false;
                var face = dragController.DraggedFaceId;
                var restoredCoordinate = dragController.InitialCoordinate;
                var restored = dragController.Cancel();
                ownsMouseSequence = false;
                InteractionDiagnostics.LogDragCancel(face, restoredCoordinate);
                PublishHover(FaceHoverState.None);
                view.RequestDelayedRedraw(ViewRedrawRequests.Render);
                return restored;
            }
            catch (Exception ex)
            {
                Logger.Error("Escape cancellation failed in Smart Section Box tool.", ex);
                return false;
            }
        }

        public override bool MouseLeave(View view, double timeOffset)
        {
            try
            {
                EnsureController();
                if (dragController.State == DragState.Dragging) return ownsMouseSequence;
                ownsMouseSequence = false;
                ClearHoverHit();
                dragController.Reset();
                PublishHover(FaceHoverState.None);
            }
            catch (Exception ex)
            {
                Logger.Error("MouseLeave failed in Smart Section Box tool.", ex);
            }
            return false;
        }

        public override Cursor GetCursor(View view, KeyModifiers modifier)
        {
            EnsureController();
            return CursorManager.GetCursor(dragController.Hover, dragController.State);
        }

        private void RememberHoverHit(FaceHitResult hit, int x, int y)
        {
            lastHoverHit = hit;
            lastHoverX = x;
            lastHoverY = y;
        }

        private bool TryGetRecentHoverHit(int x, int y, out FaceHitResult hit)
        {
            hit = null;
            if (lastHoverHit == null || lastHoverHit.Face == null) return false;
            if (Math.Abs(x - lastHoverX) > HoverCaptureTolerancePixels || Math.Abs(y - lastHoverY) > HoverCaptureTolerancePixels) return false;
            hit = lastHoverHit;
            return true;
        }

        private void ClearHoverHit()
        {
            lastHoverHit = null;
        }

        private static void PublishHover(FaceHoverState hover)
        {
            var handler = HoverChanged;
            if (handler != null) handler(null, hover ?? FaceHoverState.None);
        }
    }
}
