using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PvPChronicle
{
    public sealed class HistoryStore
    {
        private readonly string directory;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        public HistoryStore(string directory) { this.directory = directory; }
        public void Append(HistoryEntry entry)
        {
            Directory.CreateDirectory(directory);
            var record = new Dictionary<string, object> {
                ["id"] = entry.Id, ["timestamp"] = entry.Timestamp.ToString("o"), ["kind"] = entry.Kind,
                ["playerId"] = entry.PlayerId, ["playerName"] = entry.PlayerName, ["targetId"] = entry.TargetId, ["targetName"] = entry.TargetName,
                ["killerId"] = entry.KillerId, ["killerName"] = entry.KillerName, ["lastActionId"] = entry.ActionId,
                ["lastActionName"] = entry.ActionName, ["lastActionTimestamp"] = entry.ActionTimestamp?.ToString("o"),
                ["zone"] = entry.Zone, ["assistWindowSeconds"] = entry.AssistWindowSeconds, ["killerWasAlly"] = entry.KillerWasAlly
            };
            File.AppendAllText(Path.Combine(directory, entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd") + ".jsonl"), json.Serialize(record) + Environment.NewLine, new UTF8Encoding(false));
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
