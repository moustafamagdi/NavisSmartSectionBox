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
            if (face == null || face.Corners == null || face.Corners.Length < 3) return new ScreenPoint(0, 0, 0);
            var centre = WorldToScreen(face.Center, view);
            if (!centre.HasValue) return new ScreenPoint(0, 0, 0);

            // The previous calculation used the distance from the world origin, which can be
            // millions of units in civil models. That projects past the camera and can invert
            // the apparent pull direction. Use local face dimensions, then adaptively widen the
            // finite projection step when a near edge-on X/Y normal produces only a few pixels.
            var edgeA = (face.Corners[1] - face.Corners[0]).Length;
            var edgeB = (face.Corners[2] - face.Corners[1]).Length;
            var localScale = Math.Max(0.001, Math.Min(edgeA, edgeB));
            var normal = face.Normal.Normalized();
            var best = new ScreenPoint(0, 0, 0);
            var bestLength = 0.0;
            var reference = new ScreenPoint(0, 0, 0);
            var referenceLength = 0.0;

            foreach (var multiplier in new[] { 0.15, 0.35, 0.75, 1.5 })
            {
                var length = Math.Max(0.001, localScale * multiplier);
                var endpoint = WorldToScreen(face.Center + normal * length, view);
                if (!endpoint.HasValue) continue;
                var candidate = new ScreenPoint(endpoint.Value.X - centre.Value.X, endpoint.Value.Y - centre.Value.Y, 0);
                var candidateLength = Math.Sqrt(candidate.X * candidate.X + candidate.Y * candidate.Y);
                if (candidateLength < 1e-9) continue;

                // The shortest usable vector establishes direction. Longer samples must agree
                // with it; this avoids adopting a numerically unstable direction when a sample
                // crosses a projection singularity.
                if (referenceLength < 1e-9)
                {
                    reference = candidate;
                    referenceLength = candidateLength;
                }
                else if (candidate.X * reference.X + candidate.Y * reference.Y <= 0)
                {
                    continue;
                }

                if (candidateLength > bestLength)
                {
                    best = candidate;
                    bestLength = candidateLength;
                }
            }

            return best;
        }
    }
}
