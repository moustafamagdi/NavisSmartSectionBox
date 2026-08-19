using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;

namespace SmartSectionBox.Interaction
{
    public sealed class FaceHitResult
    {
        public SectionBoxFace Face { get; set; }
        public ScreenPoint[] Polygon { get; set; }
        public double AverageDepth { get; set; }
        public double DistanceToPolygon { get; set; }
        public bool IsInsidePolygon { get; set; }
    }

    public sealed class FaceHitProbe
    {
        public IReadOnlyList<FaceHitResult> Candidates { get; set; }
        public FaceHitResult Selected { get; set; }
        public int SelectedIndex { get; set; } = -1;
    }

    public sealed class FaceHitTester
    {
        private readonly CameraProjection projection;

        public FaceHitTester(CameraProjection projection)
        {
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }

        public FaceHitResult HitTest(SectionBoxState state, View view, int mouseX, int mouseY, double edgeTolerancePixels = 10.0)
        {
            return Probe(state, view, mouseX, mouseY, edgeTolerancePixels).Selected;
        }

        public FaceHitResult SelectCandidate(FaceHitProbe probe, bool requestUnderlay, int mouseX, int mouseY)
        {
            if (probe == null || probe.Candidates == null || probe.Candidates.Count == 0) return null;
            var index = 0;
            if (requestUnderlay && probe.Candidates.Count > 1)
            {
                // Ctrl selects the next face behind the default candidate. Repeated Ctrl-clicks
                // at nearly the same screen position cycle predictably through every overlap.
                var nearestExisting = LastSelectionIndex(probe.Candidates, mouseX, mouseY);
                index = nearestExisting < 0 ? 1 : (nearestExisting + 1) % probe.Candidates.Count;
            }

            probe.SelectedIndex = index;
            probe.Selected = probe.Candidates[index];
            RememberSelection(probe.Selected, mouseX, mouseY, index);
            return probe.Selected;
        }

        public FaceHitProbe Probe(SectionBoxState state, View view, int mouseX, int mouseY, double edgeTolerancePixels = 10.0)
        {
            var probe = new FaceHitProbe { Candidates = new List<FaceHitResult>() };
            if (state == null || !state.Enabled || view == null) return probe;

            var candidates = new List<FaceHitResult>();
            foreach (var face in SectionBoxMath.GetFaces(state))
            {
                var polygon = ProjectFace(face, view);
                if (polygon == null) continue;

                var point = new ScreenPoint(mouseX, mouseY, 0);
                var inside = PointInPolygon(point, polygon);
                var distance = inside ? 0 : DistanceToPolygon(point, polygon);
                if (!inside && distance > edgeTolerancePixels) continue;

                candidates.Add(new FaceHitResult
                {
                    Face = face,
                    Polygon = polygon,
                    AverageDepth = polygon.Average(p => p.Depth),
                    DistanceToPolygon = distance,
                    IsInsidePolygon = inside
                });
            }

            probe.Candidates = candidates
                .OrderBy(c => c.DistanceToPolygon)
                .ThenBy(c => c.AverageDepth)
                .ThenBy(c => c.Face.Id)
                .ToList();
            probe.Selected = probe.Candidates.FirstOrDefault();
            probe.SelectedIndex = probe.Selected == null ? -1 : 0;
            return probe;
        }

        private int lastMouseX = int.MinValue;
        private int lastMouseY = int.MinValue;
        private int lastCandidateIndex = -1;
        private SectionBoxFaceId lastFaceId;

        private int LastSelectionIndex(IReadOnlyList<FaceHitResult> candidates, int mouseX, int mouseY)
        {
            var withinCycleRadius = Math.Abs(mouseX - lastMouseX) <= 12 && Math.Abs(mouseY - lastMouseY) <= 12;
            if (!withinCycleRadius) return -1;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Face.Id == lastFaceId) return i;
            }
            return lastCandidateIndex >= 0 && lastCandidateIndex < candidates.Count ? lastCandidateIndex : -1;
        }

        private void RememberSelection(FaceHitResult selection, int mouseX, int mouseY, int index)
        {
            lastMouseX = mouseX;
            lastMouseY = mouseY;
            lastCandidateIndex = index;
            lastFaceId = selection.Face.Id;
        }

        private ScreenPoint[] ProjectFace(SectionBoxFace face, View view)
        {
            var projected = new ScreenPoint[face.Corners.Length];
            for (var i = 0; i < face.Corners.Length; i++)
            {
                var point = projection.WorldToScreen(face.Corners[i], view);
                if (!point.HasValue) return null;
                projected[i] = point.Value;
            }
            return projected;
        }

        private static bool PointInPolygon(ScreenPoint point, IReadOnlyList<ScreenPoint> polygon)
        {
            var inside = false;
            for (var i = 0; i < polygon.Count; i++)
            {
                var j = (i + polygon.Count - 1) % polygon.Count;
                var a = polygon[i];
                var b = polygon[j];
                var crosses = ((a.Y > point.Y) != (b.Y > point.Y)) &&
                              (point.X < (b.X - a.X) * (point.Y - a.Y) / Math.Max(b.Y - a.Y, 1e-12) + a.X);
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static double DistanceToPolygon(ScreenPoint point, IReadOnlyList<ScreenPoint> polygon)
        {
            var distance = double.MaxValue;
            for (var i = 0; i < polygon.Count; i++)
            {
                distance = Math.Min(distance, DistanceToSegment(point, polygon[i], polygon[(i + 1) % polygon.Count]));
            }
            return distance;
        }

        private static double DistanceToSegment(ScreenPoint point, ScreenPoint a, ScreenPoint b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 1e-12) return Distance(point, a);
            var t = Math.Max(0, Math.Min(1, ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lengthSquared));
            return Math.Sqrt(Math.Pow(point.X - (a.X + t * dx), 2) + Math.Pow(point.Y - (a.Y + t * dy), 2));
        }

        private static double Distance(ScreenPoint a, ScreenPoint b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
