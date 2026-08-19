using System;

namespace SmartSectionBox.Core
{
    /// <summary>
    /// UI-independent box state. Coordinates are always stored in Navisworks model units.
    /// Rotations are Navisworks degree values around local X, Y and Z, applied in X-Y-Z order.
    /// </summary>
    public sealed class SectionBoxState
    {
        public bool Enabled { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
        public double RotationX { get; set; }
        public double RotationY { get; set; }
        public double RotationZ { get; set; }

        /// <summary>Raw native clipping JSON from which this state was decoded.</summary>
        public string NativeJsonTemplate { get; set; }

        public SectionBoxState Clone()
        {
            return new SectionBoxState
            {
                Enabled = Enabled,
                MinX = MinX,
                MinY = MinY,
                MinZ = MinZ,
                MaxX = MaxX,
                MaxY = MaxY,
                MaxZ = MaxZ,
                RotationX = RotationX,
                RotationY = RotationY,
                RotationZ = RotationZ,
                NativeJsonTemplate = NativeJsonTemplate
            };
        }

        public SectionBoxState Normalized(double minimumThickness)
        {
            var copy = Clone();
            var t = Math.Max(minimumThickness, 1e-9);
            copy.MaxX = Math.Max(copy.MaxX, copy.MinX + t);
            copy.MaxY = Math.Max(copy.MaxY, copy.MinY + t);
            copy.MaxZ = Math.Max(copy.MaxZ, copy.MinZ + t);
            return copy;
        }

        public double GetFaceCoordinate(SectionBoxFaceId face)
        {
            switch (face)
            {
                case SectionBoxFaceId.MinX: return MinX;
                case SectionBoxFaceId.MaxX: return MaxX;
                case SectionBoxFaceId.MinY: return MinY;
                case SectionBoxFaceId.MaxY: return MaxY;
                case SectionBoxFaceId.MinZ: return MinZ;
                case SectionBoxFaceId.MaxZ: return MaxZ;
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        public void SetFaceCoordinate(SectionBoxFaceId face, double value, double minimumThickness)
        {
            var t = Math.Max(minimumThickness, 1e-9);
            switch (face)
            {
                case SectionBoxFaceId.MinX: MinX = Math.Min(value, MaxX - t); break;
                case SectionBoxFaceId.MaxX: MaxX = Math.Max(value, MinX + t); break;
                case SectionBoxFaceId.MinY: MinY = Math.Min(value, MaxY - t); break;
                case SectionBoxFaceId.MaxY: MaxY = Math.Max(value, MinY + t); break;
                case SectionBoxFaceId.MinZ: MinZ = Math.Min(value, MaxZ - t); break;
                case SectionBoxFaceId.MaxZ: MaxZ = Math.Max(value, MinZ + t); break;
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }
    }
}
