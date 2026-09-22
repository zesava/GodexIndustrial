using System;
using System.Drawing;

namespace GodexIndustrial
{
    internal static class LabelRotation
    {
        public static readonly string[] Labels =
        {
            "0° (normal)", "90°", "180°", "270°"
        };

        public static int Degrees(int value)
        {
            if (value < 0 || value >= Labels.Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            return value * 90;
        }

        public static RectangleF RotatedBounds(float x, float y, float width, float height, int value)
        {
            Degrees(value);
            PointF[] corners =
            {
                Rotate(0, 0, value),
                Rotate(width, 0, value),
                Rotate(0, height, value),
                Rotate(width, height, value)
            };
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (PointF corner in corners)
            {
                minX = Math.Min(minX, x + corner.X);
                minY = Math.Min(minY, y + corner.Y);
                maxX = Math.Max(maxX, x + corner.X);
                maxY = Math.Max(maxY, y + corner.Y);
            }
            return RectangleF.FromLTRB(minX, minY, maxX, maxY);
        }

        private static PointF Rotate(float x, float y, int value)
        {
            switch (value)
            {
                case 0: return new PointF(x, y);
                case 1: return new PointF(-y, x);
                case 2: return new PointF(-x, -y);
                default: return new PointF(y, -x);
            }
        }
    }
}
