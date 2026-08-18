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
        private readonly CameraProjection projection = new CameraProjection();
        private FaceHitTester hitTester;
        private DragController dragController;

        public static event EventHandler<FaceHoverState> HoverChanged;

        private void EnsureController()
        {
            if (hitTester != null) return;
            hitTester = new FaceHitTester(projection);
            dragController = new DragController(SmartSectionBoxRuntime.Service, projection);
            dragController.LiveUpdates = SmartSectionBoxRuntime.LiveUpdates;
        }

        public override bool MouseMove(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset)
        {
            try
            {
                EnsureController();
                dragController.LiveUpdates = SmartSectionBoxRuntime.LiveUpdates;
                if (dragController.State == DragState.Dragging)
                {
                    var handled = dragController.Update(x, y, modifiers, view);
                    PublishHover(dragController.Hover);
                    view.RequestDelayedRedraw(ViewRedrawRequests.Render);
                    return handled;
                }

                var state = SmartSectionBoxRuntime.Service.GetCurrentBox();
                var hit = hitTester.HitTest(state, view, x, y);
                dragController.UpdateHover(hit);
                PublishHover(dragController.Hover);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("MouseMove failed in Smart Section Box tool.", ex);
                return false;
            }
        }

        public override bool MouseDown(View view, KeyModifiers modifiers, ushort button, int x, int y, double timeOffset)
        {
            try
            {
                EnsureController();
                if (button != LeftMouseButton) return false;
                var state = SmartSectionBoxRuntime.Service.GetCurrentBox();
                var hit = hitTester.HitTest(state, view, x, y);
                var captured = dragController.Begin(hit, x, y, view);
                if (captured)
                {
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
                if (dragController.State != DragState.Dragging || button != LeftMouseButton) return false;
                dragController.Update(x, y, modifiers, view);
                var committed = dragController.Commit();
                PublishHover(FaceHoverState.None);
                view.RequestDelayedRedraw(ViewRedrawRequests.Render);
                return committed;
            }
            catch (Exception ex)
            {
                Logger.Error("MouseUp failed in Smart Section Box tool.", ex);
                return false;
            }
        }

        public override bool KeyDown(View view, KeyModifiers modifier, ushort key, double timeOffset)
        {
            try
            {
                EnsureController();
                if (key != EscapeVirtualKey || dragController.State != DragState.Dragging) return false;
                var restored = dragController.Cancel();
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
                if (dragController.State == DragState.Dragging) return false;
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

        public override void OverlayRender(View view, Graphics graphics)
        {
            // OverlayRender is intentionally reserved for a future supported face outline.
            // Cursor and dock-pane feedback are the dependable feedback channel; no window
            // overlay or unsupported HWND manipulation is used.
        }

        private static void PublishHover(FaceHoverState hover)
        {
            var handler = HoverChanged;
            if (handler != null) handler(null, hover ?? FaceHoverState.None);
        }
    }
}
