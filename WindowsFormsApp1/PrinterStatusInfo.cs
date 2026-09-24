using System;

namespace GodexIndustrial
{
    internal enum PrinterStatusKind
    {
        Ready,
        Busy,
        Error,
        Unknown
    }

    internal sealed class PrinterStatusInfo
    {
        private PrinterStatusInfo(string code, string description, PrinterStatusKind kind)
        {
            Code = code;
            Description = description;
            Kind = kind;
        }

        public string Code { get; }
        public string Description { get; }
        public PrinterStatusKind Kind { get; }

        public static PrinterStatusInfo Parse(string response)
        {
            string value = (response ?? string.Empty).Trim();
            if (value.Length < 2 || !char.IsDigit(value[0]) || !char.IsDigit(value[1]))
                return new PrinterStatusInfo(null, "Unknown status", PrinterStatusKind.Unknown);

            string code = value.Substring(0, 2);
            switch (code)
            {
                case "00": return new PrinterStatusInfo(code, "Ready", PrinterStatusKind.Ready);
                case "01":
                case "02": return new PrinterStatusInfo(code, "Media empty or jam", PrinterStatusKind.Error);
                case "03": return new PrinterStatusInfo(code, "Ribbon empty", PrinterStatusKind.Error);
                case "04": return new PrinterStatusInfo(code, "Printhead open", PrinterStatusKind.Error);
                case "05": return new PrinterStatusInfo(code, "Rewinder full", PrinterStatusKind.Error);
                case "06": return new PrinterStatusInfo(code, "File system full", PrinterStatusKind.Error);
                case "07": return new PrinterStatusInfo(code, "File not found", PrinterStatusKind.Error);
                case "08": return new PrinterStatusInfo(code, "Duplicate name", PrinterStatusKind.Error);
                case "09": return new PrinterStatusInfo(code, "Syntax error", PrinterStatusKind.Error);
                case "10": return new PrinterStatusInfo(code, "Cutter jam", PrinterStatusKind.Error);
                case "11": return new PrinterStatusInfo(code, "Memory not found", PrinterStatusKind.Error);
                case "20": return new PrinterStatusInfo(code, "Paused", PrinterStatusKind.Busy);
                case "21": return new PrinterStatusInfo(code, "Settings mode", PrinterStatusKind.Busy);
                case "22": return new PrinterStatusInfo(code, "Keyboard mode", PrinterStatusKind.Busy);
                case "50": return new PrinterStatusInfo(code, "Printing", PrinterStatusKind.Busy);
                case "60": return new PrinterStatusInfo(code, "Processing", PrinterStatusKind.Busy);
                default: return new PrinterStatusInfo(code, "Unknown status", PrinterStatusKind.Unknown);
            }
        }
    }
}
