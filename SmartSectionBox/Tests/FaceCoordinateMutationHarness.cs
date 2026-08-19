using System;
using SmartSectionBox.Core;
using SmartSectionBox.Interaction;

internal static class FaceCoordinateMutationHarness
{
    private static int Main()
    {
        try
        {
            VerifyEachFaceMutatesOnlyItsOwnBound();
            Console.WriteLine("All per-face coordinate mutation tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void VerifyEachFaceMutatesOnlyItsOwnBound()
    {
        VerifyFace(SectionBoxFaceId.MinX, SectionBoxAxis.X, false);
        VerifyFace(SectionBoxFaceId.MaxX, SectionBoxAxis.X, true);
        VerifyFace(SectionBoxFaceId.MinY, SectionBoxAxis.Y, false);
        VerifyFace(SectionBoxFaceId.MaxY, SectionBoxAxis.Y, true);
        VerifyFace(SectionBoxFaceId.MinZ, SectionBoxAxis.Z, false);
        VerifyFace(SectionBoxFaceId.MaxZ, SectionBoxAxis.Z, true);
    }

    private static void VerifyFace(SectionBoxFaceId id, SectionBoxAxis axis, bool positiveSide)
    {
        var face = new SectionBoxFace { Id = id, Axis = axis, PositiveSide = positiveSide };
        var initial = CreateState();
        var updated = initial.Clone();
        var outwardOffset = 7.5;
        var coordinateDelta = DragController.FaceCoordinateDeltaFromNormalOffset(face, outwardOffset);
        updated.SetFaceCoordinate(id, initial.GetFaceCoordinate(id) + coordinateDelta, 0.001);

        var expected = positiveSide
            ? initial.GetFaceCoordinate(id) + outwardOffset
            : initial.GetFaceCoordinate(id) - outwardOffset;
        Assert(NearlyEqual(updated.GetFaceCoordinate(id), expected), id + " must move in its outward normal direction.");
        AssertUnchangedExcept(initial, updated, id);
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
            RotationX = 37,
            RotationY = -22,
            RotationZ = 11
        };
    }

    private static void AssertUnchangedExcept(SectionBoxState initial, SectionBoxState updated, SectionBoxFaceId moved)
    {
        Assert(moved == SectionBoxFaceId.MinX || NearlyEqual(initial.MinX, updated.MinX), "MinX changed while moving " + moved);
        Assert(moved == SectionBoxFaceId.MaxX || NearlyEqual(initial.MaxX, updated.MaxX), "MaxX changed while moving " + moved);
        Assert(moved == SectionBoxFaceId.MinY || NearlyEqual(initial.MinY, updated.MinY), "MinY changed while moving " + moved);
        Assert(moved == SectionBoxFaceId.MaxY || NearlyEqual(initial.MaxY, updated.MaxY), "MaxY changed while moving " + moved);
        Assert(moved == SectionBoxFaceId.MinZ || NearlyEqual(initial.MinZ, updated.MinZ), "MinZ changed while moving " + moved);
        Assert(moved == SectionBoxFaceId.MaxZ || NearlyEqual(initial.MaxZ, updated.MaxZ), "MaxZ changed while moving " + moved);
        Assert(NearlyEqual(initial.RotationX, updated.RotationX), "RotationX must remain unchanged.");
        Assert(NearlyEqual(initial.RotationY, updated.RotationY), "RotationY must remain unchanged.");
        Assert(NearlyEqual(initial.RotationZ, updated.RotationZ), "RotationZ must remain unchanged.");
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
