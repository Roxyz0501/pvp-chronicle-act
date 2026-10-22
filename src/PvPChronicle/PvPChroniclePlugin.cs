using Advanced_Combat_Tracker;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PvPChronicle
{
    public sealed class PvPChroniclePlugin : IActPluginV1
    {
        private readonly CombatLogTracker tracker = new CombatLogTracker();
        private readonly SessionStats stats = new SessionStats();
        private readonly UpdateService updateService = new UpdateService();
        private readonly CancellationTokenSource updateCancellation = new CancellationTokenSource();
        private readonly object lifecycleGate = new object();
        private readonly object updateGate = new object();
        private readonly List<Task> updateTasks = new List<Task>();
        private UpdateCheckResult lastUpdateResult;
        private bool updateCheckRunning;
        private bool stopped, acceptingLogs = true;
        private int activeLogCallbacks;
        private readonly ConcurrentQueue<HistoryEntry> pending = new ConcurrentQueue<HistoryEntry>();
        private readonly List<HistoryEntry> history = new List<HistoryEntry>();
        private readonly System.Windows.Forms.Timer pump = new System.Windows.Forms.Timer { Interval = 250 };
        private HistoryStore store;
        private SettingsPersistence settingsPersistence;
        private readonly System.Windows.Forms.Timer settingsTimer = new System.Windows.Forms.Timer { Interval = 60000 };
        private int bootstrapTicks;
        private uint displayedPlayer;
        private PluginSettings settings; private SettingsControl control; private OverlayForm overlay; private Label status; private TabPage page; private string settingsPath;
        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            status = pluginStatusText; page = pluginScreenSpace; settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker", "Config", "PvPChronicle.xml");
            settings = PluginSettings.Load(settingsPath); settings.InitializeLanguageIfMissing(CultureInfo.CurrentUICulture); settings.Normalize();
            settingsPersistence = new SettingsPersistence(settings, settingsPath);
            control = new SettingsControl(settings); control.SettingsChanged += SettingsChanged; control.CheckUpdatesRequested += CheckUpdatesClicked; control.InstallUpdateRequested += InstallUpdate; control.LaterRequested += Later; page.Controls.Add(control); page.Text = Localization.Get(settings.Language, "PluginTab");
            store = new HistoryStore(HistoryDirectory);
            try { history.AddRange(store.LoadRecent(1000)); } catch (Exception ex) { status.Text = Localization.Get(settings.Language, "LoadError", ex.Message); }
            overlay = new OverlayForm(settings); overlay.ApplySettings(settings, true);
            control.OpenHistoryRequested += delegate { try { Directory.CreateDirectory(HistoryDirectory); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(HistoryDirectory) { UseShellExecute = true }); } catch (Exception ex) { status.Text = Localization.Get(settings.Language, "FolderError", ex.Message); } };
            tracker.AssistWindowSeconds = settings.AssistWindowSeconds;
            tracker.RecordAssists = settings.RecordAssists;
            tracker.Recorded += Recorded; ActGlobals.oFormActMain.OnLogLineRead += LogLineRead;
            BootstrapPlayer(); RefreshHistory(); pump.Tick += Pump; pump.Start();
            if (string.IsNullOrEmpty(status.Text)) status.Text = Localization.Get(settings.Language, tracker.PlayerId == 0 ? "Waiting" : "Started");
            settingsTimer.Tick += SettingsTimerTick; settingsTimer.Start();
            if (settings.CheckUpdatesOnStartup) TrackUpdateTask(CheckForUpdatesAsync(false));
        }
        public static string HistoryDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker", "PvPChronicle", "History");
        private void LogLineRead(bool isImport, LogLineEventArgs info)
        {
            if (isImport || info == null || !TryEnterLogCallback()) return;
            try { var line = (info.originalLogLine ?? "").Contains("|") ? info.originalLogLine : info.logLine; tracker.ProcessLine(line); stats.ProcessLine(line, tracker.PlayerId); }
            finally { ExitLogCallback(); }
        }
        private bool TryEnterLogCallback() { lock (lifecycleGate) { if (!acceptingLogs) return false; activeLogCallbacks++; return true; } }
        private void ExitLogCallback() { lock (lifecycleGate) { activeLogCallbacks--; if (activeLogCallbacks == 0) Monitor.PulseAll(lifecycleGate); } }
        private void Recorded(object sender, HistoryEntry e) { if (e.Kind == "Kill") stats.RecordKill(); pending.Enqueue(e); }
        private void SettingsChanged(object sender, EventArgs e) { control.ApplyTo(settings); tracker.AssistWindowSeconds = settings.AssistWindowSeconds; tracker.RecordAssists = settings.RecordAssists; overlay.ApplySettings(settings); RefreshHistory(); page.Text = Localization.Get(settings.Language, "PluginTab"); }
        private void CheckUpdatesClicked(object sender, EventArgs e) => TrackUpdateTask(CheckForUpdatesAsync(true));
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
        private void SettingsTimerTick(object sender, EventArgs e) => SaveSettingsIfChanged();
        private bool SaveSettingsIfChanged() { try { settingsPersistence?.SaveIfChanged(); return true; } catch (Exception ex) { if (status != null) status.Text = Localization.Get(settings == null ? "en" : settings.Language, "SaveError", ex.Message); return false; } }
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
        private async Task CheckForUpdatesAsync(bool manual)
        {
            if (updateCheckRunning || control == null || control.IsDisposed || stopped) return; updateCheckRunning = true; control.SetUpdateChecking(); var current = Assembly.GetExecutingAssembly().GetName().Version;
            var result = await updateService.CheckAsync(current, updateCancellation.Token).ConfigureAwait(false); if (stopped || updateCancellation.IsCancellationRequested) return;
            SafeUi(() =>
            {
                updateCheckRunning = false; lastUpdateResult = result;
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
            });
        }
        private void InstallUpdate(object sender, EventArgs e) => TrackUpdateTask(PrepareUpdateAsync());
        private Task PrepareUpdateAsync()
        {
            var release = lastUpdateResult == null ? null : lastUpdateResult.Release;
            if (stopped || release == null || lastUpdateResult.Status != UpdateCheckStatus.UpdateAvailable) return Task.CompletedTask;
            var message = Localization.Get(settings.Language, "ManualUpdateOnly");
            control.SetUpdateText("UpdateFailed", message); status.Text = message;
            return Task.CompletedTask;
        }
        private void TrackUpdateTask(Task task) { lock (updateGate) { updateTasks.RemoveAll(x => x.IsCompleted); updateTasks.Add(task); } }
        private void SafeUi(Action action) { try { if (page != null && !page.IsDisposed && page.IsHandleCreated) page.BeginInvoke((MethodInvoker)delegate { if (!stopped && !page.IsDisposed) action(); }); } catch (InvalidOperationException) { } }
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
        private void Later(object sender, EventArgs e) { var release = lastUpdateResult == null ? null : lastUpdateResult.Release; if (release == null) return; settings.SkippedVersion = release.Version.ToString(); control.ShowUpdateSkipped(settings.SkippedVersion); }
        public void DeInitPlugin()
        {
            lock (lifecycleGate) acceptingLogs = false;
            stopped = true;
            ActGlobals.oFormActMain.OnLogLineRead -= LogLineRead; tracker.Recorded -= Recorded;
            pump.Stop(); settingsTimer.Stop();
            lock (lifecycleGate) while (activeLogCallbacks != 0) Monitor.Wait(lifecycleGate);
            Drain(); pump.Dispose(); settingsTimer.Dispose();
            updateCancellation.Cancel();
            Task[] tasks; lock (updateGate) tasks = updateTasks.ToArray();
            try { Task.WaitAll(tasks); } catch (AggregateException) { }
            if (control != null) { control.SettingsChanged -= SettingsChanged; control.CheckUpdatesRequested -= CheckUpdatesClicked; }
            var settingsSaved = SaveSettingsIfChanged();
            overlay?.Close(); overlay?.Dispose(); control?.Dispose(); updateService.Dispose(); updateCancellation.Dispose(); if (status != null && settingsSaved) status.Text = Localization.Get(settings == null ? "en" : settings.Language, "Stopped");
        }
    }
}
