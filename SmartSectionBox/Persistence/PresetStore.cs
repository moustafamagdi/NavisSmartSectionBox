using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Autodesk.Navisworks.Api;
using SmartSectionBox.Core;

namespace SmartSectionBox.Persistence
{
    public sealed class SectionBoxPreset
    {
        public string Name { get; set; }
        public string DocumentIdentifier { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public SectionBoxState State { get; set; }
    }

    public sealed class PresetStore
    {
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly string rootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NavisworksSmartSectionBox",
            "Presets");

        public IEnumerable<string> ListNames()
        {
            var directory = GetDocumentDirectory();
            if (!Directory.Exists(directory)) return Enumerable.Empty<string>();
            return Directory.GetFiles(directory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public void Save(string name, SectionBoxState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var safeName = ValidateName(name);
            var path = GetPath(safeName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var existing = Load(safeName);
            var now = DateTime.UtcNow;
            var preset = new SectionBoxPreset
            {
                Name = safeName,
                DocumentIdentifier = GetDocumentIdentifier(),
                CreatedUtc = existing == null ? now : existing.CreatedUtc,
                ModifiedUtc = now,
                State = state.Clone()
            };
            File.WriteAllText(path, serializer.Serialize(preset));
        }

        public SectionBoxPreset Load(string name)
        {
            var path = GetPath(ValidateName(name));
            if (!File.Exists(path)) return null;
            return serializer.Deserialize<SectionBoxPreset>(File.ReadAllText(path));
        }

        public bool Delete(string name)
        {
            var path = GetPath(ValidateName(name));
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        public void Rename(string existingName, string newName)
        {
            var preset = Load(existingName);
            if (preset == null) throw new FileNotFoundException("The requested section-box preset was not found.");
            Delete(existingName);
            preset.Name = ValidateName(newName);
            preset.ModifiedUtc = DateTime.UtcNow;
            var path = GetPath(preset.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, serializer.Serialize(preset));
        }

        private string GetPath(string name) => Path.Combine(GetDocumentDirectory(), name + ".json");
        private string GetDocumentDirectory() => Path.Combine(rootDirectory, GetDocumentIdentifier());

        private static string GetDocumentIdentifier()
        {
            var document = Autodesk.Navisworks.Api.Application.ActiveDocument;
            var source = document == null ? "NoDocument" : (document.FileName ?? document.Title ?? "Untitled");
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 24);
            }
        }

        private static string ValidateName(string name)
        {
            var cleaned = (name ?? string.Empty).Trim();
            if (cleaned.Length == 0) throw new ArgumentException("Enter a preset name.", nameof(name));
            if (cleaned.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("The preset name contains invalid file-name characters.", nameof(name));
            return cleaned;
        }
    }
}
