using System;
using System.Collections.Generic;
using SmartSectionBox.Core;
using SmartSectionBox.Interaction;

internal static class FaceSelectionHarness
{
    private static int Main()
    {
        try
        {
            var tester = new FaceHitTester(new CameraProjection());

            var frontProbe = Probe(false, SectionBoxFaceId.MaxZ, SectionBoxFaceId.MaxY, SectionBoxFaceId.MinX);
            Assert(tester.SelectCandidate(frontProbe, 300, 400).Face.Id == SectionBoxFaceId.MaxZ,
                "Normal face selection must choose the nearest camera-facing candidate.");
            Assert(!frontProbe.IsUnderlaySelection, "Normal face selection must use the front set.");

            var underlayProbe1 = Probe(true, SectionBoxFaceId.MinZ, SectionBoxFaceId.MinY, SectionBoxFaceId.MaxX);
            Assert(tester.SelectCandidate(underlayProbe1, 300, 400).Face.Id == SectionBoxFaceId.MinZ,
                "The first Ctrl face selection must choose the nearest underlay candidate.");
            Assert(underlayProbe1.IsUnderlaySelection, "Ctrl face selection must use the underlay set.");

            var underlayProbe2 = Probe(true, SectionBoxFaceId.MinZ, SectionBoxFaceId.MinY, SectionBoxFaceId.MaxX);
            Assert(tester.SelectCandidate(underlayProbe2, 304, 406).Face.Id == SectionBoxFaceId.MinY,
                "A repeated Ctrl click in the cycle window must advance to the next underlay face.");
            Assert(underlayProbe2.SelectedIndex == 1, "The second Ctrl selection must record the cycled candidate index.");

            var underlayProbe3 = Probe(true, SectionBoxFaceId.MinZ, SectionBoxFaceId.MinY, SectionBoxFaceId.MaxX);
            Assert(tester.SelectCandidate(underlayProbe3, 304, 406).Face.Id == SectionBoxFaceId.MaxX,
                "A third repeated Ctrl click must advance deterministically through all underlay faces.");

            var movedProbe = Probe(true, SectionBoxFaceId.MinZ, SectionBoxFaceId.MinY, SectionBoxFaceId.MaxX);
            Assert(tester.SelectCandidate(movedProbe, 340, 406).Face.Id == SectionBoxFaceId.MinZ,
                "Moving outside the cycle window must restart Ctrl underlay selection at the nearest face.");

            Console.WriteLine("All front and underlay face-selection tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static FaceHitProbe Probe(bool isUnderlay, params SectionBoxFaceId[] faceIds)
    {
        var candidates = new List<FaceHitResult>();
        foreach (var faceId in faceIds)
        {
            candidates.Add(new FaceHitResult { Face = new SectionBoxFace { Id = faceId } });
        }

        return new FaceHitProbe { Candidates = candidates, IsUnderlaySelection = isUnderlay };
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
