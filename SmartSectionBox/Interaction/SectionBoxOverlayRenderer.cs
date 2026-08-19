using System;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;

namespace SmartSectionBox.Interaction
{
    /// <summary>
    /// Lightweight Revit-style section-box outline. Navisworks remains the clipping engine;
    /// the wireframe gives users an unobtrusive, high-performance interaction target.
    /// </summary>
    internal sealed class SectionBoxOverlayRenderer
    {
        private static readonly Color WireColor = Color.FromByteRGB(110, 225, 155);
        private static readonly Color HoverColor = Color.FromByteRGB(65, 255, 135);
        private static readonly int[,] Edges =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        public void Render(View view, Graphics graphics, SectionBoxState state, FaceHoverState hover)
        {
            if (view == null || graphics == null || state == null || !state.Enabled) return;

            try
            {
                var corners = SectionBoxMath.GetCorners(state);
                graphics.DepthTest(false);
                graphics.DepthMask(false);
                graphics.Color(WireColor, 0.72);
                graphics.LineWidth(1.8);
                DrawEdges(graphics, corners, Edges);

                // Hover feedback remains an outline only—no opaque faces or colour-coded skins.
                if (hover != null && hover.IsHovering)
                {
                    var face = FindFace(state, hover.FaceId);
                    if (face != null)
                    {
                        graphics.Color(HoverColor, 0.95);
                        graphics.LineWidth(3.2);
                        DrawFaceOutline(graphics, face);
                    }
                }
            }
            finally
            {
                graphics.DepthMask(true);
                graphics.DepthTest(true);
            }
        }

        private static void DrawEdges(Graphics graphics, Vector3[] corners, int[,] edges)
        {
            for (var i = 0; i < edges.GetLength(0); i++)
            {
                graphics.Line(ToPoint(corners[edges[i, 0]]), ToPoint(corners[edges[i, 1]]));
            }
        }

        private static void DrawFaceOutline(Graphics graphics, SectionBoxFace face)
        {
            for (var i = 0; i < face.Corners.Length; i++)
            {
                graphics.Line(ToPoint(face.Corners[i]), ToPoint(face.Corners[(i + 1) % face.Corners.Length]));
            }
        }

        private static SectionBoxFace FindFace(SectionBoxState state, SectionBoxFaceId id)
        {
            foreach (var face in SectionBoxMath.GetFaces(state))
            {
                if (face.Id == id) return face;
            }
            return null;
        }

        private static Point3D ToPoint(Vector3 point)
        {
            return new Point3D(point.X, point.Y, point.Z);
        }
    }
}
