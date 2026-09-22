using System;
using System.Collections.Generic;
using System.IO;

namespace GodexIndustrial
{
    internal static class LabelDataParser
    {
        public static List<string[]> ParseTsv(string text, int columnCount)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("Clipboard is empty.");
            var rows = new List<string[]>();
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0 && reader.Peek() == -1) break;
                    string[] cells = line.Split('\t');
                    if (cells.Length > columnCount)
                        throw new ArgumentException($"Row {rows.Count + 1} has {cells.Length} columns; the template has {columnCount}.");
                    rows.Add(cells);
                }
            }
            if (rows.Count == 0) throw new ArgumentException("Clipboard has no rows.");
            return rows;
        }
    }
}
