using System;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;

namespace SmartSectionBox.Interaction
{
    public struct ScreenPoint
    {
        public ScreenPoint(double x, double y, double depth)
        {
            X = x;
            Y = y;
            Depth = depth;
        }

        public double X { get; }
        public double Y { get; }
        public double Depth { get; }
    }

    /// <summary>
    /// Uses Navisworks' View.ProjectPoint for screen coordinates. The scale calculation is
    /// explicitly camera-dependent: perspective scales at the reference-face distance and
    /// orthographic uses the fixed visible height.
    /// </summary>
    public sealed class CameraProjection
    {
        public ScreenPoint? WorldToScreen(Vector3 worldPoint, View view)
        {
            if (view == null) return null;
            try
            {
                var projected = view.ProjectPoint(new Point3D(worldPoint.X, worldPoint.Y, worldPoint.Z), false, false);
                if (projected == null) return null;
                return new ScreenPoint(projected.X, projected.Y, projected.Depth);
            }
            catch
            {
                return null;
            }
        }

        public double ScreenPixelsToWorldDistance(double pixelDelta, Vector3 referencePoint, View view)
        {
            if (view == null || view.Height <= 0) return 0;
            var viewpoint = view.CreateViewpointCopy();
            if (viewpoint == null) return 0;

            var visibleHeight = viewpoint.VerticalExtentAtFocalDistance;
            if (viewpoint.Projection == ViewpointProjection.Perspective)
            {
                var focal = Math.Max(viewpoint.FocalDistance, 1e-9);
                var position = viewpoint.Position;
                var dx = position.X - referencePoint.X;
                var dy = position.Y - referencePoint.Y;
                var dz = position.Z - referencePoint.Z;
                var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                visibleHeight *= Math.Max(distance, 1e-9) / focal;
            }

            return pixelDelta * visibleHeight / view.Height;
        }

        public ScreenPoint GetProjectedNormalDirection(SectionBoxFace face, View view)
        {
            var centre = WorldToScreen(face.Center, view);
            var length = Math.Max(1.0, face.Corners[0].Length * 0.025);
            var endpoint = WorldToScreen(face.Center + face.Normal * length, view);
            if (!centre.HasValue || !endpoint.HasValue) return new ScreenPoint(0, 0, 0);
            return new ScreenPoint(endpoint.Value.X - centre.Value.X, endpoint.Value.Y - centre.Value.Y, 0);
        }
    }
}
