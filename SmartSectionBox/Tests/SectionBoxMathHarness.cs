using System;
using SmartSectionBox.Core;

internal static class SectionBoxMathHarness
{
    private static int Main()
    {
        try
        {
            TestAxisAlignedFaces();
            TestRotatedNormal();
            TestMinimumThicknessClamp();
            TestLargeCoordinateBounds();
            Console.WriteLine("All isolated SectionBoxMath tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void TestAxisAlignedFaces()
    {
        var state = new SectionBoxState { MinX = 10, MinY = 20, MinZ = 30, MaxX = 14, MaxY = 26, MaxZ = 38 };
        var faces = SectionBoxMath.GetFaces(state);
        AssertNear(10, faces[0].Center.X, "Min X face centre");
        AssertNear(-1, faces[0].Normal.X, "Min X normal");
        AssertNear(14, faces[1].Center.X, "Max X face centre");
        AssertNear(1, faces[1].Normal.X, "Max X normal");
        AssertNear(30, faces[4].Center.Z, "Min Z face centre");
    }

    private static void TestRotatedNormal()
    {
        var state = new SectionBoxState { MinX = 0, MinY = 0, MinZ = 0, MaxX = 2, MaxY = 2, MaxZ = 2, RotationZ = 90.0 };
        var maxX = SectionBoxMath.GetFaces(state)[1];
        AssertNear(0, maxX.Normal.X, "Rotated Max X normal X");
        AssertNear(1, maxX.Normal.Y, "Rotated Max X normal Y");
        var local = SectionBoxMath.InverseRotateLocal(maxX.Normal, state);
        AssertNear(1, local.X, "Inverse rotated normal X");
        AssertNear(0, local.Y, "Inverse rotated normal Y");
    }

    private static void TestMinimumThicknessClamp()
    {
        var state = new SectionBoxState { MinX = 0, MinY = 0, MinZ = 0, MaxX = 10, MaxY = 10, MaxZ = 10 };
        state.SetFaceCoordinate(SectionBoxFaceId.MaxX, -100, 0.25);
        AssertNear(0.25, state.MaxX, "Max X clamp");
        state.SetFaceCoordinate(SectionBoxFaceId.MinZ, 100, 0.5);
        AssertNear(9.5, state.MinZ, "Min Z clamp");
    }

    private static void TestLargeCoordinateBounds()
    {
        var a = new Bounds3D(new Vector3(1000000, 5000000, 100000), new Vector3(1000010, 5000020, 100030));
        var b = new Bounds3D(new Vector3(999990, 5000010, 99990), new Vector3(1000020, 5000030, 100040));
        var combined = SectionBoxMath.Union(a, b);
        AssertNear(999990, combined.Min.X, "Large coordinate min X");
        AssertNear(5000030, combined.Max.Y, "Large coordinate max Y");
    }

    private static void AssertNear(double expected, double actual, string name)
    {
        if (Math.Abs(expected - actual) > 1e-8) throw new InvalidOperationException(name + ": expected " + expected + ", got " + actual);
    }
}
