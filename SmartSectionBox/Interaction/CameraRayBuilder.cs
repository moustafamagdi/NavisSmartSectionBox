using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;

namespace SmartSectionBox.Interaction
{
    /// <summary>
    /// A camera ray expressed in Navisworks world coordinates. The ray retains the projection
    /// scale used to construct it so hit testing can convert screen tolerances to world units.
    /// </summary>
    public struct CameraRay
    {
        public CameraRay(
            Vector3 origin,
            Vector3 direction,
            Vector3 forward,
            Vector3 right,
            Vector3 up,
            ViewpointProjection projection,
            double verticalExtentAtFocalDistance,
            double focalDistance,
            int viewportHeight,
            double extentScale)
        {
            Origin = origin;
            Direction = direction.Normalized();
            Forward = forward.Normalized();
            Right = right.Normalized();
            Up = up.Normalized();
            Projection = projection;
            VerticalExtentAtFocalDistance = verticalExtentAtFocalDistance;
            FocalDistance = focalDistance;
            ViewportHeight = viewportHeight;
            ExtentScale = extentScale;
        }

        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public Vector3 Forward { get; }
        public Vector3 Right { get; }
        public Vector3 Up { get; }
        public ViewpointProjection Projection { get; }
        public double VerticalExtentAtFocalDistance { get; }
        public double FocalDistance { get; }
        public int ViewportHeight { get; }
        public double ExtentScale { get; }

        public Vector3 PointAt(double distance)
        {
            return Origin + Direction * distance;
        }

        public bool TryIntersectPlane(Vector3 planePoint, Vector3 planeNormal, out Vector3 hitPoint, out double distance)
        {
            hitPoint = new Vector3(0, 0, 0);
            distance = 0;
            var normal = planeNormal.Normalized();
            var denominator = Vector3.Dot(Direction, normal);
            if (Math.Abs(denominator) < 1e-10) return false;

            // Evaluate relative to the plane point. Apart from being algebraically simple, this
            // prevents unnecessary large-world-coordinate products on civil-size models.
            var relativeOrigin = Origin - planePoint;
            distance = -Vector3.Dot(relativeOrigin, normal) / denominator;
            if (distance <= 1e-9) return false;
            hitPoint = PointAt(distance);
            return true;
        }

        public double WorldUnitsPerPixelAt(double rayDistance)
        {
            if (ViewportHeight <= 0 || VerticalExtentAtFocalDistance <= 0) return 0;
            var visibleHeight = VerticalExtentAtFocalDistance * ExtentScale;
            if (Projection == ViewpointProjection.Perspective)
            {
                var focal = Math.Max(FocalDistance, 1e-9);
                // Ray distance is deliberately used rather than world-origin distance. It is
                // stable for boxes at very large civil coordinates and correctly grows with
                // perspective distance from the eye.
                visibleHeight *= Math.Max(rayDistance, 1e-9) / focal;
            }

            return visibleHeight / ViewportHeight;
        }
    }

    /// <summary>
    /// Records whether a ray basis was verified through View.ProjectPoint. Invalid calibration
    /// intentionally causes the caller to use the legacy 2D picker rather than guess.
    /// </summary>
    public sealed class CameraRayCalibration
    {
        public bool IsValid { get; internal set; }
        public double MeanErrorPixels { get; internal set; }
        public double MaxErrorPixels { get; internal set; }
        public double ExtentScale { get; internal set; }
        public string QuaternionConvention { get; internal set; }
        public string FailureReason { get; internal set; }

        public static CameraRayCalibration Invalid(string reason)
        {
            return new CameraRayCalibration
            {
                IsValid = false,
                FailureReason = reason ?? "unknown",
                QuaternionConvention = "none"
            };
        }
    }

    /// <summary>
    /// Builds a supported managed-API screen ray from Viewpoint.Position, Viewpoint.Rotation,
    /// focal-plane extents, and projection type. Navisworks documents quaternion components as
    /// A/B/C/D rather than X/Y/Z/W, so two common component layouts and both conjugation senses
    /// are calibrated against View.ProjectPoint before the result can drive picking.
    /// </summary>
    public sealed class CameraRayBuilder
    {
        private const double MaximumCalibrationErrorPixels = 1.5;
        private readonly object sync = new object();
        private CameraSnapshot cachedSnapshot;
        private CalibrationModel cachedModel;
        private CameraRayCalibration cachedCalibration;

        public CameraRayCalibration LastCalibration
        {
            get
            {
                lock (sync)
                {
                    return cachedCalibration ?? CameraRayCalibration.Invalid("not-calibrated");
                }
            }
        }

        public bool TryCreateRay(View view, int mouseX, int mouseY, out CameraRay ray, out CameraRayCalibration calibration)
        {
            ray = default(CameraRay);
            calibration = CameraRayCalibration.Invalid("uninitialized");
            if (view == null || view.Width <= 0 || view.Height <= 0)
            {
                calibration = CameraRayCalibration.Invalid("invalid-viewport");
                return false;
            }

            Viewpoint viewpoint;
            try
            {
                viewpoint = view.CreateViewpointCopy();
            }
            catch
            {
                calibration = CameraRayCalibration.Invalid("viewpoint-copy-failed");
                return false;
            }

            if (viewpoint == null || viewpoint.Position == null || viewpoint.Rotation == null)
            {
                calibration = CameraRayCalibration.Invalid("camera-state-unavailable");
                return false;
            }

            var snapshot = CameraSnapshot.From(view, viewpoint);
            if (!snapshot.IsUsable)
            {
                calibration = CameraRayCalibration.Invalid(snapshot.InvalidReason);
                return false;
            }

            CalibrationModel model;
            lock (sync)
            {
                if (cachedSnapshot.Equals(snapshot) && cachedModel != null && cachedCalibration != null && cachedCalibration.IsValid)
                {
                    model = cachedModel;
                    calibration = cachedCalibration;
                }
                else
                {
                    model = Calibrate(view, snapshot, out calibration);
                    cachedSnapshot = snapshot;
                    cachedModel = model;
                    cachedCalibration = calibration;
                }
            }

            if (model == null || calibration == null || !calibration.IsValid) return false;
            ray = BuildRay(snapshot, model, mouseX, mouseY);

            // Verify the actual requested location too. Calibration anchors prove the basis;
            // this guard protects the extreme viewport edges and avoids applying a bad ray on a
            // host/version whose camera semantics do not round-trip as expected.
            var error = RoundTripError(view, ray, mouseX, mouseY);
            if (double.IsNaN(error) || double.IsInfinity(error) || error > MaximumCalibrationErrorPixels)
            {
                calibration = CameraRayCalibration.Invalid("screen-ray-round-trip-failed");
                lock (sync)
                {
                    cachedSnapshot = snapshot;
                    cachedModel = null;
                    cachedCalibration = calibration;
                }
                return false;
            }

            return true;
        }

        private static CalibrationModel Calibrate(View view, CameraSnapshot snapshot, out CameraRayCalibration calibration)
        {
            CalibrationModel best = null;
            var samplePoints = CalibrationPoints(snapshot.ViewportWidth, snapshot.ViewportHeight);
            foreach (var candidate in CalibrationModel.Candidates(snapshot.HasNativeMatrix))
            {
                var result = CalibrateCandidate(view, snapshot, candidate, samplePoints);
                if (best == null || result.MeanErrorPixels < best.MeanErrorPixels)
                {
                    best = result;
                }
            }

            if (best == null || double.IsInfinity(best.MeanErrorPixels) || best.MaxErrorPixels > MaximumCalibrationErrorPixels)
            {
                calibration = new CameraRayCalibration
                {
                    IsValid = false,
                    MeanErrorPixels = best == null ? double.PositiveInfinity : best.MeanErrorPixels,
                    MaxErrorPixels = best == null ? double.PositiveInfinity : best.MaxErrorPixels,
                    ExtentScale = best == null ? 0 : best.ExtentScale,
                    QuaternionConvention = best == null ? "none" : best.Name,
                    FailureReason = "camera-basis-calibration-failed"
                };
                return null;
            }

            calibration = new CameraRayCalibration
            {
                IsValid = true,
                MeanErrorPixels = best.MeanErrorPixels,
                MaxErrorPixels = best.MaxErrorPixels,
                ExtentScale = best.ExtentScale,
                QuaternionConvention = best.Name,
                FailureReason = null
            };
            return best;
        }

        private static CalibrationModel CalibrateCandidate(View view, CameraSnapshot snapshot, CalibrationModel candidate, IReadOnlyList<ScreenPoint> samplePoints)
        {
            // The documented extents should normally select a scale of 1.0. The small bounded
            // search makes scale semantics self-verifying instead of relying on an undocumented
            // half-angle/full-extent assumption.
            var trialScales = new[] { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 4.0 };
            CalibrationModel best = null;
            foreach (var scale in trialScales)
            {
                var trial = candidate.WithScale(scale);
                ScoreCalibration(view, snapshot, trial, samplePoints);
                if (best == null || trial.MeanErrorPixels < best.MeanErrorPixels) best = trial;
            }

            if (best == null || double.IsInfinity(best.MeanErrorPixels)) return candidate.WithFailure();

            // Refine around the strongest coarse value. This costs only a handful of projection
            // calls and happens once per changed camera state, never once per face.
            var lower = Math.Max(0.05, best.ExtentScale * 0.70);
            var upper = Math.Min(8.0, best.ExtentScale * 1.30);
            for (var iteration = 0; iteration < 6; iteration++)
            {
                var oneThird = lower + (upper - lower) / 3.0;
                var twoThirds = upper - (upper - lower) / 3.0;
                var first = candidate.WithScale(oneThird);
                var second = candidate.WithScale(twoThirds);
                ScoreCalibration(view, snapshot, first, samplePoints);
                ScoreCalibration(view, snapshot, second, samplePoints);
                if (first.MeanErrorPixels <= second.MeanErrorPixels)
                {
                    best = first.MeanErrorPixels < best.MeanErrorPixels ? first : best;
                    upper = twoThirds;
                }
                else
                {
                    best = second.MeanErrorPixels < best.MeanErrorPixels ? second : best;
                    lower = oneThird;
                }
            }

            return best;
        }

        private static void ScoreCalibration(View view, CameraSnapshot snapshot, CalibrationModel model, IReadOnlyList<ScreenPoint> points)
        {
            var total = 0.0;
            var maximum = 0.0;
            foreach (var point in points)
            {
                var ray = BuildRay(snapshot, model, point.X, point.Y);
                var error = RoundTripError(view, ray, point.X, point.Y);
                if (double.IsNaN(error) || double.IsInfinity(error))
                {
                    model.MeanErrorPixels = double.PositiveInfinity;
                    model.MaxErrorPixels = double.PositiveInfinity;
                    return;
                }

                total += error;
                maximum = Math.Max(maximum, error);
            }

            model.MeanErrorPixels = total / Math.Max(1, points.Count);
            model.MaxErrorPixels = maximum;
        }

        private static CameraRay BuildRay(CameraSnapshot snapshot, CalibrationModel model, double pixelX, double pixelY)
        {
            Vector3 right;
            Vector3 up;
            Vector3 forward;
            GetBasis(snapshot, model, out right, out up, out forward);

            var ndcX = 2.0 * pixelX / snapshot.ViewportWidth - 1.0;
            var ndcY = 1.0 - 2.0 * pixelY / snapshot.ViewportHeight;
            var halfWidth = snapshot.HorizontalExtentAtFocalDistance * model.ExtentScale * 0.5;
            var halfHeight = snapshot.VerticalExtentAtFocalDistance * model.ExtentScale * 0.5;
            Vector3 origin;
            Vector3 direction;
            if (snapshot.Projection == ViewpointProjection.Orthographic)
            {
                origin = snapshot.Position + right * (ndcX * halfWidth) + up * (ndcY * halfHeight);
                direction = forward;
            }
            else
            {
                origin = snapshot.Position;
                direction = (forward * snapshot.FocalDistance + right * (ndcX * halfWidth) + up * (ndcY * halfHeight)).Normalized();
            }

            return new CameraRay(
                origin,
                direction,
                forward,
                right,
                up,
                snapshot.Projection,
                snapshot.VerticalExtentAtFocalDistance,
                snapshot.FocalDistance,
                snapshot.ViewportHeight,
                model.ExtentScale);
        }

        private static void GetBasis(CameraSnapshot snapshot, CalibrationModel model, out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            if (model.BasisSource == BasisSource.NativeMatrix)
            {
                if (model.UseInverse)
                {
                    // Rows are the world-space axes when the native matrix is interpreted as
                    // world-to-camera. The calibration chooses this only if ProjectPoint agrees.
                    right = new Vector3(snapshot.M00, snapshot.M01, snapshot.M02).Normalized();
                    up = new Vector3(snapshot.M10, snapshot.M11, snapshot.M12).Normalized();
                    forward = new Vector3(-snapshot.M20, -snapshot.M21, -snapshot.M22).Normalized();
                }
                else
                {
                    // Columns are the world-space images of the local camera +X/+Y/+Z axes
                    // when the native matrix is interpreted as camera-to-world.
                    right = new Vector3(snapshot.M00, snapshot.M10, snapshot.M20).Normalized();
                    up = new Vector3(snapshot.M01, snapshot.M11, snapshot.M21).Normalized();
                    forward = new Vector3(-snapshot.M02, -snapshot.M12, -snapshot.M22).Normalized();
                }

                right = Vector3.Cross(forward, up).Normalized();
                up = Vector3.Cross(right, forward).Normalized();
                return;
            }

            var x = model.ComponentLayout == QuaternionLayout.ABcd ? snapshot.QuaternionA : snapshot.QuaternionB;
            var y = model.ComponentLayout == QuaternionLayout.ABcd ? snapshot.QuaternionB : snapshot.QuaternionC;
            var z = model.ComponentLayout == QuaternionLayout.ABcd ? snapshot.QuaternionC : snapshot.QuaternionD;
            var w = model.ComponentLayout == QuaternionLayout.ABcd ? snapshot.QuaternionD : snapshot.QuaternionA;
            if (model.UseInverse) { x = -x; y = -y; z = -z; }

            right = Rotate(new Vector3(1, 0, 0), x, y, z, w).Normalized();
            up = Rotate(new Vector3(0, 1, 0), x, y, z, w).Normalized();
            forward = Rotate(new Vector3(0, 0, -1), x, y, z, w).Normalized();

            // Preserve a mathematically orthogonal, right-handed basis even if the host
            // quaternion has negligible serialization noise.
            right = Vector3.Cross(forward, up).Normalized();
            up = Vector3.Cross(right, forward).Normalized();
        }

        private static Vector3 Rotate(Vector3 vector, double x, double y, double z, double w)
        {
            var norm = Math.Sqrt(x * x + y * y + z * z + w * w);
            if (norm < 1e-12) return new Vector3(0, 0, 0);
            x /= norm;
            y /= norm;
            z /= norm;
            w /= norm;

            // q * v * q^-1, with v encoded as a pure quaternion.
            var tx = 2.0 * (y * vector.Z - z * vector.Y);
            var ty = 2.0 * (z * vector.X - x * vector.Z);
            var tz = 2.0 * (x * vector.Y - y * vector.X);
            return new Vector3(
                vector.X + w * tx + (y * tz - z * ty),
                vector.Y + w * ty + (z * tx - x * tz),
                vector.Z + w * tz + (x * ty - y * tx));
        }

        private static double RoundTripError(View view, CameraRay ray, double expectedX, double expectedY)
        {
            try
            {
                var forwardDot = Math.Max(Math.Abs(Vector3.Dot(ray.Direction, ray.Forward)), 1e-6);
                var distance = ray.Projection == ViewpointProjection.Perspective
                    ? Math.Max(ray.FocalDistance, 1.0) / forwardDot
                    : Math.Max(ray.FocalDistance, 1.0);
                var point = ray.PointAt(distance);
                var projection = view.ProjectPoint(new Point3D(point.X, point.Y, point.Z), false, false);
                if (projection == null) return double.PositiveInfinity;
                var dx = projection.X - expectedX;
                var dy = projection.Y - expectedY;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            catch
            {
                return double.PositiveInfinity;
            }
        }

        private static IReadOnlyList<ScreenPoint> CalibrationPoints(int width, int height)
        {
            return new[]
            {
                new ScreenPoint(width * 0.23, height * 0.31, 0),
                new ScreenPoint(width * 0.73, height * 0.29, 0),
                new ScreenPoint(width * 0.38, height * 0.74, 0)
            };
        }

        private enum QuaternionLayout
        {
            ABcd,
            WAbc
        }

        private enum BasisSource
        {
            NativeMatrix,
            Quaternion
        }

        private sealed class CalibrationModel
        {
            public BasisSource BasisSource { get; private set; }
            public QuaternionLayout ComponentLayout { get; private set; }
            public bool UseInverse { get; private set; }
            public double ExtentScale { get; private set; }
            public string Name { get; private set; }
            public double MeanErrorPixels { get; set; }
            public double MaxErrorPixels { get; set; }

            private CalibrationModel(BasisSource basisSource, QuaternionLayout layout, bool useInverse, double extentScale)
            {
                BasisSource = basisSource;
                ComponentLayout = layout;
                UseInverse = useInverse;
                ExtentScale = extentScale;
                Name = basisSource == BasisSource.NativeMatrix
                    ? "native-Matrix3(Rotation3D)" + (useInverse ? "; inverse" : "; direct")
                    : (layout == QuaternionLayout.ABcd ? "A,B,C,D=>x,y,z,w" : "A,B,C,D=>w,x,y,z") +
                      (useInverse ? "; inverse" : "; direct");
                MeanErrorPixels = double.PositiveInfinity;
                MaxErrorPixels = double.PositiveInfinity;
            }

            public static IEnumerable<CalibrationModel> Candidates(bool hasNativeMatrix)
            {
                if (hasNativeMatrix)
                {
                    yield return new CalibrationModel(BasisSource.NativeMatrix, QuaternionLayout.ABcd, false, 1.0);
                    yield return new CalibrationModel(BasisSource.NativeMatrix, QuaternionLayout.ABcd, true, 1.0);
                }
                yield return new CalibrationModel(BasisSource.Quaternion, QuaternionLayout.ABcd, false, 1.0);
                yield return new CalibrationModel(BasisSource.Quaternion, QuaternionLayout.ABcd, true, 1.0);
                yield return new CalibrationModel(BasisSource.Quaternion, QuaternionLayout.WAbc, false, 1.0);
                yield return new CalibrationModel(BasisSource.Quaternion, QuaternionLayout.WAbc, true, 1.0);
            }

            public CalibrationModel WithScale(double scale)
            {
                return new CalibrationModel(BasisSource, ComponentLayout, UseInverse, scale);
            }

            public CalibrationModel WithFailure()
            {
                return new CalibrationModel(BasisSource, ComponentLayout, UseInverse, ExtentScale);
            }
        }

        private struct CameraSnapshot : IEquatable<CameraSnapshot>
        {
            public Vector3 Position;
            public double QuaternionA;
            public double QuaternionB;
            public double QuaternionC;
            public double QuaternionD;
            public bool HasNativeMatrix;
            public double M00;
            public double M01;
            public double M02;
            public double M10;
            public double M11;
            public double M12;
            public double M20;
            public double M21;
            public double M22;
            public ViewpointProjection Projection;
            public double VerticalExtentAtFocalDistance;
            public double HorizontalExtentAtFocalDistance;
            public double FocalDistance;
            public int ViewportWidth;
            public int ViewportHeight;
            public bool IsUsable;
            public string InvalidReason;

            public static CameraSnapshot From(View view, Viewpoint viewpoint)
            {
                var vertical = viewpoint.VerticalExtentAtFocalDistance;
                var horizontal = viewpoint.HorizontalExtentAtFocalDistance;
                var focal = viewpoint.FocalDistance;
                var snapshot = new CameraSnapshot
                {
                    Position = new Vector3(viewpoint.Position.X, viewpoint.Position.Y, viewpoint.Position.Z),
                    QuaternionA = viewpoint.Rotation.A,
                    QuaternionB = viewpoint.Rotation.B,
                    QuaternionC = viewpoint.Rotation.C,
                    QuaternionD = viewpoint.Rotation.D,
                    Projection = viewpoint.Projection,
                    VerticalExtentAtFocalDistance = vertical,
                    HorizontalExtentAtFocalDistance = horizontal > 1e-9 ? horizontal : vertical * view.Width / Math.Max(1.0, view.Height),
                    FocalDistance = focal,
                    ViewportWidth = view.Width,
                    ViewportHeight = view.Height
                };

                try
                {
                    using (var nativeMatrix = new Matrix3(viewpoint.Rotation))
                    {
                        snapshot.M00 = nativeMatrix.Get(0, 0); snapshot.M01 = nativeMatrix.Get(0, 1); snapshot.M02 = nativeMatrix.Get(0, 2);
                        snapshot.M10 = nativeMatrix.Get(1, 0); snapshot.M11 = nativeMatrix.Get(1, 1); snapshot.M12 = nativeMatrix.Get(1, 2);
                        snapshot.M20 = nativeMatrix.Get(2, 0); snapshot.M21 = nativeMatrix.Get(2, 1); snapshot.M22 = nativeMatrix.Get(2, 2);
                        snapshot.HasNativeMatrix = true;
                    }
                }
                catch
                {
                    // Matrix3 is the preferred managed path. Keep the established quaternion
                    // candidates available on hosts where matrix materialization is unavailable.
                    snapshot.HasNativeMatrix = false;
                }

                var quaternionLength = Math.Sqrt(snapshot.QuaternionA * snapshot.QuaternionA + snapshot.QuaternionB * snapshot.QuaternionB + snapshot.QuaternionC * snapshot.QuaternionC + snapshot.QuaternionD * snapshot.QuaternionD);
                snapshot.IsUsable = vertical > 1e-9 && snapshot.HorizontalExtentAtFocalDistance > 1e-9 &&
                                    (snapshot.Projection == ViewpointProjection.Orthographic || focal > 1e-9) &&
                                    quaternionLength > 1e-9;
                snapshot.InvalidReason = snapshot.IsUsable ? null : "invalid-camera-extents-or-rotation";
                return snapshot;
            }

            public bool Equals(CameraSnapshot other)
            {
                return NearlyEqual(Position.X, other.Position.X) && NearlyEqual(Position.Y, other.Position.Y) && NearlyEqual(Position.Z, other.Position.Z) &&
                       NearlyEqual(QuaternionA, other.QuaternionA) && NearlyEqual(QuaternionB, other.QuaternionB) &&
                       NearlyEqual(QuaternionC, other.QuaternionC) && NearlyEqual(QuaternionD, other.QuaternionD) &&
                       HasNativeMatrix == other.HasNativeMatrix &&
                       (!HasNativeMatrix || (NearlyEqual(M00, other.M00) && NearlyEqual(M01, other.M01) && NearlyEqual(M02, other.M02) &&
                                             NearlyEqual(M10, other.M10) && NearlyEqual(M11, other.M11) && NearlyEqual(M12, other.M12) &&
                                             NearlyEqual(M20, other.M20) && NearlyEqual(M21, other.M21) && NearlyEqual(M22, other.M22))) &&
                       Projection == other.Projection && NearlyEqual(VerticalExtentAtFocalDistance, other.VerticalExtentAtFocalDistance) &&
                       NearlyEqual(HorizontalExtentAtFocalDistance, other.HorizontalExtentAtFocalDistance) && NearlyEqual(FocalDistance, other.FocalDistance) &&
                       ViewportWidth == other.ViewportWidth && ViewportHeight == other.ViewportHeight;
            }

            public override bool Equals(object obj)
            {
                return obj is CameraSnapshot && Equals((CameraSnapshot)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = ViewportWidth;
                    hash = hash * 397 ^ ViewportHeight;
                    hash = hash * 397 ^ (int)Projection;
                    return hash;
                }
            }

            private static bool NearlyEqual(double left, double right)
            {
                return Math.Abs(left - right) <= 1e-8 * Math.Max(1.0, Math.Max(Math.Abs(left), Math.Abs(right)));
            }
        }
    }
}
