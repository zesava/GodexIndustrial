using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace GodexIndustrial
{
    // Reads the selected Windows face directly from GDI; no font-file picker or
    // assumptions about the Fonts folder or registry paths are needed.
    internal static class InstalledFontData
    {
        private const uint GdiError = 0xFFFFFFFF;

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern uint GetFontData(IntPtr hdc, uint table, uint offset,
            IntPtr buffer, uint length);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern uint GetFontData(IntPtr hdc, uint table, uint offset,
            [Out] byte[] buffer, uint length);

        internal static byte[] ReadTrueType(Font selectedFont)
        {
            if (selectedFont == null) throw new ArgumentNullException(nameof(selectedFont));

            IntPtr fontHandle = IntPtr.Zero;
            IntPtr dc = IntPtr.Zero;
            IntPtr previous = IntPtr.Zero;
            try
            {
                fontHandle = selectedFont.ToHfont();
                dc = CreateCompatibleDC(IntPtr.Zero);
                if (dc == IntPtr.Zero)
                    throw new IOException("Не вдалося прочитати вибраний шрифт Windows.");
                previous = SelectObject(dc, fontHandle);
                if (previous == IntPtr.Zero || previous == new IntPtr(-1))
                    throw new IOException("Не вдалося активувати вибраний шрифт Windows.");

                uint length = GetFontData(dc, 0, 0, IntPtr.Zero, 0);
                if (length == GdiError)
                    throw new NotSupportedException(
                        "Цей шрифт не містить доступних даних TrueType. Виберіть інший шрифт.");
                if (length < 12 || length > 64 * 1024 * 1024)
                    throw new InvalidDataException("Некоректний розмір системного шрифту.");

                var data = new byte[(int)length];
                if (GetFontData(dc, 0, 0, data, length) != length)
                    throw new IOException("Не вдалося повністю прочитати вибраний шрифт Windows.");
                if (!((data[0] == 0 && data[1] == 1 && data[2] == 0 && data[3] == 0) ||
                      (data[0] == 't' && data[1] == 'r' && data[2] == 'u' && data[3] == 'e')))
                    throw new NotSupportedException(
                        "Принтер підтримує TrueType (.ttf); вибраний шрифт має інший формат.");
                return data;
            }
            finally
            {
                if (dc != IntPtr.Zero && previous != IntPtr.Zero && previous != new IntPtr(-1))
                    SelectObject(dc, previous);
                if (dc != IntPtr.Zero) DeleteDC(dc);
                if (fontHandle != IntPtr.Zero) DeleteObject(fontHandle);
            }
        }
    }
}
