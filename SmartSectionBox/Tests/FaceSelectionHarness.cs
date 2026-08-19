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

            var underlayProbe = Probe(true, SectionBoxFaceId.MinZ, SectionBoxFaceId.MinY, SectionBoxFaceId.MaxX);
            Assert(tester.SelectCandidate(underlayProbe, 300, 400).Face.Id == SectionBoxFaceId.MinZ,
                "Ctrl face selection must choose the nearest underlay candidate.");
            Assert(underlayProbe.IsUnderlaySelection, "Ctrl face selection must use the underlay set.");

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
