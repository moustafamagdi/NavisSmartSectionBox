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

            var frontProbe = Probe(SectionBoxFaceId.MaxZ, SectionBoxFaceId.MaxY, SectionBoxFaceId.MinX);
            Assert(tester.SelectCandidate(frontProbe).Face.Id == SectionBoxFaceId.MaxZ,
                "Front-facing selection must choose the nearest ordered candidate.");
            Assert(frontProbe.SelectedIndex == 0, "The selected front-facing candidate must be recorded at index zero.");

            var repeatProbe = Probe(SectionBoxFaceId.MaxZ, SectionBoxFaceId.MaxY, SectionBoxFaceId.MinX);
            Assert(tester.SelectCandidate(repeatProbe).Face.Id == SectionBoxFaceId.MaxZ,
                "A repeated click must not cycle through occluded candidates.");
            Assert(repeatProbe.SelectedIndex == 0,
                "A repeated click must preserve deterministic nearest-face selection.");

            var empty = new FaceHitProbe { Candidates = new List<FaceHitResult>() };
            Assert(tester.SelectCandidate(empty) == null, "An empty front-facing candidate set must not select a face.");

            Console.WriteLine("All front-facing-only face-selection tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static FaceHitProbe Probe(params SectionBoxFaceId[] faceIds)
    {
        var candidates = new List<FaceHitResult>();
        foreach (var faceId in faceIds)
        {
            candidates.Add(new FaceHitResult { Face = new SectionBoxFace { Id = faceId } });
        }

        return new FaceHitProbe { Candidates = candidates };
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
