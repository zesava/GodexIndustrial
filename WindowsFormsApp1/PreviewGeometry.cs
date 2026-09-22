using System;
using System.Drawing;

namespace GodexIndustrial
{
    internal static class PreviewGeometry
    {
        public static float DotsPerMillimeter(int dpi)
        {
            switch (dpi)
            {
                case 203: return 8f;
                case 300: return 12f;
                default: throw new ArgumentOutOfRangeException(nameof(dpi));
            }
        }

        public static SizeF LabelSizeDots(LabelTemplate template, int dpi)
        {
            float dots = DotsPerMillimeter(dpi);
            return new SizeF(template.LabelWidth * dots, template.LabelLength * dots);
        }

        public static float GapDots(LabelTemplate template, int dpi)
        {
            return template.LabelGap * DotsPerMillimeter(dpi);
        }

        public static RectangleF TextBounds(LabelTemplate template, int column, SizeF textSize)
        {
            return LabelRotation.RotatedBounds(
                template.XOffsets[column], template.YOffset,
                textSize.Width, textSize.Height, template.Rotation);
        }

        public static bool FitsOnLabel(LabelTemplate template, int dpi, RectangleF bounds)
        {
            SizeF size = LabelSizeDots(template, dpi);
            const float tolerance = 0.01f;
            return bounds.Left >= -tolerance && bounds.Top >= -tolerance &&
                bounds.Right <= size.Width + tolerance && bounds.Bottom <= size.Height + tolerance;
        }
    }
}
