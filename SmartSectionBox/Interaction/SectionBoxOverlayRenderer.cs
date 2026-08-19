using System;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;

namespace SmartSectionBox.Interaction
{
    /// <summary>
    /// Draws the interactive section-box skin in the ToolPlugin render pass. Navisworks still
    /// performs the actual clipping; this class is the sole user-facing visual representation.
    /// </summary>
    internal sealed class SectionBoxOverlayRenderer
    {
        private static readonly Color FrontColor = Color.FromByteRGB(220, 48, 48);
        private static readonly Color UnderlayColor = Color.FromByteRGB(245, 196, 28);
        private static readonly Color HoverColor = Color.FromByteRGB(40, 150, 255);

        public void Render(View view, Graphics graphics, SectionBoxState state, FaceHoverState hover)
        {
            if (view == null || graphics == null || state == null || !state.Enabled) return;

            var viewpoint = view.CreateViewpointCopy();
            if (viewpoint == null) return;
            var cameraPosition = new Vector3(viewpoint.Position.X, viewpoint.Position.Y, viewpoint.Position.Z);

            try
            {
                // Render the controlled skin independently of the native Move tool's blue box.
                // The false depth test makes all six faces legible while front/underlay colour
                // communicates the selection policy unambiguously.
                graphics.DepthTest(false);
                graphics.DepthMask(false);

                foreach (var face in SectionBoxMath.GetFaces(state))
                {
                    var isFrontFacing = Vector3.Dot(face.Normal, cameraPosition - face.Center) >= 0;
                    var isHover = hover != null && hover.IsHovering && hover.FaceId == face.Id;
                    var color = isHover ? HoverColor : (isFrontFacing ? FrontColor : UnderlayColor);
                    var opacity = isHover ? 0.36 : (isFrontFacing ? 0.22 : 0.14);

                    graphics.Color(color, opacity);
                    DrawFill(graphics, face);
                    graphics.Color(color, 1.0);
                    graphics.LineWidth(isHover ? 4.0 : (isFrontFacing ? 2.5 : 1.5));
                    DrawOutline(graphics, face);
                }
            }
            finally
            {
                graphics.DepthMask(true);
                graphics.DepthTest(true);
            }
        }

        private static void DrawFill(Graphics graphics, SectionBoxFace face)
        {
            var a = ToPoint(face.Corners[0]);
            var b = ToPoint(face.Corners[1]);
            var c = ToPoint(face.Corners[2]);
            var d = ToPoint(face.Corners[3]);
            graphics.Triangle(a, b, c, true);
            graphics.Triangle(a, c, d, true);
        }

        private static void DrawOutline(Graphics graphics, SectionBoxFace face)
        {
            for (var i = 0; i < face.Corners.Length; i++)
            {
                graphics.Line(ToPoint(face.Corners[i]), ToPoint(face.Corners[(i + 1) % face.Corners.Length]));
            }
        }

        private static Point3D ToPoint(Vector3 point)
        {
            return new Point3D(point.X, point.Y, point.Z);
        }
    }
}
