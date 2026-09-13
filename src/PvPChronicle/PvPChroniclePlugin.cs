using Advanced_Combat_Tracker;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace PvPChronicle
{
    public sealed class PvPChroniclePlugin : IActPluginV1
    {
        private readonly CombatLogTracker tracker = new CombatLogTracker();
        private readonly SessionStats stats = new SessionStats();
        private readonly UpdateService updateService = new UpdateService();
        private readonly CancellationTokenSource updateCancellation = new CancellationTokenSource();
        private UpdateCheckResult lastUpdateResult;
        private bool updateCheckRunning;
        private bool preparing, stopped;
        private readonly ConcurrentQueue<HistoryEntry> pending = new ConcurrentQueue<HistoryEntry>();
        private readonly List<HistoryEntry> history = new List<HistoryEntry>();
        private readonly System.Windows.Forms.Timer pump = new System.Windows.Forms.Timer { Interval = 250 };
        private HistoryStore store;
        private int bootstrapTicks;
        private uint displayedPlayer;
        private PluginSettings settings; private SettingsControl control; private OverlayForm overlay; private Label status; private TabPage page; private string settingsPath;
        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            status = pluginStatusText; page = pluginScreenSpace; settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker", "Config", "PvPChronicle.xml");
            settings = PluginSettings.Load(settingsPath); if (settings.InitializeLanguageIfMissing(CultureInfo.CurrentUICulture)) Save(); settings.Normalize();
            control = new SettingsControl(settings); control.SettingsChanged += SettingsChanged; control.CheckUpdatesRequested += delegate { CheckForUpdates(true); }; control.InstallUpdateRequested += InstallUpdate; control.LaterRequested += Later; page.Controls.Add(control); page.Text = Localization.Get(settings.Language, "PluginTab");
            store = new HistoryStore(HistoryDirectory);
            try { history.AddRange(store.LoadRecent(1000)); } catch (Exception ex) { status.Text = Localization.Get(settings.Language, "LoadError", ex.Message); }
            overlay = new OverlayForm(settings); overlay.BoundsChangedByUser += delegate { Save(); }; overlay.ApplySettings(settings, true);
            control.OpenHistoryRequested += delegate { try { Directory.CreateDirectory(HistoryDirectory); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(HistoryDirectory) { UseShellExecute = true }); } catch (Exception ex) { status.Text = Localization.Get(settings.Language, "FolderError", ex.Message); } };
            tracker.AssistWindowSeconds = settings.AssistWindowSeconds;
            tracker.RecordAssists = settings.RecordAssists;
            tracker.Recorded += Recorded; ActGlobals.oFormActMain.OnLogLineRead += LogLineRead;
            BootstrapPlayer(); RefreshHistory(); pump.Tick += Pump; pump.Start();
            if (string.IsNullOrEmpty(status.Text)) status.Text = Localization.Get(settings.Language, tracker.PlayerId == 0 ? "Waiting" : "Started");
            if (settings.CheckUpdatesOnStartup) CheckForUpdates(false);
        }
        public static string HistoryDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker", "PvPChronicle", "History");
        private void LogLineRead(bool isImport, LogLineEventArgs info) { if (isImport || info == null) return; var line = (info.originalLogLine ?? "").Contains("|") ? info.originalLogLine : info.logLine; tracker.ProcessLine(line); stats.ProcessLine(line, tracker.PlayerId); }
        private void Recorded(object sender, HistoryEntry e) { if (e.Kind == "Kill") stats.RecordKill(); pending.Enqueue(e); }
        private void SettingsChanged(object sender, EventArgs e) { control.ApplyTo(settings); tracker.AssistWindowSeconds = settings.AssistWindowSeconds; tracker.RecordAssists = settings.RecordAssists; overlay.ApplySettings(settings); RefreshHistory(); page.Text = Localization.Get(settings.Language, "PluginTab"); Save(); }
        private void RefreshHistory() { displayedPlayer = tracker.PlayerId; overlay.SetEntries(history.Where(x => x.PlayerId == displayedPlayer && x.ShouldDisplay(settings.RecordAssists))); control.SetHistory(history); }
        private void Pump(object sender, EventArgs e)
        {
            if (++bootstrapTicks >= 8) { bootstrapTicks = 0; BootstrapPlayer(); }
            if (displayedPlayer != tracker.PlayerId) { RefreshHistory(); status.Text = Localization.Get(settings.Language, tracker.PlayerId == 0 ? "Waiting" : "Started"); }
            Drain();
            overlay.SetStats(stats.Snapshot());
        }
        private void Drain()
        {
            HistoryEntry entry; bool changed = false;
            while (pending.TryDequeue(out entry))
            {
                if (history.Any(x => x.Id == entry.Id)) continue;
                history.Insert(0, entry); if (history.Count > 1000) history.RemoveAt(history.Count - 1); changed = true;
                try { store.Append(entry); } catch (Exception ex) { status.Text = Localization.Get(settings.Language, "HistoryError", ex.Message); }
            }
            if (changed) RefreshHistory();
        }
        private void Save() { try { settings?.Save(settingsPath); } catch (Exception ex) { if (status != null) status.Text = Localization.Get(settings == null ? "en" : settings.Language, "SaveError", ex.Message); } }
        private void BootstrapPlayer()
        {
            try
            {
                foreach (var plugin in ActGlobals.oFormActMain.ActPlugins)
                {
                    var instance = plugin.pluginObj;
                    if (instance == null || instance.GetType().Assembly.GetName().Name != "FFXIV_ACT_Plugin") continue;
                    var repository = instance.GetType().GetProperty("DataRepository")?.GetValue(instance, null);
                    if (repository == null) continue;
                    tracker.AllyCheck = new AllyResolver(repository).IsAlly;
                    var type = repository.GetType().GetInterfaces().FirstOrDefault(x => x.FullName == "FFXIV_ACT_Plugin.Common.IDataRepository") ?? repository.GetType();
                    var method = type.GetMethod("GetCurrentPlayerID");
                    if (method != null) tracker.SetPlayer(Convert.ToUInt32(method.Invoke(repository, null)), null);
                    var territory = type.GetMethod("GetCurrentTerritoryID");
                    if (territory != null) stats.Bootstrap(Convert.ToUInt32(territory.Invoke(repository, null)), tracker.PlayerId);
                    return;
                }
            }
            catch { }
        }
        private async void CheckForUpdates(bool manual)
        {
            if (updateCheckRunning || control == null || control.IsDisposed) return; updateCheckRunning = true; control.SetUpdateChecking(); var current = Assembly.GetExecutingAssembly().GetName().Version;
            var result = await updateService.CheckAsync(current, updateCancellation.Token); if (control == null || control.IsDisposed || updateCancellation.IsCancellationRequested) return;
            updateCheckRunning = false;
            lastUpdateResult = result; settings.LastUpdateCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); Save();
            if (!manual && result != null && result.Status == UpdateCheckStatus.UpdateAvailable && result.Release != null &&
                string.Equals(settings.SkippedVersion, result.Release.Version.ToString(), StringComparison.OrdinalIgnoreCase))
            { control.ShowUpdateResult(result, current); control.ShowUpdateSkipped(result.Release.Version.ToString()); }
            else
            {
                control.ShowUpdateResult(result, current);
                if (result != null && result.Status == UpdateCheckStatus.UpdateAvailable && result.Release != null)
                {
                    control.FocusUpdateTab(); status.Text = Localization.Get(settings.Language, "UpdateAvailable", current.ToString(3), result.Release.Version);
                }
            }
        }
        private async void InstallUpdate(object sender, EventArgs e)
        {
            var release = lastUpdateResult == null ? null : lastUpdateResult.Release; if (preparing || stopped || release == null || lastUpdateResult.Status != UpdateCheckStatus.UpdateAvailable) return; preparing = true; control.SetUpdateText("Preparing");
            try { var prepared = await updateService.DownloadAndVerifyAsync(release, updateCancellation.Token); if (updateCancellation.IsCancellationRequested) return; var targetPath = ResolvePluginDllPath(ActGlobals.oFormActMain.ActPlugins, this, Assembly.GetExecutingAssembly().Location); updateService.LaunchUpdater(prepared, targetPath); control.SetUpdateText("Prepared"); status.Text = Localization.Get(settings.Language, "Prepared"); }
            catch (Exception ex) { if (!stopped) { control.SetUpdateText("UpdateFailed", ex.GetBaseException().Message); status.Text = Localization.Get(settings.Language, "UpdateFailed", ex.GetBaseException().Message); } }
            finally { preparing = false; }
        }
        internal static string ResolvePluginDllPath(IEnumerable<ActPluginData> plugins, IActPluginV1 instance, string assemblyLocation)
        {
            if (plugins != null)
            {
                foreach (var plugin in plugins)
                    if (plugin != null && ReferenceEquals(plugin.pluginObj, instance) && plugin.pluginFile != null && !string.IsNullOrWhiteSpace(plugin.pluginFile.FullName))
                        return Path.GetFullPath(plugin.pluginFile.FullName);
            }
            throw new InvalidOperationException("PluginPathUnavailable");
        }
        private void Later(object sender, EventArgs e) { var release = lastUpdateResult == null ? null : lastUpdateResult.Release; if (release == null) return; settings.SkippedVersion = release.Version.ToString(); Save(); control.ShowUpdateSkipped(settings.SkippedVersion); }
        public void DeInitPlugin()
        {
            stopped = true;
            ActGlobals.oFormActMain.OnLogLineRead -= LogLineRead; tracker.Recorded -= Recorded; pump.Stop(); Drain(); pump.Dispose(); updateCancellation.Cancel(); if (control != null) control.SettingsChanged -= SettingsChanged; Save();
            overlay?.Close(); overlay?.Dispose(); control?.Dispose(); updateService.Dispose(); updateCancellation.Dispose(); if (status != null) status.Text = Localization.Get(settings == null ? "en" : settings.Language, "Stopped");
        }
    }
}
