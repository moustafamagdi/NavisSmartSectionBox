using System;

namespace SmartSectionBox.Core
{
    public enum SectionBoxAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    public enum SectionBoxFaceId
    {
        MinX,
        MaxX,
        MinY,
        MaxY,
        MinZ,
        MaxZ
    }

    public sealed class SectionBoxFace
    {
        public SectionBoxFaceId Id { get; set; }
        public SectionBoxAxis Axis { get; set; }
        public bool PositiveSide { get; set; }
        public int[] CornerIndices { get; set; }
        public Vector3 Center { get; set; }
        public Vector3 Normal { get; set; }
        public Vector3[] Corners { get; set; }

        public string DisplayName
        {
            get
            {
                var sign = PositiveSide ? "MAX " : "MIN ";
                return sign + Axis.ToString().ToUpperInvariant();
            }
        }
    }

    public sealed class FaceHoverState
    {
        public static readonly FaceHoverState None = new FaceHoverState();

        public bool IsHovering { get; set; }
        public SectionBoxFaceId FaceId { get; set; }
        public SectionBoxAxis Axis { get; set; }
        public bool PositiveSide { get; set; }
        public double Coordinate { get; set; }

        public static FaceHoverState FromFace(SectionBoxFace face, double coordinate)
        {
            if (face == null) return None;
            return new FaceHoverState
            {
                IsHovering = true,
                FaceId = face.Id,
                Axis = face.Axis,
                PositiveSide = face.PositiveSide,
                Coordinate = coordinate
            };
        }
    }

    public struct Vector3
    {
        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vector3 Normalized()
        {
            var length = Length;
            return length < 1e-12 ? new Vector3(0, 0, 0) : this / length;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 a, double scalar) => new Vector3(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static Vector3 operator *(double scalar, Vector3 a) => a * scalar;
        public static Vector3 operator /(Vector3 a, double scalar) => new Vector3(a.X / scalar, a.Y / scalar, a.Z / scalar);

        public static double Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }
    }
}
