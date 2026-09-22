using System;
using System.IO;

namespace GodexIndustrial
{
    internal static class AppStorage
    {
        public static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodexIndustrial");

        public static string EnsureDirectory(string name)
        {
            string path = Path.Combine(DirectoryPath, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public static void WriteAtomically(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, content);
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
