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

            Assert(tester.SelectCandidate(Probe(), false, 300, 400).Face.Id == SectionBoxFaceId.MaxZ, "Default selection must choose the visible/front candidate.");
            Assert(tester.SelectCandidate(Probe(), true, 300, 400).Face.Id == SectionBoxFaceId.MaxY, "First Ctrl selection must choose the first underlay candidate.");
            Assert(tester.SelectCandidate(Probe(), true, 300, 400).Face.Id == SectionBoxFaceId.MinX, "Repeated Ctrl selection must cycle to the next underlay candidate.");
            Assert(tester.SelectCandidate(Probe(), true, 600, 400).Face.Id == SectionBoxFaceId.MaxY, "A new Ctrl click location must begin with the first underlay candidate.");

            Console.WriteLine("All Ctrl underlay face-selection tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static FaceHitProbe Probe()
    {
        return new FaceHitProbe
        {
            Candidates = new List<FaceHitResult>
            {
                Candidate(SectionBoxFaceId.MaxZ),
                Candidate(SectionBoxFaceId.MaxY),
                Candidate(SectionBoxFaceId.MinX)
            }
        };
    }

    private static FaceHitResult Candidate(SectionBoxFaceId id)
    {
        return new FaceHitResult { Face = new SectionBoxFace { Id = id } };
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
