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

            VerifySlopedOrthographicPolygonContainment();
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

            private static void VerifySlopedOrthographicPolygonContainment()
        {
            // This is the visible MinY polygon shape captured from the user's Orthographic host
            // trace. Its downward-oriented edges require a signed scanline denominator.
            var polygon = new List<ScreenPoint>
            {
                new ScreenPoint(562, 220, 0),
                new ScreenPoint(562, 350, 0),
                new ScreenPoint(951, 516, 0),
                new ScreenPoint(951, 387, 0)
            };
            Assert(FaceHitTester.PointInPolygon(new ScreenPoint(733, 344, 0), polygon),
                "A point inside a sloped Orthographic Y-face polygon must be selectable.");
            Assert(!FaceHitTester.PointInPolygon(new ScreenPoint(733, 180, 0), polygon),
                "A point above a sloped Orthographic Y-face polygon must not be selectable.");

            polygon.Reverse();
            Assert(FaceHitTester.PointInPolygon(new ScreenPoint(733, 344, 0), polygon),
                "Polygon winding must not change Orthographic face containment.");
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
