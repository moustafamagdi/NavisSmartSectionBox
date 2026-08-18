using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;

namespace SmartSectionBox.Interaction
{
    /// <summary>
    /// Navisworks' public Cursor enum exposes Handled, HyperHand and Unhandled rather than
    /// axis-specific resize glyphs. We return Handled only for an interactive face and leave
    /// every other cursor to Navisworks via Unhandled.
    /// </summary>
    internal static class CursorManager
    {
        public static Cursor GetCursor(FaceHoverState hover, DragState dragState)
        {
            if (dragState == DragState.Dragging || (hover != null && hover.IsHovering))
            {
                return Cursor.Handled;
            }
            return Cursor.Unhandled;
        }
    }
}
