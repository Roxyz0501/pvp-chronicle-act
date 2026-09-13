using PvPChronicle;
using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    private static int assertions;
    private static string artifacts;
    [STAThread]
    private static int Main()
    {
        try
        {
            artifacts = Path.Combine(Environment.CurrentDirectory, "artifacts", "tests"); Directory.CreateDirectory(artifacts);
            Tracking(); SessionTotals(); AssistRules(); Persistence(); Languages(); UpdateSafety(); WindowStability(); RenderSettings();
            if (Environment.GetEnvironmentVariable("PVPCHRONICLE_UPDATE_INTEGRATION") == "1")
            {
                using (var service = new UpdateService())
                {
                    var result = service.CheckAsync(new Version(0, 0, 0), CancellationToken.None).GetAwaiter().GetResult();
                    Check(result.Status == UpdateCheckStatus.UpdateAvailable, "live release available");
                    var prepared = service.DownloadAndVerifyAsync(result.Release, CancellationToken.None).GetAwaiter().GetResult();
                    Check(File.Exists(prepared.StagedDllPath) && File.Exists(prepared.StagedUpdaterPath), "live package verified");
                }
            }
            Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static string Stamp(int seconds) => new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero).AddSeconds(seconds).ToString("o");
    private static string Hit(int time, string target = "10000002", string flags = "3", string source = "10000001", string action = "Attack")
    {
        var f = new string[25]; for (int i = 0; i < f.Length; i++) f[i] = "0";
        f[0] = "21"; f[1] = Stamp(time); f[2] = source; f[3] = "Me"; f[4] = "ABCD"; f[5] = action; f[6] = target; f[7] = "Opponent"; f[8] = flags; f[9] = "03E80000";
        return string.Join("|", f);
    }
    private static string Death(int time, string target = "10000002", string source = "10000001") => "25|" + Stamp(time) + "|" + target + "|Opponent|" + source + "|Killer|hash";
    private static CombatLogTracker Tracker(List<HistoryEntry> entries)
    {
        var t = new CombatLogTracker(); t.ProcessLine("02|" + Stamp(0) + "|10000001|Me|"); t.AllyCheck = (player, killer) => player == 0x10000001 && killer == 0x10000003; t.Recorded += (s, e) => entries.Add(e); return t;
    }
    private static void Tracking()
    {
        var entries = new List<HistoryEntry>(); var t = Tracker(entries);
        t.ProcessLine(Hit(1)); t.ProcessLine(Death(2)); Check(entries.Count == 1 && entries[0].Kind == "Kill" && entries[0].ActionName == "Attack", "kill and last action");
        t.ProcessLine(Death(2)); Check(entries.Count == 1, "duplicate death");
        t.ProcessLine(Hit(5, action: "New action")); t.ProcessLine(Death(6, source: "10000003")); Check(entries.Count == 2 && entries[1].Kind == "EstimatedAssist" && entries[1].ActionName == "New action", "assist after respawn");
        t.ProcessLine(Hit(10)); t.ProcessLine(Death(21, source: "10000003")); Check(entries.Count == 2, "expired assist");
        t.ProcessLine(Hit(25, flags: "4")); t.ProcessLine(Death(26, source: "10000003")); Check(entries.Count == 2, "healing excluded");
        t.ProcessLine(Hit(30, target: "40000002")); t.ProcessLine(Death(31, target: "40000002")); Check(entries.Count == 2, "NPC excluded");
        t.ProcessLine(Hit(35)); t.ProcessLine("01|" + Stamp(36) + "|1|New zone|"); t.ProcessLine(Death(37, source: "10000003")); Check(entries.Count == 2, "zone resets attacks");
        t.ProcessLine(Hit(40)); t.SetPlayer(0x10000004, "Other"); t.ProcessLine(Death(41, source: "10000003")); Check(entries.Count == 2, "player change resets attacks");
        t.SetPlayer(0x10000001, "Me"); t.ProcessLine(Death(45)); Check(entries.Last().ActionName == null, "unknown action does not invent skill");
        t.ProcessLine(Hit(50)); t.ProcessLine(Death(49, source: "10000003")); Check(entries.Count == 3, "future attack rejected");
        t.ProcessLine(Hit(55, flags: "80000003")); t.ProcessLine(Death(56, source: "10000003")); Check(entries.Count == 4, "prior actual hit retained for assist");
        var fresh = new List<HistoryEntry>(); t = Tracker(fresh);
        t.ProcessLine(Hit(1, flags: "80000003")); t.ProcessLine(Death(2, source: "10000003")); Check(fresh.Count == 0, "source-applied effect excluded");
        t.ProcessLine(Hit(4).Replace("|3|03E80000|0|0|", "|1D|0|3|03E80000|")); t.ProcessLine(Death(5, source: "10000003")); Check(fresh.Count == 0, "reflection excluded");
        t.ProcessLine(Hit(7).Replace("03E80000", "0")); t.ProcessLine(Death(8, source: "10000003")); Check(fresh.Count == 0, "zero damage excluded");
        t.ProcessLine(Hit(10)); t.ProcessLine(Death(11, source: "40000003")); Check(fresh.Count == 0, "NPC killer excluded from assist");
        Check(!t.ProcessLine("25|bad|10000002|x|10000001|x"), "malformed time"); Check(!t.ProcessLine("21|" + Stamp(0)), "truncated line");
        t.AssistWindowSeconds = 60; t.ProcessLine(Hit(20)); t.ProcessLine(Death(80, source: "10000003")); Check(fresh.Count == 1, "inclusive configured boundary");
        t.ProcessLine(Death(81, target: "10000001")); Check(fresh.Count == 1, "own death excluded");
    }
    private static void SessionTotals()
    {
        var s = new SessionStats(); const uint me = 0x10000001;
        s.ProcessLine(Hit(1), me); Check(s.Snapshot().DamageDealt == 0, "outside PvP excluded");
        s.ProcessLine("01|" + Stamp(2) + "|378|Onsal Hakair|", me);
        Check(s.Snapshot().InPvp, "PvP zone detected");
        s.ProcessLine(Hit(3), me); s.ProcessLine(Hit(3), me); Check(s.Snapshot().DamageDealt == 1000, "damage and duplicate suppression");
        s.ProcessLine(Hit(4, target: "10000001", source: "10000002"), me); Check(s.Snapshot().DamageTaken == 1000, "incoming damage");
        s.ProcessLine(Hit(5, flags: "4"), me); Check(s.Snapshot().DamageDealt == 1000, "healing not damage");
        s.ProcessLine("24|" + Stamp(6) + "|10000002|Target|DoT|0|64|0|0|0|0|||0|0|0|0|10000001|Me|", me);
        s.ProcessLine("24|" + Stamp(7) + "|10000001|Me|DoT|0|C8|", me);
        Check(s.Snapshot().DamageDealt == 1100 && s.Snapshot().DamageTaken == 1200, "DoT damage");
        s.RecordKill(); Check(s.Snapshot().Kills == 1, "session kill count");
        s.ProcessLine("01|" + Stamp(8) + "|155|Housing|", me); s.ProcessLine(Hit(9), me);
        Check(!s.Snapshot().InPvp && s.Snapshot().DamageDealt == 1100, "outside PvP preserves result");
        s.ProcessLine("01|" + Stamp(10) + "|378|Onsal Hakair|", me);
        Check(s.Snapshot().Kills == 0 && s.Snapshot().DamageDealt == 0 && s.Snapshot().DamageTaken == 0, "entry resets all counters");
        s.ProcessLine(Hit(11), me); s.Bootstrap(888, me); Check(s.Snapshot().DamageDealt == 1000, "poll does not reset totals");
        s.ProcessLine("01|" + Stamp(12) + "|378|Onsal Hakair|", me); Check(s.Snapshot().DamageDealt == 0, "same-zone reload resets");
        Check(Localization.Get("ja", "Kill") == "キル" && Localization.Get("ja", "EstimatedAssist") == "アシスト", "short result labels");
    }
    private static void AssistRules()
    {
        var events = new List<HistoryEntry>(); var tracker = Tracker(events);
        tracker.RecordAssists = false; tracker.ProcessLine(Hit(1)); tracker.ProcessLine(Death(2, source: "10000003"));
        Check(events.Count == 0, "assists off");
        tracker.ProcessLine(Hit(5)); tracker.ProcessLine(Death(6)); Check(events.Count == 1 && events[0].Kind == "Kill", "kills still recorded");
        tracker.RecordAssists = true; tracker.ProcessLine(Hit(10)); tracker.ProcessLine(Death(11, source: "10000004")); Check(events.Count == 1, "third faction killer excluded");
        tracker.ProcessLine(Hit(15)); tracker.ProcessLine(Death(25, source: "10000003")); Check(events.Count == 2 && events[1].KillerWasAlly, "ally kill at time boundary");
        tracker.AllyCheck = null; tracker.ProcessLine(Hit(30)); tracker.ProcessLine(Death(31, source: "10000003")); Check(events.Count == 2, "unknown faction excluded");
        tracker.AllyCheck = (p, k) => { throw new IOException("unavailable"); }; tracker.ProcessLine(Hit(35)); tracker.ProcessLine(Death(36, source: "10000003")); Check(events.Count == 2, "ally lookup failure contained");
        Check(!events[1].ShouldDisplay(false) && events[1].ShouldDisplay(true) && events[0].ShouldDisplay(false), "display toggle preserves kills");
        Check(!new HistoryEntry { Kind = "EstimatedAssist" }.ShouldDisplay(true), "legacy unverified assists hidden");
        var repo = new FakeRepository(); var resolver = new AllyResolver(repo);
        Check(resolver.IsAlly(0x10000001, 0x10000003), "party member accepted");
        repo.Members[0].PartyType = 2; Check(resolver.IsAlly(0x10000001, 0x10000003), "alliance member accepted");
        repo.Members[0].PartyType = 0; Check(!resolver.IsAlly(0x10000001, 0x10000003), "nonmember rejected");
        repo.Members[0].PartyType = 3; Check(!resolver.IsAlly(0x10000001, 0x10000003), "unknown membership rejected");
        repo.Members[0].PartyType = 2; Check(!resolver.IsAlly(0x10000002, 0x10000003), "different current player rejected");
        Check(!resolver.IsAlly(0x10000001, 0x10000004), "missing combatant rejected");
        repo.Fail = true; Check(!resolver.IsAlly(0x10000001, 0x10000003), "repository failure rejected");
        Check(!new AllyResolver(new object()).IsAlly(0x10000001, 0x10000003), "missing API rejected");
        foreach (var lang in new[] { "en", "ja", "zh-CN", "ko" }) Check(Localization.Get(lang, "RecordAssists") != "RecordAssists", "toggle localized " + lang);
    }
    public sealed class FakeCombatant { public uint ID { get; set; } = 0x10000003; public int PartyType { get; set; } = 1; }
    public sealed class FakeRepository
    {
        public bool Fail;
        public FakeCombatant[] Members = { new FakeCombatant() };
        public uint GetCurrentPlayerID() => 0x10000001;
        public FakeCombatant[] GetCombatantList() { if (Fail) throw new IOException("unavailable"); return Members; }
    }
    private static void Persistence()
    {
        var folder = Path.Combine(artifacts, "history " + Guid.NewGuid().ToString("N")); var store = new HistoryStore(folder);
        for (int i = 0; i < 40; i++) store.Append(new HistoryEntry { Id = i.ToString(), Timestamp = DateTimeOffset.Parse(Stamp(i)), Kind = "Kill", PlayerId = 0x10000001, TargetId = 0x10000002, TargetName = "Test \" name\n日本語", ActionName = "技", ActionTimestamp = DateTimeOffset.Parse(Stamp(i)) });
        File.AppendAllText(Directory.GetFiles(folder).Single(), "broken json\n");
        var recent = store.LoadRecent(30); Check(recent.Count == 30 && recent[0].Id == "39" && recent[29].Id == "10", "latest N restored"); Check(recent[0].TargetName == "Test \" name\n日本語", "UTF8 JSON roundtrip");
        var config = Path.Combine(folder, "config.xml"); var s = new PluginSettings { MaximumLines = 30, AssistWindowSeconds = 60, Language = "ko", RecordAssists = false }; s.Save(config); s.Save(config);
        var loaded = PluginSettings.Load(config); Check(loaded.MaximumLines == 30 && loaded.AssistWindowSeconds == 60 && loaded.Language == "ko" && !loaded.RecordAssists, "config roundtrip");
        File.WriteAllText(config, "<PluginSettings><MaximumLines>3</MaximumLines></PluginSettings>"); loaded = PluginSettings.Load(config); Check(loaded.MaximumLines == 3 && loaded.AssistWindowSeconds == 10 && loaded.RecordAssists, "old config defaults");
        store.Append(new HistoryEntry { Id = "ally", Timestamp = DateTimeOffset.Parse(Stamp(50)), Kind = "EstimatedAssist", KillerWasAlly = true });
        Check(store.LoadRecent(1)[0].KillerWasAlly, "ally proof persists");
    }
    private static void Languages()
    {
        Check(Localization.MapCulture(new CultureInfo("ja-JP")) == "ja", "Japanese OS"); Check(Localization.MapCulture(new CultureInfo("zh-TW")) == "zh-CN", "Chinese OS");
        Check(Localization.MapCulture(new CultureInfo("ko-KR")) == "ko", "Korean OS"); Check(Localization.MapCulture(new CultureInfo("fr-FR")) == "en", "English fallback");
        var s = new PluginSettings(); Check(s.InitializeLanguageIfMissing(new CultureInfo("ja-JP")), "initial language"); Check(!s.InitializeLanguageIfMissing(new CultureInfo("ko-KR")) && s.Language == "ja", "retains user language");
        foreach (var language in new[] { "en", "ja", "zh-CN", "ko" }) foreach (var key in new[] { "EstimatedAssist", "LastAction", "HistoryError", "UpdateTitle", "SupportBody", "Waiting" }) Check(Localization.Get(language, key, "test") != key, language + " " + key);
        string error; bool called = false;
        Check(SettingsControl.TryOpenSupportLink(info => { called = true; Check(info.FileName == "https://ko-fi.com/roxyz0501" && info.UseShellExecute, "support URL"); return null; }, out error) && called, "support click");
        Check(!SettingsControl.TryOpenSupportLink(info => { throw new InvalidOperationException("test"); }, out error), "support failure contained");
    }
    private static void UpdateSafety()
    {
        SemVersion a, b; Check(SemVersion.TryParse("v1.10.0", out a) && SemVersion.TryParse("1.9.0", out b) && a.CompareTo(b) > 0, "SemVer numeric");
        Check(!SemVersion.TryParse("1.01.0", out a) && !SemVersion.TryParse("1.0.0-a..b", out a), "invalid SemVer");
        Check(UpdateService.EvaluateResponse("broken", new Version(1, 0, 0)).Status == UpdateCheckStatus.Failed, "invalid JSON");
        var json = "[{\"tag_name\":\"v9.0.0\",\"draft\":false,\"prerelease\":true},{\"tag_name\":\"v1.1.0\",\"draft\":false,\"prerelease\":false}]";
        Check(UpdateService.EvaluateResponse(json, new Version(1, 0, 0)).Release.Version.ToString() == "1.1.0", "stable only");
        Check(UpdateService.CheckFromFetcherAsync(() => { throw new IOException("offline"); }, new Version(1, 0, 0)).GetAwaiter().GetResult().Status == UpdateCheckStatus.Failed, "network failure");
        Throws(() => UpdateService.EnsureAllowedUri(new Uri("http://github.com/x"), false), "HTTP rejected");
        Throws(() => UpdateService.EnsureAllowedUri(new Uri("https://evil.githubusercontent.com/x"), false), "arbitrary host rejected");
        Throws(() => UpdatePackageVerifier.SafeDestination(artifacts, "../outside"), "Zip Slip rejected");
        var zip = Path.Combine(artifacts, "bad-" + Guid.NewGuid().ToString("N") + ".zip"); using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) z.CreateEntry("evil.exe");
        Throws(() => UpdatePackageVerifier.ExtractValidated(zip, Path.Combine(artifacts, "extract")), "unexpected asset rejected");
        var dll = typeof(PluginSettings).Assembly.Location;
        Check(UpdatePackageVerifier.VerifySha256(dll, UpdatePackageVerifier.ComputeSha256(dll)), "hash valid"); Check(!UpdatePackageVerifier.VerifySha256(dll, new string('0', 64)), "corrupt hash");
        var hash = UpdatePackageVerifier.ComputeSha256(dll); Check(UpdatePackageVerifier.FindManifestHash(hash + "  a.zip", "b.zip") == null, "manifest filename");
        SemVersion.TryParse(typeof(PluginSettings).Assembly.GetName().Version.ToString(3), out a); UpdatePackageVerifier.ValidatePluginAssembly(dll, a);
        var instance = new PvPChroniclePlugin(); var target = Path.Combine(artifacts, "日本語 space", "PvPChronicle.dll");
        var registration = new ActPluginData { pluginObj = instance, pluginFile = new FileInfo(target) };
        Check(PvPChroniclePlugin.ResolvePluginDllPath(new[] { registration }, instance, "shadow.dll") == target, "ACT registration priority");
        Throws(() => PvPChroniclePlugin.ResolvePluginDllPath(null, instance, dll), "missing ACT path stops update");
        Directory.CreateDirectory(Path.GetDirectoryName(target)); File.WriteAllText(target, "previous");
        Throws(() => PvPChronicle.Updater.Program.ReplaceWithBackup(dll, target, hash, "1.0.0", (s, d) => { File.WriteAllText(d, "partial"); throw new IOException("failure"); }), "simulated copy failure");
        Check(File.ReadAllText(target) == "previous", "rollback restores original");
        PvPChronicle.Updater.Program.ReplaceWithBackup(dll, target, hash, "1.0.0"); Check(UpdatePackageVerifier.VerifySha256(target, hash), "successful replacement");
        Check(!ReleaseParser.Summarize("**bold** `code`").Contains("**"), "release Markdown removed");
    }
    private static void WindowStability()
    {
        var s = new PluginSettings { Language = "ja", Preview = true, CheckUpdatesOnStartup = false };
        using (var overlay = new OverlayForm(s))
        {
            overlay.ApplySettings(s, true); var handle = overlay.Handle; var bounds = overlay.Bounds; int destroyed = 0, hidden = 0;
            overlay.HandleDestroyed += (sender, e) => destroyed++; overlay.VisibleChanged += (sender, e) => { if (!overlay.Visible) hidden++; };
            foreach (var opacity in new[] { 30, 100, 96 })
            {
                s.OpacityPercent = opacity; s.Locked = !s.Locked; overlay.ApplySettings(s); s.Preview = !s.Preview; overlay.ApplySettings(s); s.OverlayEnabled = !s.OverlayEnabled; overlay.ApplySettings(s);
                Check(overlay.Handle == handle && overlay.Bounds == bounds, "overlay handle and bounds stable");
            }
            Check(destroyed == 0 && hidden == 0, "no handle recreation or Hide");
            s.OverlayEnabled = true; s.Preview = true; s.Locked = false; overlay.ApplySettings(s);
            overlay.SetStats(new StatsSnapshot { InPvp = true, Kills = 11, DamageDealt = 1234567, DamageTaken = 987654 });
            using (var bitmap = new Bitmap(overlay.Width, overlay.Height)) { overlay.DrawToBitmap(bitmap, overlay.ClientRectangle); bitmap.Save(Path.Combine(artifacts, "overlay-ja.png")); }
        }
    }
    private static void RenderSettings()
    {
        foreach (var lang in new[] { "en", "ja", "zh-CN", "ko" })
        using (var form = new Form { ClientSize = new Size(820, 680), ShowInTaskbar = false })
        using (var control = new SettingsControl(new PluginSettings { Language = lang, CheckUpdatesOnStartup = false }))
        {
            form.Controls.Add(control); form.Show(); Application.DoEvents();
            var tabs = FindTabs(control); Check(tabs.TabCount == 4, "all tabs " + lang);
            for (int i = 0; i < tabs.TabCount; i++)
            {
                tabs.SelectedIndex = i; Application.DoEvents();
                using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) { control.DrawToBitmap(bitmap, control.ClientRectangle); bitmap.Save(Path.Combine(artifacts, "settings-" + lang + "-" + i + ".png")); }
            }
            form.ClientSize = new Size(420, 560); Application.DoEvents(); Check(control.Width <= 420, "narrow pane"); form.Close();
        }
    }
    private static TabControl FindTabs(Control root) { foreach (Control c in root.Controls) { if (c is TabControl) return (TabControl)c; var found = FindTabs(c); if (found != null) return found; } return null; }
    private static void Throws(Action action, string name) { try { action(); } catch { Check(true, name); return; } throw new Exception("Expected rejection: " + name); }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); assertions++; }
}
