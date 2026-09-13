using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PvPChronicle
{
    public sealed class HistoryEntry : EventArgs
    {
        public string Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public string Kind { get; set; }
        public uint PlayerId { get; set; }
        public string PlayerName { get; set; }
        public uint TargetId { get; set; }
        public string TargetName { get; set; }
        public uint KillerId { get; set; }
        public string KillerName { get; set; }
        public string ActionId { get; set; }
        public string ActionName { get; set; }
        public DateTimeOffset? ActionTimestamp { get; set; }
        public string Zone { get; set; }
        public int AssistWindowSeconds { get; set; }
        public bool KillerWasAlly { get; set; }
        public bool ShouldDisplay(bool recordAssists) => Kind == "Kill" || (Kind == "EstimatedAssist" && recordAssists && KillerWasAlly);
    }

    public sealed class CombatLogTracker
    {
        private readonly object gate = new object();
        private readonly Dictionary<uint, Attack> attacks = new Dictionary<uint, Attack>();
        private readonly Dictionary<uint, DateTimeOffset> deaths = new Dictionary<uint, DateTimeOffset>();
        private uint playerId;
        private string playerName = "", zone = "";
        private int assistWindowSeconds = 10;
        private bool recordAssists = true;
        private Func<uint, uint, bool> allyCheck;
        public bool RecordAssists { get { lock (gate) return recordAssists; } set { lock (gate) recordAssists = value; } }
        internal Func<uint, uint, bool> AllyCheck { set { lock (gate) allyCheck = value; } }
        public uint PlayerId { get { lock (gate) return playerId; } }
        public int AssistWindowSeconds { get { lock (gate) return assistWindowSeconds; } set { lock (gate) assistWindowSeconds = Math.Max(1, Math.Min(60, value)); } }
        public event EventHandler<HistoryEntry> Recorded;

        public void SetPlayer(uint id, string name)
        {
            lock (gate)
            {
                if (id != playerId) { attacks.Clear(); deaths.Clear(); playerName = ""; }
                playerId = IsPlayer(id) ? id : 0;
                if (name != null) playerName = name;
            }
        }

        public bool ProcessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length > 32768) return false;
            var f = line.Split('|');
            DateTimeOffset time;
            if (f.Length < 3 || !DateTimeOffset.TryParse(f[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) return false;
            HistoryEntry entry = null;
            lock (gate)
            {
                uint target, source;
                if (f[0] == "02" && f.Length >= 4 && Hex(f[2], out source)) { SetPlayer(source, f[3]); return true; }
                if (f[0] == "01" && f.Length >= 4) { zone = f[3]; attacks.Clear(); deaths.Clear(); return true; }
                if (playerId == 0) return false;
                foreach (var id in attacks.Where(x => time - x.Value.Time > TimeSpan.FromSeconds(60)).Select(x => x.Key).ToArray()) attacks.Remove(id);
                foreach (var id in deaths.Where(x => time - x.Value > TimeSpan.FromSeconds(60)).Select(x => x.Key).ToArray()) deaths.Remove(id);
                if ((f[0] == "21" || f[0] == "22") && f.Length >= 24 && Hex(f[2], out source) && source == playerId && Hex(f[6], out target) && IsPlayer(target) && target != playerId)
                {
                    bool reflected = false, damage = false;
                    for (int i = 8; i < 24; i += 2)
                    {
                        uint flags; long amount;
                        if (!Hex(f[i], out flags)) continue;
                        var type = flags & 0xff;
                        if (type == 0x1d) reflected = true;
                        if ((type == 3 || type == 5 || type == 6) && !reflected && (flags & 0x80000000) == 0 && FfxivAmountDecoder.TryDecode(f[i + 1], out amount) && amount > 0) damage = true;
                    }
                    if (!damage) return false;
                    DateTimeOffset died;
                    if (deaths.TryGetValue(target, out died) && time <= died) return false;
                    Attack previous;
                    if (!attacks.TryGetValue(target, out previous) || time >= previous.Time)
                        attacks[target] = new Attack { Time = time, Id = f[4], Name = f[5] };
                    return true;
                }
                if (f[0] != "25" || f.Length < 6 || !Hex(f[2], out target) || !IsPlayer(target) || target == playerId || !Hex(f[4], out source)) return false;
                DateTimeOffset lastDeath;
                if (deaths.TryGetValue(target, out lastDeath) && time <= lastDeath.AddSeconds(2)) return false;
                Attack hit;
                attacks.TryGetValue(target, out hit);
                var recent = hit != null && time >= hit.Time && (time - hit.Time).TotalSeconds <= assistWindowSeconds;
                deaths[target] = time;
                if (hit == null || time >= hit.Time) attacks.Remove(target);
                // Player-to-player death attribution avoids counting PvE NPC kills and unrelated ally deaths.
                if (source != playerId && (!recordAssists || !recent || !IsPlayer(source) || source == target || !IsConfirmedAlly(source))) return false;
                entry = new HistoryEntry { Id = time.ToString("o") + ":" + playerId.ToString("X8") + ":" + target.ToString("X8"),
                    Timestamp = time, Kind = source == playerId ? "Kill" : "EstimatedAssist", PlayerId = playerId, PlayerName = playerName,
                    TargetId = target, TargetName = f[3], KillerId = source, KillerName = f[5], Zone = zone,
                    ActionId = recent ? hit.Id : null, ActionName = recent ? hit.Name : null, ActionTimestamp = recent ? (DateTimeOffset?)hit.Time : null,
                    AssistWindowSeconds = assistWindowSeconds, KillerWasAlly = source != playerId };
            }
            Recorded?.Invoke(this, entry);
            return true;
        }
        private static bool IsPlayer(uint id) => id >= 0x10000000 && id < 0x20000000;
        private bool IsConfirmedAlly(uint id) { try { return allyCheck != null && allyCheck(playerId, id); } catch { return false; } }
        private static bool Hex(string s, out uint value) => uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        private sealed class Attack { public DateTimeOffset Time; public string Id, Name; }
    }
}
