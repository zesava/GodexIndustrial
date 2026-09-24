using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GodexIndustrial
{
    public sealed class PrinterFontInfo
    {
        public string Type { get; }
        public string Name { get; }
        public string Size { get; }

        public PrinterFontInfo(string type, string name, string size)
        {
            Type = type;
            Name = name;
            Size = size;
        }

        public bool TryGetPrintSlot(out char slot)
        {
            slot = '\0';
            if (Type == "TTF" && !string.IsNullOrEmpty(Name) && Name.Length >= 2 &&
                Name[0] >= 'A' && Name[0] <= 'Z' && Name[1] == ':')
            {
                slot = Name[0];
                return true;
            }
            if (Type == "FNT" && !string.IsNullOrEmpty(Name) && Name.Length == 5 &&
                char.ToUpperInvariant(Name[0]) >= 'A' &&
                char.ToUpperInvariant(Name[0]) <= 'Z' &&
                Name.EndsWith(".FNT", StringComparison.OrdinalIgnoreCase))
            {
                slot = char.ToUpperInvariant(Name[0]);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return Type + " — " + Name;
        }

        public bool TryBuildDeleteCommand(out string command)
        {
            command = null;
            if (!TryGetPrintSlot(out char slot)) return false;
            command = (Type == "TTF" ? "~MDELC," : "~MDELE,") + slot;
            return true;
        }
    }

    public sealed class PrinterFontCatalog
    {
        private static readonly Regex BitmapFont = new Regex(
            @"^\s*(?<name>.+?)(?:\.FNT|\s+FNT)(?:\s+(?<size>\d+\s*(?:KB|BYTES?)))?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TrueTypeFont = new Regex(
            @"^\s*(?<name>.+?)(?:\.TTF|\s+TTF)(?:\s+(?<size>\d+\s*(?:KB|BYTES?)))?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FreeBytes = new Regex(
            @"(?<size>\d+)\s+bytes?\s*(?:\(s\))?\s+free",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FreeKilobytes = new Regex(
            @"FREE\s+MEMORY\s+SPACE\s*:?\s*(?<size>\d+)\s*KB",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IReadOnlyList<PrinterFontInfo> Fonts { get; }
        public string FreeMemoryKb { get; }

        private PrinterFontCatalog(List<PrinterFontInfo> fonts, string freeMemoryKb)
        {
            Fonts = fonts.AsReadOnly();
            FreeMemoryKb = freeMemoryKb;
        }

        public char FindAvailableSlot(string type)
        {
            if (type != "TTF" && type != "FNT")
                throw new ArgumentException("Font type must be TTF or FNT.", nameof(type));

            var used = new HashSet<char>();
            foreach (PrinterFontInfo font in Fonts)
                if (font.Type == type && font.TryGetPrintSlot(out char slot))
                    used.Add(slot);

            for (char slot = 'A'; slot <= 'Z'; slot++)
                if (!used.Contains(slot)) return slot;
            throw new InvalidOperationException("All 26 " + type + " font slots are occupied.");
        }

        public static PrinterFontCatalog ParseDirectory(string reply)
        {
            if (string.IsNullOrWhiteSpace(reply) ||
                (reply.IndexOf("MEMORY", StringComparison.OrdinalIgnoreCase) < 0 &&
                 reply.IndexOf("free", StringComparison.OrdinalIgnoreCase) < 0 &&
                 reply.IndexOf(" FNT", StringComparison.OrdinalIgnoreCase) < 0 &&
                 reply.IndexOf(".FNT", StringComparison.OrdinalIgnoreCase) < 0 &&
                 reply.IndexOf(" TTF", StringComparison.OrdinalIgnoreCase) < 0 &&
                 reply.IndexOf(".TTF", StringComparison.OrdinalIgnoreCase) < 0))
                throw new FormatException("Printer did not return a memory directory.");

            var fonts = new List<PrinterFontInfo>();
            foreach (string rawLine in reply.Split(new[] { "\r\n", "\n", "\r" },
                StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.IndexOf("TTF_TABLE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("TTF TABLE", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                Match bitmap = BitmapFont.Match(line);
                if (bitmap.Success)
                {
                    string name = bitmap.Groups["name"].Value.Trim();
                    fonts.Add(new PrinterFontInfo("FNT", name + ".FNT",
                        bitmap.Groups["size"].Value.Trim()));
                    continue;
                }

                Match trueType = TrueTypeFont.Match(line);
                if (!trueType.Success) continue;
                string fontName = Regex.Replace(trueType.Groups["name"].Value.Trim(),
                    @"\s*\(True Type\)$", string.Empty, RegexOptions.IgnoreCase);
                fonts.Add(new PrinterFontInfo("TTF", fontName,
                    trueType.Groups["size"].Value.Trim()));
            }

            Match freeBytes = FreeBytes.Match(reply);
            string freeMemory = string.Empty;
            if (freeBytes.Success &&
                long.TryParse(freeBytes.Groups["size"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out long bytes))
                freeMemory = (bytes / 1024).ToString(CultureInfo.InvariantCulture);
            else
            {
                Match freeKilobytes = FreeKilobytes.Match(reply.Replace("\r", " ").Replace("\n", " "));
                if (freeKilobytes.Success) freeMemory = freeKilobytes.Groups["size"].Value;
            }

            return new PrinterFontCatalog(fonts, freeMemory);
        }
    }
}