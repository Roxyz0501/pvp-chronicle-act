using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PvPChronicle
{
    internal sealed class HistoryWriteResult
    {
        internal int WriteOperations { get; set; }
        internal long BytesWritten { get; set; }
    }

    public sealed class HistoryStore
    {
        private readonly string directory;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        public HistoryStore(string directory) { this.directory = directory; }
        public void Append(HistoryEntry entry) => AppendBatch(new[] { entry });

        internal HistoryWriteResult AppendBatch(IList<HistoryEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException("entries");
            var result = new HistoryWriteResult();
            foreach (var group in entries.Where(x => x != null).GroupBy(x => x.Timestamp.ToLocalTime().ToString("yyyy-MM-dd"), StringComparer.Ordinal))
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, group.Key + ".jsonl");
                var lines = group.Select(Serialize).ToList();
                var bytes = lines.Select(x => Encoding.UTF8.GetBytes(x + Environment.NewLine)).ToList();
                using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    RepairPartialTail(stream);
                    var writtenPrefix = FindWrittenPrefix(stream, lines, bytes.Sum(x => (long)x.Length));
                    if (writtenPrefix == lines.Count) continue;
                    stream.Position = stream.Length;
                    var payload = new byte[bytes.Skip(writtenPrefix).Sum(x => x.Length)]; var offset = 0;
                    for (var i = writtenPrefix; i < bytes.Count; i++)
                    {
                        Buffer.BlockCopy(bytes[i], 0, payload, offset, bytes[i].Length);
                        offset += bytes[i].Length;
                    }
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                    result.BytesWritten += payload.Length;
                    result.WriteOperations++;
                }
            }
            return result;
        }

        private string Serialize(HistoryEntry entry)
        {
            var record = new Dictionary<string, object> {
                ["id"] = entry.Id, ["timestamp"] = entry.Timestamp.ToString("o"), ["kind"] = entry.Kind,
                ["playerId"] = entry.PlayerId, ["playerName"] = entry.PlayerName, ["targetId"] = entry.TargetId, ["targetName"] = entry.TargetName,
                ["killerId"] = entry.KillerId, ["killerName"] = entry.KillerName, ["lastActionId"] = entry.ActionId,
                ["lastActionName"] = entry.ActionName, ["lastActionTimestamp"] = entry.ActionTimestamp?.ToString("o"),
                ["zone"] = entry.Zone, ["assistWindowSeconds"] = entry.AssistWindowSeconds, ["killerWasAlly"] = entry.KillerWasAlly
            };
            return json.Serialize(record);
        }

        private static void RepairPartialTail(FileStream stream)
        {
            if (stream.Length == 0) return;
            stream.Position = stream.Length - 1;
            if (stream.ReadByte() == (byte)'\n') return;
            var position = stream.Length - 1;
            while (position >= 0)
            {
                stream.Position = position;
                if (stream.ReadByte() == (byte)'\n') { stream.SetLength(position + 1); return; }
                position--;
            }
            stream.SetLength(0);
        }

        private static int FindWrittenPrefix(FileStream stream, IList<string> expected, long expectedBytes)
        {
            if (stream.Length == 0 || expected.Count == 0) return 0;
            var window = Math.Min(stream.Length, Math.Min(expectedBytes + 256, 1024 * 1024));
            stream.Position = stream.Length - window;
            var tail = new byte[(int)window]; var offset = 0;
            while (offset < tail.Length)
            {
                var read = stream.Read(tail, offset, tail.Length - offset);
                if (read == 0) break;
                offset += read;
            }
            var text = Encoding.UTF8.GetString(tail, 0, offset);
            var lines = text.Split('\n').ToList();
            if (stream.Length > window && lines.Count > 0) lines.RemoveAt(0);
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            for (var count = Math.Min(lines.Count, expected.Count); count > 0; count--)
            {
                var matches = true;
                for (var i = 0; i < count; i++)
                    if (!string.Equals(lines[lines.Count - count + i].TrimEnd('\r'), expected[i], StringComparison.Ordinal)) { matches = false; break; }
                if (matches) return count;
            }
            return 0;
        }
        public List<HistoryEntry> LoadRecent(int count)
        {
            var result = new List<HistoryEntry>(); var seen = new HashSet<string>();
            if (!Directory.Exists(directory)) return result;
            foreach (var file in Directory.GetFiles(directory, "????-??-??.jsonl").OrderByDescending(x => x, StringComparer.Ordinal))
            {
                // Read only the tail of each day; old history remains on disk without a growing memory footprint.
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var start = Math.Max(0, stream.Length - 1024 * 1024); stream.Position = start;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        if (start > 0) reader.ReadLine();
                        var day = new Queue<HistoryEntry>(); string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            try
                            {
                                var d = json.Deserialize<Dictionary<string, object>>(line);
                                Func<string, string> text = key => d.ContainsKey(key) ? Convert.ToString(d[key], CultureInfo.InvariantCulture) : "";
                                DateTimeOffset time, actionTime;
                                if (!DateTimeOffset.TryParse(text("timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.None, out time) || string.IsNullOrEmpty(text("id")) || !seen.Add(text("id"))) continue;
                                var kind = text("kind"); if (kind != "Kill" && kind != "EstimatedAssist") continue;
                                day.Enqueue(new HistoryEntry { Id = text("id"), Timestamp = time, Kind = kind, TargetName = text("targetName"), PlayerName = text("playerName"),
                                    PlayerId = Convert.ToUInt32(d["playerId"], CultureInfo.InvariantCulture), TargetId = Convert.ToUInt32(d["targetId"], CultureInfo.InvariantCulture),
                                    KillerId = Convert.ToUInt32(d["killerId"], CultureInfo.InvariantCulture), KillerName = text("killerName"), ActionId = text("lastActionId"), KillerWasAlly = text("killerWasAlly") == "True",
                                    ActionName = text("lastActionName"), ActionTimestamp = DateTimeOffset.TryParse(text("lastActionTimestamp"), out actionTime) ? (DateTimeOffset?)actionTime : null, Zone = text("zone") });
                                while (day.Count > count) day.Dequeue();
                            }
                            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is FormatException || ex is OverflowException || ex is KeyNotFoundException) { }
                        }
                        result.AddRange(day.Reverse());
                    }
                }
                if (result.Count >= count) break;
            }
            return result.OrderByDescending(x => x.Timestamp).Take(count).ToList();
        }
    }
}
