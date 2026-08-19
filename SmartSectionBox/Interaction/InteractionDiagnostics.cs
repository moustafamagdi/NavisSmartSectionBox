using System;
using System.Globalization;
using System.Linq;
using System.Text;
using SmartSectionBox.Core;
using SmartSectionBox.Infrastructure;

namespace SmartSectionBox.Interaction
{
    /// <summary>
    /// Records concise, opt-in interaction traces. It intentionally logs only box geometry,
    /// pointer coordinates, and face-picking decisions—never model properties or selection data.
    /// </summary>
    internal static class InteractionDiagnostics
    {
        public static bool Enabled { get; set; }

        public static void LogPointerDown(int x, int y, FaceHitProbe probe, SectionBoxState state, bool captured)
        {
            if (!Enabled) return;
            var builder = new StringBuilder("FACE_DIAGNOSTIC POINTER_DOWN ");
            builder.Append("screen=").Append(Point(x, y));
            builder.Append(" captured=").Append(captured);
            builder.Append(" selected=").Append(FaceName(probe == null ? null : probe.Selected));
            builder.Append(" box=").Append(Box(state));
            builder.Append(" candidates=").Append(Candidates(probe));
            Logger.Info(builder.ToString());
        }

        public static void LogDragBegin(int x, int y, FaceHitResult selected, ScreenPoint normal, double coordinate)
        {
            if (!Enabled) return;
            Logger.Info("FACE_DIAGNOSTIC DRAG_BEGIN screen=" + Point(x, y) +
                        " face=" + FaceName(selected) +
                        " coordinate=" + Number(coordinate) +
                        " screenNormal=" + Point(normal.X, normal.Y));
        }

        public static void LogDragEnd(int startX, int startY, int endX, int endY, SectionBoxFaceId face, double initialCoordinate, double finalCoordinate, bool applied)
        {
            if (!Enabled) return;
            Logger.Info("FACE_DIAGNOSTIC DRAG_END start=" + Point(startX, startY) +
                        " end=" + Point(endX, endY) +
                        " face=" + face +
                        " initial=" + Number(initialCoordinate) +
                        " final=" + Number(finalCoordinate) +
                        " applied=" + applied);
        }

        public static void LogDragCancel(SectionBoxFaceId face, double restoredCoordinate)
        {
            if (!Enabled) return;
            Logger.Info("FACE_DIAGNOSTIC DRAG_CANCEL face=" + face + " restored=" + Number(restoredCoordinate));
        }

        private static string Candidates(FaceHitProbe probe)
        {
            if (probe == null || probe.Candidates == null || probe.Candidates.Count == 0) return "[]";
            return "[" + string.Join(" | ", probe.Candidates.Select(candidate =>
                FaceName(candidate) +
                " inside=" + candidate.IsInsidePolygon +
                " dist=" + Number(candidate.DistanceToPolygon) +
                " depth=" + Number(candidate.AverageDepth) +
                " poly=" + Polygon(candidate.Polygon))) + "]";
        }

        private static string FaceName(FaceHitResult result)
        {
            return result == null || result.Face == null ? "none" : result.Face.Id.ToString();
        }

        private static string Box(SectionBoxState state)
        {
            if (state == null) return "none";
            return "min=" + Point(state.MinX, state.MinY, state.MinZ) + " max=" + Point(state.MaxX, state.MaxY, state.MaxZ) +
                   " rotation=" + Point(state.RotationX, state.RotationY, state.RotationZ) + " enabled=" + state.Enabled;
        }

        private static string Polygon(ScreenPoint[] polygon)
        {
            if (polygon == null || polygon.Length == 0) return "none";
            return "(" + string.Join(";", polygon.Select(point => Point(point.X, point.Y) + "@" + Number(point.Depth))) + ")";
        }

        private static string Point(double x, double y)
        {
            return "(" + Number(x) + "," + Number(y) + ")";
        }

        private static string Point(double x, double y, double z)
        {
            return "(" + Number(x) + "," + Number(y) + "," + Number(z) + ")";
        }

        private static string Number(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
