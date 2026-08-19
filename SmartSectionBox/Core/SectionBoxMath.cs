using System;
using System.Collections.Generic;

namespace SmartSectionBox.Core
{
    public static class SectionBoxMath
    {
        // Local-corner ordering: x changes fastest, then y, then z.
        public static Vector3[] GetCorners(SectionBoxState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var local = new[]
            {
                new Vector3(state.MinX, state.MinY, state.MinZ), // 0
                new Vector3(state.MaxX, state.MinY, state.MinZ), // 1
                new Vector3(state.MaxX, state.MaxY, state.MinZ), // 2
                new Vector3(state.MinX, state.MaxY, state.MinZ), // 3
                new Vector3(state.MinX, state.MinY, state.MaxZ), // 4
                new Vector3(state.MaxX, state.MinY, state.MaxZ), // 5
                new Vector3(state.MaxX, state.MaxY, state.MaxZ), // 6
                new Vector3(state.MinX, state.MaxY, state.MaxZ)  // 7
            };

            var centre = new Vector3(
                (state.MinX + state.MaxX) * 0.5,
                (state.MinY + state.MaxY) * 0.5,
                (state.MinZ + state.MaxZ) * 0.5);

            for (var i = 0; i < local.Length; i++)
            {
                local[i] = RotateLocal(local[i] - centre, state) + centre;
            }

            return local;
        }

        public static IReadOnlyList<SectionBoxFace> GetFaces(SectionBoxState state)
        {
            var corners = GetCorners(state);
            return new[]
            {
                CreateFace(SectionBoxFaceId.MinX, SectionBoxAxis.X, false, new[] { 0, 3, 7, 4 }, corners, state),
                CreateFace(SectionBoxFaceId.MaxX, SectionBoxAxis.X, true, new[] { 1, 5, 6, 2 }, corners, state),
                CreateFace(SectionBoxFaceId.MinY, SectionBoxAxis.Y, false, new[] { 0, 4, 5, 1 }, corners, state),
                CreateFace(SectionBoxFaceId.MaxY, SectionBoxAxis.Y, true, new[] { 3, 2, 6, 7 }, corners, state),
                CreateFace(SectionBoxFaceId.MinZ, SectionBoxAxis.Z, false, new[] { 0, 1, 2, 3 }, corners, state),
                CreateFace(SectionBoxFaceId.MaxZ, SectionBoxAxis.Z, true, new[] { 4, 7, 6, 5 }, corners, state)
            };
        }

        public static Vector3 LocalAxisNormal(SectionBoxAxis axis, bool positiveSide)
        {
            var sign = positiveSide ? 1.0 : -1.0;
            switch (axis)
            {
                case SectionBoxAxis.X: return new Vector3(sign, 0, 0);
                case SectionBoxAxis.Y: return new Vector3(0, sign, 0);
                case SectionBoxAxis.Z: return new Vector3(0, 0, sign);
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        public static Vector3 RotateLocal(Vector3 localVector, SectionBoxState state)
        {
            // Navisworks serializes OrientedBox3D rotations in degrees. Convert at the
            // geometry boundary so JSON, state, face hit-testing, and drag updates agree.
            var x = RotateX(localVector, DegreesToRadians(state.RotationX));
            var y = RotateY(x, DegreesToRadians(state.RotationY));
            return RotateZ(y, DegreesToRadians(state.RotationZ));
        }

        public static Vector3 InverseRotateLocal(Vector3 worldVector, SectionBoxState state)
        {
            var z = RotateZ(worldVector, -DegreesToRadians(state.RotationZ));
            var y = RotateY(z, -DegreesToRadians(state.RotationY));
            return RotateX(y, -DegreesToRadians(state.RotationX));
        }

        /// <summary>
        /// Moves one oriented-box face along its outward world normal while preserving the
        /// opposite oriented face. Navisworks stores Min/Max as a box centre and extents in a
        /// world-coordinate payload plus a rotation. Therefore a rotated X/Y face pull must move
        /// the stored centre along the rotated normal as well as change the relevant half-extent;
        /// changing one raw Min/Max component alone distorts the rendered native box.
        /// </summary>
        public static void MoveFaceAlongOutwardNormal(
            SectionBoxState state,
            SectionBoxFaceId faceId,
            double outwardDistance,
            double minimumThickness)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            SectionBoxAxis axis;
            bool positiveSide;
            FaceDefinition(faceId, out axis, out positiveSide);

            var halfX = Math.Max(0, (state.MaxX - state.MinX) * 0.5);
            var halfY = Math.Max(0, (state.MaxY - state.MinY) * 0.5);
            var halfZ = Math.Max(0, (state.MaxZ - state.MinZ) * 0.5);
            var minimumHalfExtent = Math.Max(minimumThickness, 1e-9) * 0.5;
            var requestedHalfExtent = HalfExtent(axis, halfX, halfY, halfZ) + outwardDistance * 0.5;
            var resultingHalfExtent = Math.Max(minimumHalfExtent, requestedHalfExtent);

            // The clamp can shorten a very large inward request. Use the actual applied motion
            // for the centre shift so the stationary opposite face remains stationary even at the
            // minimum-thickness limit.
            var appliedOutwardDistance = (resultingHalfExtent - HalfExtent(axis, halfX, halfY, halfZ)) * 2.0;
            SetHalfExtent(axis, resultingHalfExtent, ref halfX, ref halfY, ref halfZ);

            var rawCentre = new Vector3(
                (state.MinX + state.MaxX) * 0.5,
                (state.MinY + state.MaxY) * 0.5,
                (state.MinZ + state.MaxZ) * 0.5);
            var outwardNormal = RotateLocal(LocalAxisNormal(axis, positiveSide), state).Normalized();
            var shiftedCentre = rawCentre + outwardNormal * (appliedOutwardDistance * 0.5);

            state.MinX = shiftedCentre.X - halfX;
            state.MaxX = shiftedCentre.X + halfX;
            state.MinY = shiftedCentre.Y - halfY;
            state.MaxY = shiftedCentre.Y + halfY;
            state.MinZ = shiftedCentre.Z - halfZ;
            state.MaxZ = shiftedCentre.Z + halfZ;
        }

        public static Bounds3D Union(Bounds3D first, Bounds3D second)
        {
            if (!first.IsValid) return second;
            if (!second.IsValid) return first;
            return new Bounds3D(
                new Vector3(Math.Min(first.Min.X, second.Min.X), Math.Min(first.Min.Y, second.Min.Y), Math.Min(first.Min.Z, second.Min.Z)),
                new Vector3(Math.Max(first.Max.X, second.Max.X), Math.Max(first.Max.Y, second.Max.Y), Math.Max(first.Max.Z, second.Max.Z)));
        }

        public static Bounds3D Expand(Bounds3D bounds, double padding)
        {
            var p = Math.Max(0, padding);
            return new Bounds3D(bounds.Min - new Vector3(p, p, p), bounds.Max + new Vector3(p, p, p));
        }

        private static double HalfExtent(SectionBoxAxis axis, double halfX, double halfY, double halfZ)
        {
            switch (axis)
            {
                case SectionBoxAxis.X: return halfX;
                case SectionBoxAxis.Y: return halfY;
                case SectionBoxAxis.Z: return halfZ;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        private static void SetHalfExtent(SectionBoxAxis axis, double value, ref double halfX, ref double halfY, ref double halfZ)
        {
            switch (axis)
            {
                case SectionBoxAxis.X: halfX = value; return;
                case SectionBoxAxis.Y: halfY = value; return;
                case SectionBoxAxis.Z: halfZ = value; return;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        private static void FaceDefinition(SectionBoxFaceId faceId, out SectionBoxAxis axis, out bool positiveSide)
        {
            switch (faceId)
            {
                case SectionBoxFaceId.MinX: axis = SectionBoxAxis.X; positiveSide = false; return;
                case SectionBoxFaceId.MaxX: axis = SectionBoxAxis.X; positiveSide = true; return;
                case SectionBoxFaceId.MinY: axis = SectionBoxAxis.Y; positiveSide = false; return;
                case SectionBoxFaceId.MaxY: axis = SectionBoxAxis.Y; positiveSide = true; return;
                case SectionBoxFaceId.MinZ: axis = SectionBoxAxis.Z; positiveSide = false; return;
                case SectionBoxFaceId.MaxZ: axis = SectionBoxAxis.Z; positiveSide = true; return;
                default: throw new ArgumentOutOfRangeException(nameof(faceId));
            }
        }

        private static SectionBoxFace CreateFace(
            SectionBoxFaceId id,
            SectionBoxAxis axis,
            bool positiveSide,
            int[] indices,
            Vector3[] allCorners,
            SectionBoxState state)
        {
            var faceCorners = new Vector3[4];
            var centre = new Vector3(0, 0, 0);
            for (var i = 0; i < indices.Length; i++)
            {
                faceCorners[i] = allCorners[indices[i]];
                centre += faceCorners[i];
            }

            return new SectionBoxFace
            {
                Id = id,
                Axis = axis,
                PositiveSide = positiveSide,
                CornerIndices = indices,
                Corners = faceCorners,
                Center = centre / 4.0,
                Normal = RotateLocal(LocalAxisNormal(axis, positiveSide), state).Normalized()
            };
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static Vector3 RotateX(Vector3 p, double radians)
        {
            var c = Math.Cos(radians);
            var s = Math.Sin(radians);
            return new Vector3(p.X, c * p.Y - s * p.Z, s * p.Y + c * p.Z);
        }

        private static Vector3 RotateY(Vector3 p, double radians)
        {
            var c = Math.Cos(radians);
            var s = Math.Sin(radians);
            return new Vector3(c * p.X + s * p.Z, p.Y, -s * p.X + c * p.Z);
        }

        private static Vector3 RotateZ(Vector3 p, double radians)
        {
            var c = Math.Cos(radians);
            var s = Math.Sin(radians);
            return new Vector3(c * p.X - s * p.Y, s * p.X + c * p.Y, p.Z);
        }
    }

    public struct Bounds3D
    {
        public Bounds3D(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public Vector3 Min { get; }
        public Vector3 Max { get; }
        public bool IsValid => Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;
    }
}
