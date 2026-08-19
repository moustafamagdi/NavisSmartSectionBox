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
        // Populated only by the calibration-failure fallback. Kept for current diagnostics and
        // backwards-compatible callers.
        public ScreenPoint[] Polygon { get; set; }
        public double AverageDepth { get; set; }
        public double DistanceToPolygon { get; set; }
        public bool IsInsidePolygon { get; set; }
        public bool IsFrontFacing { get; set; }
        public double RayDistance { get; set; }
        public Vector3 HitPoint { get; set; }
        public double FaceU { get; set; }
        public double FaceV { get; set; }
        public double WorldTolerance { get; set; }
        public string PickerMode { get; set; }
    }

    public sealed class FaceHitProbe
    {
        public IReadOnlyList<FaceHitResult> Candidates { get; set; }
        public FaceHitResult Selected { get; set; }
        public int SelectedIndex { get; set; } = -1;
        public bool UsedRayPicker { get; set; }
        public CameraRayCalibration Calibration { get; set; }
    }

    /// <summary>
    /// Selects section-box faces with a calibrated world ray. The ray branch never projects box
    /// corners, so face corners behind the camera or outside the frustum cannot corrupt picking.
    /// The retained projected-polygon implementation is invoked only when host camera calibration
    /// deliberately refuses to validate the ray model.
    /// </summary>
    public sealed class FaceHitTester
    {
        private const double MinimumRayDenominator = 1e-9;

        private readonly CameraProjection projection;
        private readonly CameraRayBuilder rayBuilder;

        public FaceHitTester(CameraProjection projection)
            : this(projection, new CameraRayBuilder())
        {
        }

        internal FaceHitTester(CameraProjection projection, CameraRayBuilder rayBuilder)
        {
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
            this.rayBuilder = rayBuilder ?? throw new ArgumentNullException(nameof(rayBuilder));
        }

        public FaceHitResult HitTest(SectionBoxState state, View view, int mouseX, int mouseY, double edgeTolerancePixels = 10.0)
        {
            return Probe(state, view, mouseX, mouseY, edgeTolerancePixels).Selected;
        }

        public FaceHitResult SelectCandidate(FaceHitProbe probe)
        {
            if (probe == null || probe.Candidates == null || probe.Candidates.Count == 0) return null;
            probe.SelectedIndex = 0;
            probe.Selected = probe.Candidates[0];
            return probe.Selected;
        }

        public FaceHitProbe Probe(SectionBoxState state, View view, int mouseX, int mouseY, double edgeTolerancePixels = 10.0)
        {
            var empty = new FaceHitProbe
            {
                Candidates = new List<FaceHitResult>(),
                UsedRayPicker = false,
                Calibration = CameraRayCalibration.Invalid("not-attempted")
            };
            if (state == null || !state.Enabled || view == null) return empty;

            CameraRay ray;
            CameraRayCalibration calibration;
            if (rayBuilder.TryCreateRay(view, mouseX, mouseY, out ray, out calibration))
            {
                return ProbeRay(state, ray, edgeTolerancePixels, calibration);
            }

            // The fallback is intentionally not a secondary picker that competes with a working
            // ray. It is an explicit safe mode when the current Navisworks host/viewpoint does
            // not round-trip the documented camera fields through ProjectPoint.
            var fallback = ProbeProjectedPolygon(state, view, mouseX, mouseY, edgeTolerancePixels);
            fallback.Calibration = calibration;
            return fallback;
        }

        private static FaceHitProbe ProbeRay(
            SectionBoxState state,
            CameraRay ray,
            double edgeTolerancePixels,
            CameraRayCalibration calibration)
        {
            var candidates = new List<FaceHitResult>();
            foreach (var face in SectionBoxMath.GetFaces(state))
            {
                var normal = face.Normal.Normalized();
                var denominator = Vector3.Dot(ray.Direction, normal);
                if (Math.Abs(denominator) < MinimumRayDenominator) continue;

                // A ray entering an outward-facing box plane is front-facing. A positive
                // denominator is the far/underlay plane reached after passing through the box.
                var isFrontFacing = denominator < 0;
                if (!isFrontFacing) continue;

                Vector3 hitPoint;
                double rayDistance;
                if (!ray.TryIntersectPlane(face.Center, normal, out hitPoint, out rayDistance)) continue;

                Vector3 uAxis;
                Vector3 vAxis;
                double halfU;
                double halfV;
                if (!TryGetFaceUv(face, out uAxis, out vAxis, out halfU, out halfV)) continue;

                // A pixel tolerance is scaled at this candidate's actual ray distance. This is
                // the world-space equivalent of the old fixed 10 px band without allowing it to
                // dominate a small, near, or edge-on face.
                var unitsPerPixel = ray.WorldUnitsPerPixelAt(rayDistance);
                if (unitsPerPixel <= 0 || double.IsNaN(unitsPerPixel) || double.IsInfinity(unitsPerPixel)) continue;
                var tolerance = Math.Max(0, edgeTolerancePixels) * unitsPerPixel;
                var local = hitPoint - face.Center;
                var u = Vector3.Dot(local, uAxis);
                var v = Vector3.Dot(local, vAxis);
                var outsideU = Math.Max(0, Math.Abs(u) - halfU);
                var outsideV = Math.Max(0, Math.Abs(v) - halfV);
                var edgeDistanceWorld = Math.Sqrt(outsideU * outsideU + outsideV * outsideV);
                var isInside = edgeDistanceWorld <= tolerance;
                if (!isInside) continue;

                candidates.Add(new FaceHitResult
                {
                    Face = face,
                    AverageDepth = rayDistance,
                    DistanceToPolygon = edgeDistanceWorld / unitsPerPixel,
                    IsInsidePolygon = outsideU <= 1e-9 && outsideV <= 1e-9,
                    IsFrontFacing = isFrontFacing,
                    RayDistance = rayDistance,
                    HitPoint = hitPoint,
                    FaceU = u,
                    FaceV = v,
                    WorldTolerance = tolerance,
                    PickerMode = "ray"
                });
            }

            var ordered = candidates
                .OrderBy(candidate => candidate.IsInsidePolygon ? 0 : 1)
                .ThenBy(candidate => candidate.RayDistance)
                .ThenBy(candidate => candidate.DistanceToPolygon)
                .ThenBy(candidate => candidate.Face.Id)
                .ToList();
            return new FaceHitProbe
            {
                Candidates = ordered,
                Selected = ordered.FirstOrDefault(),
                SelectedIndex = ordered.Count == 0 ? -1 : 0,
                UsedRayPicker = true,
                Calibration = calibration
            };
        }

        private FaceHitProbe ProbeProjectedPolygon(SectionBoxState state, View view, int mouseX, int mouseY, double edgeTolerancePixels)
        {
            var candidates = new List<FaceHitResult>();
            var viewpoint = view.CreateViewpointCopy();
            if (viewpoint == null || viewpoint.Position == null)
            {
                return new FaceHitProbe
                {
                    Candidates = candidates,
                    UsedRayPicker = false
                };
            }

            var cameraPosition = new Vector3(viewpoint.Position.X, viewpoint.Position.Y, viewpoint.Position.Z);
            foreach (var face in SectionBoxMath.GetFaces(state))
            {
                var isFrontFacing = Vector3.Dot(face.Normal, cameraPosition - face.Center) >= 0;
                if (!isFrontFacing) continue;

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
                    IsInsidePolygon = inside,
                    IsFrontFacing = isFrontFacing,
                    PickerMode = "fallback-2d"
                });
            }

            var ordered = candidates
                .OrderBy(candidate => candidate.DistanceToPolygon)
                .ThenBy(candidate => candidate.AverageDepth)
                .ThenBy(candidate => candidate.Face.Id)
                .ToList();
            return new FaceHitProbe
            {
                Candidates = ordered,
                Selected = ordered.FirstOrDefault(),
                SelectedIndex = ordered.Count == 0 ? -1 : 0,
                UsedRayPicker = false
            };
        }

        private static bool TryGetFaceUv(SectionBoxFace face, out Vector3 uAxis, out Vector3 vAxis, out double halfU, out double halfV)
        {
            uAxis = new Vector3(0, 0, 0);
            vAxis = new Vector3(0, 0, 0);
            halfU = 0;
            halfV = 0;
            if (face == null || face.Corners == null || face.Corners.Length < 4) return false;
            var uEdge = face.Corners[1] - face.Corners[0];
            var vEdge = face.Corners[3] - face.Corners[0];
            var uLength = uEdge.Length;
            var vLength = vEdge.Length;
            if (uLength < 1e-10 || vLength < 1e-10) return false;
            uAxis = uEdge / uLength;
            vAxis = vEdge / vLength;
            halfU = uLength * 0.5;
            halfV = vLength * 0.5;
            return true;
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
