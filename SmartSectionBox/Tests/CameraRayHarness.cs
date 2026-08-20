using System;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;
using SmartSectionBox.Interaction;

internal static class CameraRayHarness
{
    private static int Main()
    {
        try
        {
            VerifyPerspectiveRoundTrip();
            VerifyOrthographicRoundTrip();
            VerifyAlternateMatrixAxisConvention();
            VerifyObbFacePickingAtCivilCoordinates();
            VerifyCalibrationFailureFallsBackSafely();
            VerifyAbsoluteRayDrag();
            Console.WriteLine("All camera-ray calibration, OBB picking, and drag tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void VerifyPerspectiveRoundTrip()
    {
        var view = SyntheticView.Perspective(new Point3D(20, 10, 100), 15.0, 50.0, 40.0, 60.0);
        var builder = new CameraRayBuilder();
        CameraRay ray;
        CameraRayCalibration calibration;
        Assert(builder.TryCreateRay(view, 175, 640, out ray, out calibration), "Perspective ray must calibrate against ProjectPoint.");
        Assert(calibration.IsValid && calibration.MaxErrorPixels <= 1.5, "Perspective calibration error must stay within the picker acceptance limit.");
        Assert(calibration.QuaternionConvention.StartsWith("native-Matrix3"), "Perspective calibration must prefer the host-native Matrix3 rotation basis.");
        AssertRoundTrip(view, ray, 175, 640, 50.0, "Perspective ray must project back to its requested pixel.");
    }

    private static void VerifyOrthographicRoundTrip()
    {
        var view = SyntheticView.Orthographic(new Point3D(-5, 8, 40), -20.0, 30.0, 48.0, 72.0);
        var builder = new CameraRayBuilder();
        CameraRay ray;
        CameraRayCalibration calibration;
        Assert(builder.TryCreateRay(view, 1010, 110, out ray, out calibration), "Orthographic ray must calibrate against ProjectPoint.");
        Assert(calibration.IsValid && calibration.MaxErrorPixels <= 1.5, "Orthographic calibration error must stay within the picker acceptance limit.");
        Assert(calibration.QuaternionConvention.StartsWith("native-Matrix3"), "Orthographic calibration must prefer the host-native Matrix3 rotation basis.");
        AssertRoundTrip(view, ray, 1010, 110, 30.0, "Orthographic ray must project back to its requested pixel.");
    }

            private static void VerifyAlternateMatrixAxisConvention()
        {
            // This synthetic host has the same Matrix3 rotation but uses local +X as look
            // direction, +Z as up, and -Y as screen right. The picker must derive that mapping
            // from ProjectPoint rather than hard-coding local +X/+Y/-Z.
            var view = SyntheticView.PerspectiveWithAlternateMatrixAxes(new Point3D(20, 10, 100), 15.0, 50.0, 40.0, 60.0);
            var builder = new CameraRayBuilder();
            CameraRay ray;
            CameraRayCalibration calibration;
            Assert(builder.TryCreateRay(view, 175, 640, out ray, out calibration),
                "An alternate native Matrix3 camera-axis convention must calibrate against ProjectPoint.");
            Assert(calibration.QuaternionConvention.StartsWith("native-Matrix3"),
                "An alternate convention must remain on the native Matrix3 path.");
            Assert(calibration.QuaternionConvention.Contains("forward=+X") && calibration.QuaternionConvention.Contains("up=+Z"),
                "The diagnostic basis must identify the selected alternate Matrix3 axes.");
            AssertRoundTrip(view, ray, 175, 640, 50.0, "Alternate Matrix3 axes must project back to the requested pixel.");
        }

        private static void VerifyObbFacePickingAtCivilCoordinates()

    {
        var baseX = 2440000.0;
        var baseY = 9080000.0;
        var view = SyntheticView.Perspective(new Point3D(baseX, baseY, 100), 0, 50, 40, 60);
        var state = new SectionBoxState
        {
            MinX = baseX - 10,
            MaxX = baseX + 10,
            MinY = baseY - 10,
            MaxY = baseY + 10,
            MinZ = 0,
            MaxZ = 20,
            RotationX = 0,
            RotationY = 0,
            RotationZ = 0,
            Enabled = true
        };
        var tester = new FaceHitTester(new CameraProjection());
        var front = tester.Probe(state, view, view.Width / 2, view.Height / 2, 10);
        Assert(front.UsedRayPicker, "Large-coordinate OBB picking must use the calibrated ray branch.");
        Assert(front.Selected != null && front.Selected.Face.Id == SectionBoxFaceId.MaxZ, "Nearest front-facing face must win by ray distance.");
        Assert(front.Selected.RayDistance > 0, "Front hit must expose a positive true ray distance.");
        foreach (var candidate in front.Candidates)
        {
            Assert(candidate.IsFrontFacing, "Front-facing-only picking must reject every underlay candidate.");
            Assert(candidate.Face.Id != SectionBoxFaceId.MinZ, "The ray exit face must never be exposed as a candidate.");
        }
    }

    private static void VerifyCalibrationFailureFallsBackSafely()
    {
        var view = SyntheticView.Uncalibrated(new Point3D(0, 0, 100), 0, 50, 40, 60);
        var builder = new CameraRayBuilder();
        CameraRay ray;
        CameraRayCalibration calibration;
        Assert(!builder.TryCreateRay(view, view.Width / 2, view.Height / 2, out ray, out calibration),
            "A non-round-tripping ProjectPoint implementation must reject calibrated ray construction.");
        Assert(!calibration.IsValid, "A rejected ray must report invalid calibration.");
        Assert(calibration.QuaternionConvention != "none", "A rejected ray must retain its best tested basis for host diagnostics.");

        var state = new SectionBoxState
        {
            MinX = -10,
            MaxX = 10,
            MinY = -10,
            MaxY = 10,
            MinZ = 0,
            MaxZ = 20,
            Enabled = true
        };
        var probe = new FaceHitTester(new CameraProjection()).Probe(state, view, view.Width / 2, view.Height / 2, 10);
        Assert(!probe.UsedRayPicker, "A failed calibration must route picking to the explicit legacy fallback.");
        Assert(probe.Calibration != null && !probe.Calibration.IsValid, "The fallback probe must retain the calibration failure for diagnostics.");
    }

    private static void VerifyAbsoluteRayDrag()
    {
        // The camera is oblique to MaxZ. This keeps the face normal away from the head-on
        // singularity and gives horizontal screen movement a measurable MaxZ component.
        var view = SyntheticView.Perspective(new Point3D(45, 0, 87.942286), 30, 50, 40, 60);
        Autodesk.Navisworks.Api.Application.ActiveDocument = new Autodesk.Navisworks.Api.Document { ActiveView = view };
        var initial = new SectionBoxState
        {
            MinX = -10,
            MaxX = 10,
            MinY = -10,
            MaxY = 10,
            MinZ = 0,
            MaxZ = 20,
            Enabled = true
        };
        var service = new SectionBoxService { LiveApplyIntervalMilliseconds = 0 };
        Assert(service.SetBox(initial, true), "The synthetic service must accept the starting section box.");
        var tester = new FaceHitTester(new CameraProjection());
        var startX = view.Width / 2;
        var startY = view.Height / 2;
        var selected = tester.Probe(initial, view, startX, startY, 10).Selected;
        Assert(selected != null && selected.Face.Id == SectionBoxFaceId.MaxZ, "The oblique drag test must capture MaxZ.");

        var controller = new DragController(service, new CameraProjection());
        Assert(controller.Begin(selected, startX, startY, view), "The drag controller must capture the selected face.");
        Assert(controller.UsesCalibratedRayDrag, "An oblique ray-selected face must use fixed-reference-plane drag movement.");
        var initialCoordinate = controller.InitialCoordinate;
        Assert(controller.Update(startX + 120, startY, KeyModifiers.None, view), "The first ray drag sample must apply.");
        var offsetCoordinate = controller.WorkingCoordinate;
        Assert(Math.Abs(offsetCoordinate - initialCoordinate) > 1e-5, "An oblique horizontal drag must move the selected face.");
        Assert(controller.Update(startX, startY, KeyModifiers.None, view), "Returning to the start pixel must apply.");
        Assert(Math.Abs(controller.WorkingCoordinate - initialCoordinate) < 1e-5,
            "Ray drag must resolve from the original hit point instead of accumulating deltas.");
    }

    private static void AssertRoundTrip(SyntheticView view, CameraRay ray, int expectedX, int expectedY, double distance, string message)
    {
        var point = ray.PointAt(distance);
        var projected = view.ProjectPoint(new Point3D(point.X, point.Y, point.Z), false, false);
        var error = Math.Sqrt(Math.Pow(projected.X - expectedX, 2) + Math.Pow(projected.Y - expectedY, 2));
        Assert(error <= 1.5, message + " Error=" + error);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class SyntheticView : View
    {
        private readonly Viewpoint viewpoint;
        private readonly bool useAlternateMatrixAxes;
        private bool forceProjectionFailure;

        private SyntheticView(Viewpoint viewpoint, bool useAlternateMatrixAxes = false)
        {
            this.viewpoint = viewpoint;
            this.useAlternateMatrixAxes = useAlternateMatrixAxes;
            Width = 1200;
            Height = 800;
        }

        public static SyntheticView Perspective(Point3D position, double yawDegrees, double focal, double verticalExtent, double horizontalExtent)
        {
            return new SyntheticView(CreateViewpoint(position, yawDegrees, ViewpointProjection.Perspective, focal, verticalExtent, horizontalExtent));
        }

        public static SyntheticView Orthographic(Point3D position, double yawDegrees, double focal, double verticalExtent, double horizontalExtent)
        {
            return new SyntheticView(CreateViewpoint(position, yawDegrees, ViewpointProjection.Orthographic, focal, verticalExtent, horizontalExtent));
        }

        public static SyntheticView PerspectiveWithAlternateMatrixAxes(Point3D position, double yawDegrees, double focal, double verticalExtent, double horizontalExtent)
        {
            return new SyntheticView(
                CreateViewpoint(position, yawDegrees, ViewpointProjection.Perspective, focal, verticalExtent, horizontalExtent),
                true);
        }

        public static SyntheticView Uncalibrated(Point3D position, double yawDegrees, double focal, double verticalExtent, double horizontalExtent)
        {
            return new SyntheticView(CreateViewpoint(position, yawDegrees, ViewpointProjection.Perspective, focal, verticalExtent, horizontalExtent))
            {
                forceProjectionFailure = true
            };
        }

        public override Viewpoint CreateViewpointCopy()
        {
            return viewpoint;
        }

        public override ProjectionResult ProjectPoint(Point3D point, bool sectionClip, bool frustumClip)
        {
            if (forceProjectionFailure) return new ProjectionResult { X = 0, Y = 0, Depth = 0 };
            var relative = new Vector3(point.X - viewpoint.Position.X, point.Y - viewpoint.Position.Y, point.Z - viewpoint.Position.Z);
            var local = InverseRotate(relative, viewpoint.Rotation);
            var depth = useAlternateMatrixAxes ? local.X : -local.Z;
            var screenRight = useAlternateMatrixAxes ? -local.Y : local.X;
            var screenUp = useAlternateMatrixAxes ? local.Z : local.Y;
            double x;
            double y;
            if (viewpoint.Projection == ViewpointProjection.Perspective)
            {
                x = Width * (0.5 + screenRight * viewpoint.FocalDistance / (depth * viewpoint.HorizontalExtentAtFocalDistance));
                y = Height * (0.5 - screenUp * viewpoint.FocalDistance / (depth * viewpoint.VerticalExtentAtFocalDistance));
            }
            else
            {
                x = Width * (0.5 + screenRight / viewpoint.HorizontalExtentAtFocalDistance);
                y = Height * (0.5 - screenUp / viewpoint.VerticalExtentAtFocalDistance);
            }

            return new ProjectionResult { X = x, Y = y, Depth = depth };
        }

        private static Viewpoint CreateViewpoint(Point3D position, double yawDegrees, ViewpointProjection projection, double focal, double verticalExtent, double horizontalExtent)
        {
            var radians = yawDegrees * Math.PI / 180.0;
            return new Viewpoint
            {
                Position = position,
                Projection = projection,
                FocalDistance = focal,
                VerticalExtentAtFocalDistance = verticalExtent,
                HorizontalExtentAtFocalDistance = horizontalExtent,
                Rotation = new Rotation3D
                {
                    A = 0,
                    B = Math.Sin(radians * 0.5),
                    C = 0,
                    D = Math.Cos(radians * 0.5)
                }
            };
        }

        private static Vector3 InverseRotate(Vector3 vector, Rotation3D rotation)
        {
            return Rotate(vector, -rotation.A, -rotation.B, -rotation.C, rotation.D);
        }

        private static Vector3 Rotate(Vector3 vector, double x, double y, double z, double w)
        {
            var norm = Math.Sqrt(x * x + y * y + z * z + w * w);
            x /= norm;
            y /= norm;
            z /= norm;
            w /= norm;
            var tx = 2.0 * (y * vector.Z - z * vector.Y);
            var ty = 2.0 * (z * vector.X - x * vector.Z);
            var tz = 2.0 * (x * vector.Y - y * vector.X);
            return new Vector3(
                vector.X + w * tx + (y * tz - z * ty),
                vector.Y + w * ty + (z * tx - x * tz),
                vector.Z + w * tz + (x * ty - y * tx));
        }
    }
}
