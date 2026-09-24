using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace GodexIndustrial
{
    public static class TemplateManager
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static string TemplatesDirectory => AppStorage.EnsureDirectory("Templates");

        private static string TemplatePath(string name)
        {
            ValidateName(name);
            return Path.Combine(TemplatesDirectory, name + ".json");
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.EndsWith(".") ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || name.Length > 100 ||
                new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
                    .Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Template name is invalid.");
        }

        public static void ValidateTemplate(LabelTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            ValidateName(template.Name);
            if (template.ColumnCount < 1 || template.ColumnCount > 8 || template.XOffsets == null ||
                template.XOffsets.Count < template.ColumnCount || template.XOffsets.Any(x => x < 0 || x > 500) ||
                template.YOffset < 0 || template.YOffset > 1000 || template.FontSize < 1 || template.FontSize > 100 ||
                template.LabelWidth < 1 || template.LabelWidth > 300 || template.LabelLength < 1 || template.LabelLength > 1000 ||
                template.LabelGap < 0 || template.LabelGap > 100 || template.Darkness < 0 || template.Darkness > 30 ||
                template.Rotation < 0 || template.Rotation > 3 || template.PrintSpeed < 0 || template.PrintSpeed > 5)
                throw new ArgumentException("Template settings are outside allowed ranges.");
        }

        public static void SaveTemplate(LabelTemplate template)
        {
            ValidateTemplate(template);
            AppStorage.WriteAtomically(TemplatePath(template.Name), Serializer.Serialize(template));
        }

        public static List<LabelTemplate> GetAllTemplates(Action<string> warning = null)
        {
            var templates = new List<LabelTemplate>();
            foreach (string file in Directory.GetFiles(TemplatesDirectory, "*.json"))
            {
                try
                {
                    LabelTemplate template = Serializer.Deserialize<LabelTemplate>(File.ReadAllText(file));
                    ValidateTemplate(template);
                    if (!string.Equals(Path.GetFileNameWithoutExtension(file), template.Name, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("File name does not match template name.");
                    templates.Add(template);
                }
                catch (Exception ex)
                {
                    warning?.Invoke($"Cannot load template {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            return templates.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static void RenameTemplate(LabelTemplate template, string newName)
        {
            ValidateName(newName);
            string oldPath = TemplatePath(template.Name);
            string newPath = TemplatePath(newName);
            if (File.Exists(newPath)) throw new IOException("A template with this name already exists.");
            var renamed = new LabelTemplate
            {
                Name = newName, Header = template.Header, ColumnCount = template.ColumnCount,
                XOffsets = new List<int>(template.XOffsets), YOffset = template.YOffset,
                FontSize = template.FontSize, LabelWidth = template.LabelWidth,
                LabelLength = template.LabelLength, LabelGap = template.LabelGap,
                Darkness = template.Darkness, Rotation = template.Rotation, PrintSpeed = template.PrintSpeed
            };
            SaveTemplate(renamed);
            try { File.Delete(oldPath); }
            catch
            {
                File.Delete(newPath);
                throw;
            }
            template.Name = newName;
        }

        public static void DeleteTemplate(string name)
        {
            string path = TemplatePath(name);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}


