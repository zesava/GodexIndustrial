using System;
using System.Collections.Generic;
using System.Text;

namespace GodexIndustrial
{
    internal static class LabelCommandBuilder
    {
        public static string Build(LabelTemplate template, IEnumerable<string[]> rows)
        {
            TemplateManager.ValidateTemplate(template);
            var sb = new StringBuilder();
            sb.AppendLine($"^Q{template.LabelLength},{template.LabelGap}");
            sb.AppendLine($"^W{template.LabelWidth}");
            sb.AppendLine($"^H{template.Darkness}");
            sb.AppendLine("^P1");
            sb.AppendLine($"^S{template.PrintSpeed + 2}");
            sb.AppendLine("^AT");
            sb.AppendLine("^C1");
            sb.AppendLine("^R0");
            sb.AppendLine("~Q+0");
            sb.AppendLine("^O0");
            sb.AppendLine("^D0");
            sb.AppendLine("^E0");
            sb.AppendLine("~R255");
            sb.AppendLine("^XSET,ROTATION,0");
            int labelCount = 0;
            foreach (string[] row in rows)
            {
                if (row == null) continue;
                sb.AppendLine("^L");
                for (int i = 0; i < template.ColumnCount && i < row.Length; i++)
                {
                    string value = (row[i] ?? string.Empty).Trim().Replace("\r", "").Replace("\n", "");
                    if (value.Length == 0) continue;
                    if (value.IndexOfAny(new[] { '\0', '^', '~' }) >= 0)
                        throw new ArgumentException($"Label {labelCount + 1}, column {i + 1} contains a printer control character.");
                    int x = template.XOffsets[i];
                    sb.AppendLine($"ATA,{x},{template.YOffset},{template.FontSize},{template.FontSize},0,{template.Rotation}BE,A,0,{value}");
                }
                sb.AppendLine("E");
                labelCount++;
            }
            if (labelCount == 0) throw new ArgumentException("There are no labels to print.");
            return sb.ToString();
        }
    }
}
