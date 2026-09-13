using System;
using System.Collections.Generic;
using System.Globalization;

namespace PvPChronicle
{
    public sealed class StatsSnapshot
    {
        public bool InPvp { get; set; }
        public long Kills { get; set; }
        public long DamageDealt { get; set; }
        public long DamageTaken { get; set; }
    }

    public sealed class SessionStats
    {
        // TerritoryType.IsPvpZone, xivapi/ffxiv-datamining (2026-09-13).
        private static readonly HashSet<uint> PvpZones = new HashSet<uint> { 149, 250, 376, 431, 554, 729, 791, 888, 1032, 1033, 1034, 1058, 1059, 1060, 1116, 1117, 1138, 1139, 1273, 1293, 1294, 1313, 1357, 1358 };
        private readonly object gate = new object();
        private readonly HashSet<string> seen = new HashSet<string>();
        private readonly Queue<string> recent = new Queue<string>();
        private bool zoneKnown, inPvp;
        private long kills, dealt, taken;
        private uint player;
        public StatsSnapshot Snapshot() { lock (gate) return new StatsSnapshot { InPvp = inPvp, Kills = kills, DamageDealt = dealt, DamageTaken = taken }; }
        public void Bootstrap(uint zone, uint playerId)
        {
            lock (gate)
            {
                SetPlayer(playerId);
                if (!zoneKnown && zone != 0) EnterZone(zone);
            }
        }
        private void SetPlayer(uint id) { if (id != player) { kills = dealt = taken = 0; seen.Clear(); recent.Clear(); player = id; } }
        private void EnterZone(uint id)
        {
            zoneKnown = true; inPvp = PvpZones.Contains(id);
            if (inPvp) { kills = dealt = taken = 0; seen.Clear(); recent.Clear(); }
        }
        public void RecordKill() { lock (gate) if (inPvp) kills++; }
        public void ProcessLine(string line, uint playerId)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length > 32768) return;
            var f = line.Split('|'); DateTimeOffset time; uint source, target, zone;
            if (f.Length < 3 || !DateTimeOffset.TryParse(f[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out time)) return;
            lock (gate)
            {
                SetPlayer(playerId);
                if (f[0] == "01" && Hex(f[2], out zone)) { EnterZone(zone); return; }
                if (!inPvp || player == 0) return;
                if (f[0] != "21" && f[0] != "22" && f[0] != "24") return;
                if (!seen.Add(line)) return;
                recent.Enqueue(line); if (recent.Count > 4096) seen.Remove(recent.Dequeue());
                if (f[0] == "24")
                {
                    uint amount;
                    if (f.Length < 7 || f[4] != "DoT" || !Hex(f[2], out target) || !Hex(f[6], out amount)) return;
                    if (target == player) taken += amount;
                    if (f.Length > 18 && Hex(f[17], out source) && source == player) dealt += amount;
                    return;
                }
                if (f.Length < 24 || !Hex(f[2], out source) || !Hex(f[6], out target) || (source != player && target != player)) return;
                bool reflected = false;
                for (int i = 8; i < 24; i += 2)
                {
                    uint flags; long amount;
                    if (!Hex(f[i], out flags)) continue;
                    var type = flags & 0xff;
                    if (type == 0x1d) reflected = true;
                    if ((type != 3 && type != 5 && type != 6) || reflected || (flags & 0x80000000) != 0 || !FfxivAmountDecoder.TryDecode(f[i + 1], out amount)) continue;
                    if (source == player) dealt += amount;
                    if (target == player) taken += amount;
                }
            }
        }
        private static bool Hex(string s, out uint value) => uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}
