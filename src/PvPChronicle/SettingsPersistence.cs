using System;
using System.IO;
using System.Xml.Serialization;

namespace PvPChronicle
{
    internal sealed class SettingsPersistence
    {
        private readonly object gate = new object();
        private readonly PluginSettings settings;
        private readonly string path;
        private readonly Action<PluginSettings, string> save;
        private byte[] lastSaved;
        private bool dirty;

        internal SettingsPersistence(PluginSettings settings, string path)
            : this(settings, path, (value, file) => value.Save(file)) { }

        internal SettingsPersistence(PluginSettings settings, string path, Action<PluginSettings, string> save)
        {
            this.settings = settings ?? throw new ArgumentNullException("settings");
            this.path = path ?? throw new ArgumentNullException("path");
            this.save = save ?? throw new ArgumentNullException("save");
            if (File.Exists(path)) lastSaved = Serialize(settings);
        }

        internal bool IsDirty { get { lock (gate) return dirty || !MatchesLastSaved(); } }

        internal bool SaveIfChanged()
        {
            lock (gate) return SaveIfChangedCore();
        }

        private bool SaveIfChangedCore()
        {
            var snapshot = Serialize(settings);
            if (lastSaved != null && Equal(snapshot, lastSaved)) { dirty = false; return false; }
            dirty = true;
            save(settings, path);
            lastSaved = snapshot;
            dirty = false;
            return true;
        }

        private bool MatchesLastSaved()
        {
            return lastSaved != null && Equal(Serialize(settings), lastSaved);
        }

        private static byte[] Serialize(PluginSettings value)
        {
            using (var stream = new MemoryStream())
            {
                new XmlSerializer(typeof(PluginSettings)).Serialize(stream, value);
                return stream.ToArray();
            }
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
}
