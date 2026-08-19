using System;
using SmartSectionBox.Core;

internal static class FaceCoordinateMutationHarness
{
    private static int Main()
    {
        try
        {
            VerifyEachFaceMovesWithoutDeformingTheOrientedBox();
            VerifyMinimumThicknessKeepsTheOppositeFaceFixed();
            Console.WriteLine("All oriented per-face motion tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void VerifyEachFaceMovesWithoutDeformingTheOrientedBox()
    {
        VerifyFace(SectionBoxFaceId.MinX);
        VerifyFace(SectionBoxFaceId.MaxX);
        VerifyFace(SectionBoxFaceId.MinY);
        VerifyFace(SectionBoxFaceId.MaxY);
        VerifyFace(SectionBoxFaceId.MinZ);
        VerifyFace(SectionBoxFaceId.MaxZ);
    }

    private static void VerifyFace(SectionBoxFaceId id)
    {
        var initial = CreateState();
        var before = FindFace(initial, id);
        var oppositeBefore = FindFace(initial, Opposite(id));
        var originalExtent = AxisExtent(initial, before.Axis);
        const double outwardDistance = 7.5;

        var updated = initial.Clone();
        SectionBoxMath.MoveFaceAlongOutwardNormal(updated, id, outwardDistance, 0.001);
        var after = FindFace(updated, id);
        var oppositeAfter = FindFace(updated, Opposite(id));

        AssertVector(after.Center - before.Center, before.Normal.Normalized() * outwardDistance,
            id + " must translate exactly along its outward world normal.");
        AssertVector(oppositeAfter.Center, oppositeBefore.Center,
            Opposite(id) + " must remain stationary while moving " + id + ".");
        for (var i = 0; i < before.Corners.Length; i++)
        {
            AssertVector(after.Corners[i] - before.Corners[i], before.Normal.Normalized() * outwardDistance,
                id + " corner " + i + " must translate with the selected face.");
            AssertVector(oppositeAfter.Corners[i], oppositeBefore.Corners[i],
                Opposite(id) + " corner " + i + " must remain stationary.");
        }

        Assert(NearlyEqual(AxisExtent(updated, before.Axis), originalExtent + outwardDistance),
            id + " must increase only its own local extent.");
        AssertOtherExtentsUnchanged(initial, updated, before.Axis, id);
        Assert(NearlyEqual(initial.RotationX, updated.RotationX), id + " must preserve RotationX.");
        Assert(NearlyEqual(initial.RotationY, updated.RotationY), id + " must preserve RotationY.");
        Assert(NearlyEqual(initial.RotationZ, updated.RotationZ), id + " must preserve RotationZ.");
    }

    private static void VerifyMinimumThicknessKeepsTheOppositeFaceFixed()
    {
        var initial = CreateState();
        var minimumThickness = 4.0;
        var initialExtent = AxisExtent(initial, SectionBoxAxis.X);
        var oppositeBefore = FindFace(initial, SectionBoxFaceId.MinX);
        var updated = initial.Clone();

        SectionBoxMath.MoveFaceAlongOutwardNormal(updated, SectionBoxFaceId.MaxX, -1000.0, minimumThickness);
        var oppositeAfter = FindFace(updated, SectionBoxFaceId.MinX);
        Assert(NearlyEqual(AxisExtent(updated, SectionBoxAxis.X), minimumThickness),
            "The captured axis must clamp to the requested minimum thickness.");
        AssertVector(oppositeAfter.Center, oppositeBefore.Center,
            "The opposite face must remain fixed when an inward pull clamps.");
        Assert(initialExtent > minimumThickness, "The test state must be thicker than the clamp.");
    }

    private static SectionBoxState CreateState()
    {
        return new SectionBoxState
        {
            Enabled = true,
            MinX = 100,
            MaxX = 200,
            MinY = 300,
            MaxY = 400,
            MinZ = 500,
            MaxZ = 600,
            RotationX = 17,
            RotationY = -22,
            RotationZ = 37
        };
    }

    private static SectionBoxFace FindFace(SectionBoxState state, SectionBoxFaceId id)
    {
        foreach (var face in SectionBoxMath.GetFaces(state))
        {
            if (face.Id == id) return face;
        }

        throw new InvalidOperationException("Face not found: " + id);
    }

    private static SectionBoxFaceId Opposite(SectionBoxFaceId id)
    {
        switch (id)
        {
            case SectionBoxFaceId.MinX: return SectionBoxFaceId.MaxX;
            case SectionBoxFaceId.MaxX: return SectionBoxFaceId.MinX;
            case SectionBoxFaceId.MinY: return SectionBoxFaceId.MaxY;
            case SectionBoxFaceId.MaxY: return SectionBoxFaceId.MinY;
            case SectionBoxFaceId.MinZ: return SectionBoxFaceId.MaxZ;
            case SectionBoxFaceId.MaxZ: return SectionBoxFaceId.MinZ;
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    private static double AxisExtent(SectionBoxState state, SectionBoxAxis axis)
    {
        switch (axis)
        {
            case SectionBoxAxis.X: return state.MaxX - state.MinX;
            case SectionBoxAxis.Y: return state.MaxY - state.MinY;
            case SectionBoxAxis.Z: return state.MaxZ - state.MinZ;
            default: throw new ArgumentOutOfRangeException(nameof(axis));
        }
    }

    private static void AssertOtherExtentsUnchanged(SectionBoxState initial, SectionBoxState updated, SectionBoxAxis changedAxis, SectionBoxFaceId id)
    {
        if (changedAxis != SectionBoxAxis.X)
        {
            Assert(NearlyEqual(AxisExtent(initial, SectionBoxAxis.X), AxisExtent(updated, SectionBoxAxis.X)), id + " must not change the local X extent.");
        }
        if (changedAxis != SectionBoxAxis.Y)
        {
            Assert(NearlyEqual(AxisExtent(initial, SectionBoxAxis.Y), AxisExtent(updated, SectionBoxAxis.Y)), id + " must not change the local Y extent.");
        }
        if (changedAxis != SectionBoxAxis.Z)
        {
            Assert(NearlyEqual(AxisExtent(initial, SectionBoxAxis.Z), AxisExtent(updated, SectionBoxAxis.Z)), id + " must not change the local Z extent.");
        }
    }

    private static void AssertVector(Vector3 actual, Vector3 expected, string message)
    {
        const double tolerance = 1e-8;
        if (Math.Abs(actual.X - expected.X) > tolerance ||
            Math.Abs(actual.Y - expected.Y) > tolerance ||
            Math.Abs(actual.Z - expected.Z) > tolerance)
        {
            throw new InvalidOperationException(message + " Actual=" + actual + " Expected=" + expected);
        }
    }

    private static bool NearlyEqual(double left, double right)
    {
        return Math.Abs(left - right) < 1e-9;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
