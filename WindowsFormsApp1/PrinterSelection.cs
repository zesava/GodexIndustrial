using System.Windows.Forms;

namespace GodexIndustrial
{
    internal static class PrinterSelection
    {
        public static string Choose(ComboBox.ObjectCollection items, string preferred, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(preferred) && items.Contains(preferred))
                return preferred;
            if (!string.IsNullOrWhiteSpace(fallback) && items.Contains(fallback))
                return fallback;
            return items.Count > 0 ? items[0] as string : null;
        }
    }

}
