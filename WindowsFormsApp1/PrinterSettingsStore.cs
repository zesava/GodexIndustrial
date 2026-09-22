using System;
using System.IO;
using System.Web.Script.Serialization;

namespace GodexIndustrial
{
    internal sealed class SavedPrinterSettings
    {
        public int ConnectionType { get; set; } = 1;
        public string IpAddress { get; set; } = "172.16.1.13";
        public string ComPort { get; set; }
        public int BaudRate { get; set; } = 9600;
        public string PrinterName { get; set; }
        public string TemplateName { get; set; }
    }

    internal static class PrinterSettingsStore
    {
        private static string FilePath => Path.Combine(AppStorage.DirectoryPath, "settings.json");

        public static SavedPrinterSettings Load()
        {
            try
            {
                return File.Exists(FilePath)
                    ? new JavaScriptSerializer().Deserialize<SavedPrinterSettings>(File.ReadAllText(FilePath)) ?? new SavedPrinterSettings()
                    : new SavedPrinterSettings();
            }
            catch
            {
                return new SavedPrinterSettings();
            }
        }

        public static void Save(SavedPrinterSettings settings)
        {
            AppStorage.WriteAtomically(FilePath, new JavaScriptSerializer().Serialize(settings));
        }
    }
}
