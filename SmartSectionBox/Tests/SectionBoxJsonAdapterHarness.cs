using System;
using SmartSectionBox.Core;

internal static class SectionBoxJsonAdapterHarness
{
    private static int Main()
    {
        try
        {
            TestFallbackRoundTrip();
            TestDirectMinMaxPayload();
            TestNativeTemplatePreservation();
            Console.WriteLine("All isolated SectionBoxJsonAdapter tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void TestFallbackRoundTrip()
    {
        var adapter = new SectionBoxJsonAdapter();
        var source = new SectionBoxState { Enabled = true, MinX = 1, MinY = 2, MinZ = 3, MaxX = 4, MaxY = 5, MaxZ = 6, RotationZ = 0.25 };
        var json = adapter.Encode(source);
        SectionBoxState decoded;
        string diagnostic;
        Assert(adapter.TryDecode(json, out decoded, out diagnostic), diagnostic);
        AssertNear(1, decoded.MinX, "Fallback MinX");
        AssertNear(6, decoded.MaxZ, "Fallback MaxZ");
        AssertNear(0.25, decoded.RotationZ, "Fallback rotation");
    }

        private static void TestDirectMinMaxPayload()
        {
            const string native = "{\"Type\":\"ClipPlaneSet\",\"Min\":[-10,-20,-30],\"Max\":[10,20,30],\"Enabled\":true}";
            var adapter = new SectionBoxJsonAdapter();
            SectionBoxState state;
            string diagnostic;
            Assert(adapter.TryDecode(native, out state, out diagnostic), diagnostic);
            AssertNear(-10, state.MinX, "Direct payload MinX");
            AssertNear(30, state.MaxZ, "Direct payload MaxZ");
            state.MinY = -25;
            var encoded = adapter.Encode(state);
            Assert(encoded.Contains("\"Min\""), "Direct payload must retain Min point.");
            Assert(adapter.TryDecode(encoded, out state, out diagnostic), diagnostic);
            AssertNear(-25, state.MinY, "Updated direct payload MinY");
        }

        private static void TestNativeTemplatePreservation()

    {
        const string native = "{\"Type\":\"ClipPlaneSet\",\"Enabled\":true,\"Metadata\":\"KeepMe\",\"OrientedBox\":{\"Box\":[[10,20,30],[40,50,60]],\"Rotation\":[0,0,0]}}";
        var adapter = new SectionBoxJsonAdapter();
        SectionBoxState state;
        string diagnostic;
        Assert(adapter.TryDecode(native, out state, out diagnostic), diagnostic);
        state.MaxY = 55;
        state.Enabled = false;
        var encoded = adapter.Encode(state);
        Assert(encoded.Contains("KeepMe"), "Unknown native fields must be retained.");
        Assert(adapter.TryDecode(encoded, out state, out diagnostic), diagnostic);
        AssertNear(55, state.MaxY, "Updated native MaxY");
        Assert(!state.Enabled, "Updated native enabled state");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertNear(double expected, double actual, string name)
    {
        if (Math.Abs(expected - actual) > 1e-8) throw new InvalidOperationException(name + ": expected " + expected + ", got " + actual);
    }
}
