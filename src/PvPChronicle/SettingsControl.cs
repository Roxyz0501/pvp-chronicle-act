using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Roxyz.Act.UI;

namespace PvPChronicle
{
    public sealed class SettingsControl : UserControl
    {
        public const string SupportUrl = "https://ko-fi.com/roxyz0501";
        private readonly PluginSettings settings;
        private readonly Label headerTitle = L(), headerSubtitle = L();
        private readonly TabPage settingsTab = new TabPage(), supportTab = new TabPage();
        private readonly TabPage historyTab = new TabPage();
        private readonly PluginTabs tabs;
        private readonly DataGridView historyGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly Button openHistory = new Button { AutoSize = true, Height = 32 };
        private System.Collections.Generic.List<HistoryEntry> history = new System.Collections.Generic.List<HistoryEntry>();
        public event EventHandler OpenHistoryRequested;
        private readonly PluginLanguagePage language;
        private readonly PluginUpdatePage updateTab;
        private readonly CheckBox enabled = C(), locked = C(), preview = C();
        private readonly CheckBox recordAssists = C();
        private readonly NumericUpDown duration = N(1, 60, 1), lines = N(1, 30, 1), fontSize = N(12, 40, 1);
        private readonly TrackBar opacity = new TrackBar { Minimum = 30, Maximum = 100, TickFrequency = 5, AutoSize = false, Height = 36, Width = 190 };
        private readonly Label durationLabel = L(), linesLabel = L(), opacityLabel = L(), opacityValue = L(), fontSizeLabel = L();
        private readonly Label hint = L(), estimate = L();
        private readonly Label supportHeading = L(), supportDescription = L(), supportOptional = L(), supportUrl = L(), supportSafety = L(), supportStatus = L();
        private readonly Button supportButton = new Button();
        private bool loading = true;
        private string updateMessageKey = "NotChecked";
        private object[] updateMessageArgs = new object[0];

        public event EventHandler SettingsChanged;
        public event EventHandler CheckUpdatesRequested;
        public event EventHandler InstallUpdateRequested;
        public event EventHandler LaterRequested;
        internal ReleaseInfo AvailableRelease { get; private set; }

        public SettingsControl(PluginSettings value)
        {
            tabs = new PluginTabs(supportTab);
            settings = value; Dock = DockStyle.Fill; BackColor = Color.FromArgb(244, 247, 251); Padding = new Padding(14);
            language = new PluginLanguagePage(settings.Language);
            updateTab = new PluginUpdatePage(UpdateConfiguration.RepositoryName, Assembly.GetExecutingAssembly().GetName().Version.ToString(3), settings.CheckUpdatesOnStartup, settings.Language);
            enabled.Checked = settings.OverlayEnabled; locked.Checked = settings.Locked; preview.Checked = settings.Preview;
            recordAssists.Checked = settings.RecordAssists;
            duration.Value = Bound(duration, settings.AssistWindowSeconds); lines.Value = Bound(lines, settings.MaximumLines);
            opacity.Value = Math.Max(opacity.Minimum, Math.Min(opacity.Maximum, settings.OpacityPercent)); opacityValue.Text = opacity.Value + "%";
            fontSize.Value = Bound(fontSize, settings.FontSize);

            var header = BuildHeader(); ConfigureTabs();
            settingsTab.Controls.Add(BuildSettingsLayout()); supportTab.Controls.Add(BuildSupportLayout());
            var historyLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Secondary(openHistory); openHistory.Click += delegate { OpenHistoryRequested?.Invoke(this, EventArgs.Empty); };
            historyLayout.Controls.Add(openHistory, 0, 0); historyLayout.Controls.Add(historyGrid, 0, 1); historyTab.Controls.Add(historyLayout);
            tabs.TabPages.Add(settingsTab); tabs.TabPages.Add(historyTab); tabs.TabPages.Add(language); tabs.TabPages.Add(updateTab); tabs.TabPages.Add(supportTab);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = BackColor };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(header, 0, 0); root.Controls.Add(tabs, 0, 1); Controls.Add(root);
            WireEvents(); ApplyLanguage(); loading = false;
        }

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(18, 11, 18, 8) };
            headerTitle.Location = new Point(18, 9); headerTitle.ForeColor = Color.FromArgb(34, 44, 61);
            headerSubtitle.Location = new Point(20, 43); headerSubtitle.ForeColor = Color.FromArgb(103, 116, 137);
            header.Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Color.FromArgb(202, 165, 87), 2f)) e.Graphics.DrawLine(pen, 0, header.Height - 2, header.Width, header.Height - 2); };
            header.Controls.Add(headerTitle); header.Controls.Add(headerSubtitle); return header;
        }

        private void ConfigureTabs()
        {
            settingsTab.BackColor = Color.White; supportTab.BackColor = Color.FromArgb(255, 249, 239);
            settingsTab.Padding = supportTab.Padding = new Padding(4);
        }

        private Control BuildSettingsLayout()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, MinimumSize = new Size(600, 0), ColumnCount = 2, RowCount = 11, Padding = new Padding(24, 20, 24, 20), BackColor = Color.White };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var heights = new[] { 0, 34, 34, 34, 38, 42, 42, 46, 42, 62, 92 }; foreach (var h in heights) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            Wide(layout, enabled, 1); Wide(layout, locked, 2); Wide(layout, preview, 3);
            Wide(layout, recordAssists, 4);
            layout.Controls.Add(durationLabel, 0, 5); layout.Controls.Add(duration, 1, 5); layout.Controls.Add(linesLabel, 0, 6); layout.Controls.Add(lines, 1, 6);
            layout.Controls.Add(opacityLabel, 0, 7); layout.Controls.Add(SliderPanel(opacity, opacityValue), 1, 7); layout.Controls.Add(fontSizeLabel, 0, 8); layout.Controls.Add(fontSize, 1, 8);
            hint.AutoSize = false; hint.Dock = DockStyle.Fill; hint.ForeColor = Color.FromArgb(73, 96, 128); hint.Padding = new Padding(0, 8, 0, 0); Wide(layout, hint, 9);
            estimate.AutoSize = false; estimate.Dock = DockStyle.Fill; estimate.ForeColor = Color.FromArgb(151, 91, 29); estimate.Padding = new Padding(0, 5, 0, 0); Wide(layout, estimate, 10);
            panel.Controls.Add(layout); return panel;
        }

        private Control BuildSupportLayout()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 249, 239), Padding = new Padding(28), AutoScroll = true };
            var card = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, MinimumSize = new Size(600, 400), ColumnCount = 1, RowCount = 7, BackColor = Color.White, Padding = new Padding(28, 23, 28, 24) };
            supportHeading.ForeColor = Color.FromArgb(194, 112, 22); supportHeading.Margin = new Padding(0, 0, 0, 10); card.Controls.Add(supportHeading, 0, 0);
            supportDescription.MaximumSize = new Size(700, 0); supportDescription.ForeColor = Color.FromArgb(61, 68, 79); supportDescription.Margin = new Padding(0, 0, 0, 8); card.Controls.Add(supportDescription, 0, 1);
            supportOptional.MaximumSize = new Size(700, 0); supportOptional.ForeColor = Color.FromArgb(117, 76, 24); supportOptional.Margin = new Padding(0, 0, 0, 18); card.Controls.Add(supportOptional, 0, 2);
            supportButton.AutoSize = false; supportButton.Size = new Size(360, 50); Primary(supportButton); supportButton.Font = new Font("Yu Gothic UI", 11f, FontStyle.Bold); supportButton.Margin = new Padding(0, 8, 0, 8); card.Controls.Add(supportButton, 0, 3);
            supportUrl.Text = SupportUrl; supportUrl.ForeColor = Color.FromArgb(126, 104, 78); supportUrl.Margin = new Padding(1, 0, 0, 8); card.Controls.Add(supportUrl, 0, 4);
            supportSafety.ForeColor = Color.FromArgb(73, 96, 128); supportSafety.Margin = new Padding(0, 0, 0, 8); card.Controls.Add(supportSafety, 0, 5); supportStatus.ForeColor = Color.FromArgb(176, 65, 56); card.Controls.Add(supportStatus, 0, 6);
            panel.Controls.Add(card); return panel;
        }

        private void WireEvents()
        {
            language.LanguageChanged += Changed; enabled.CheckedChanged += Changed; locked.CheckedChanged += Changed; preview.CheckedChanged += Changed; duration.ValueChanged += Changed; lines.ValueChanged += Changed;
            recordAssists.CheckedChanged += Changed;
            opacity.ValueChanged += delegate { opacityValue.Text = opacity.Value + "%"; Changed(this, EventArgs.Empty); }; fontSize.ValueChanged += Changed; updateTab.StartupChanged += Changed;
            updateTab.CheckRequested += delegate { CheckUpdatesRequested?.Invoke(this, EventArgs.Empty); }; updateTab.InstallRequested += delegate { InstallUpdateRequested?.Invoke(this, EventArgs.Empty); }; updateTab.LaterRequested += delegate { LaterRequested?.Invoke(this, EventArgs.Empty); }; supportButton.Click += OpenSupportLink;
        }

        public void ApplyTo(PluginSettings value)
        {
            if (settings.HostedLanguage == null) value.Language = language.LanguageCode; value.OverlayEnabled = enabled.Checked; value.Locked = locked.Checked; value.Preview = preview.Checked; value.AssistWindowSeconds = (int)duration.Value;
            value.RecordAssists = recordAssists.Checked;
            value.MaximumLines = (int)lines.Value; value.OpacityPercent = opacity.Value; value.FontSize = (int)fontSize.Value; value.CheckUpdatesOnStartup = updateTab.CheckOnStartup; value.Normalize();
        }

        internal void SetUpdateChecking() { AvailableRelease = null; SetUpdateMessage("Checking"); }
        internal void ShowUpdateResult(UpdateCheckResult result, Version current)
        {
            AvailableRelease = result?.Release;
            if (result == null || result.Status == UpdateCheckStatus.Failed || result.Status == UpdateCheckStatus.RepositoryNotConfigured) { SetUpdateMessage("UpdateConnectionFailed"); }
            else if (result.Status == UpdateCheckStatus.RateLimited) { SetUpdateMessage("RateLimited"); }
            else if (result.Status == UpdateCheckStatus.NoStableRelease) { SetUpdateMessage("NoRelease"); }
            else if (result.Status == UpdateCheckStatus.UpToDate) { SetUpdateMessage("UpToDate", current.ToString(3)); }
            else if (result.ApiRateLimited) { SetUpdateMessage("RateLimitedUpdateAvailable", current.ToString(3), result.Release.Version); }
            else { SetUpdateMessage("UpdateAvailable", current.ToString(3), result.Release.Version); }
        }
        internal void SetUpdateText(string key, string detail = null) { SetUpdateMessage(key, detail == null ? new object[0] : new object[] { detail }); }
        internal void ShowUpdateSkipped(string version) { SetUpdateMessage("UpdateSkipped", version); }
        internal void FocusUpdateTab() { if (tabs.TabPages.Contains(updateTab)) tabs.SelectedTab = updateTab; }

        private void Changed(object sender, EventArgs e) { if (loading) return; ApplyTo(settings); if (ReferenceEquals(sender, language)) ApplyLanguage(); SettingsChanged?.Invoke(this, EventArgs.Empty); }
        private void ApplyLanguage()
        {
            var family = Localization.FontFamily(CurrentLanguage); ApplyFontFamily(this, family);
            headerTitle.Text = T("PluginTab"); headerTitle.Font = new Font(family, 17f, FontStyle.Bold); headerSubtitle.Text = T("Subtitle"); headerSubtitle.Font = new Font(family, 8.5f);
            tabs.SetLanguageFont(family); settingsTab.Text = T("Settings"); language.SetLanguage(CurrentLanguage); updateTab.SetLanguage(CurrentLanguage); supportTab.Text = T("Support");
            historyTab.Text = T("History"); openHistory.Text = T("OpenHistory"); SetHistory(history);
            recordAssists.Text = T("RecordAssists");
            enabled.Text = T("Enabled"); locked.Text = T("Locked"); preview.Text = T("Preview"); durationLabel.Text = T("Duration"); linesLabel.Text = T("Lines"); opacityLabel.Text = T("Opacity"); fontSizeLabel.Text = T("FontSize"); hint.Text = T("Hint"); estimate.Text = T("EstimateNote");
            supportHeading.Text = T("SupportHeading"); supportHeading.Font = new Font(family, 18f, FontStyle.Bold); supportDescription.Text = T("SupportDescription"); supportOptional.Text = T("SupportBody"); supportOptional.Font = new Font(family, 9.5f, FontStyle.Bold); supportButton.Text = T("SupportButton"); supportSafety.Text = T("SupportSafety");
            RefreshUpdateMessage(); tabs.Invalidate(); Invalidate(true);
        }

        public void SetHistory(System.Collections.Generic.IEnumerable<HistoryEntry> entries)
        {
            history = new System.Collections.Generic.List<HistoryEntry>(entries);
            historyGrid.Columns.Clear();
            foreach (var key in new[] { "Time", "Kind", "Target", "LastAction" }) { var column = new DataGridViewTextBoxColumn { HeaderText = T(key), MinimumWidth = 140, SortMode = DataGridViewColumnSortMode.NotSortable }; historyGrid.Columns.Add(column); }
            foreach (var entry in history) if (entry.ShouldDisplay(settings.RecordAssists)) historyGrid.Rows.Add(entry.Timestamp.ToLocalTime().ToString("MM-dd HH:mm:ss"), T(entry.Kind), entry.TargetName, string.IsNullOrWhiteSpace(entry.ActionName) ? T("Unknown") : entry.ActionName);
        }

        private void SetUpdateMessage(string key, params object[] args) { updateMessageKey = key; updateMessageArgs = args ?? new object[0]; RefreshUpdateMessage(); }
        private void RefreshUpdateMessage() { var state = UpdatePageState.Ready;
            switch (updateMessageKey) {
                case "Checking": state = UpdatePageState.Checking; break;
                case "UpdateAvailable": case "RateLimitedUpdateAvailable": state = UpdatePageState.Available; break;
                case "Preparing": state = UpdatePageState.Preparing; break;
                case "Prepared": state = UpdatePageState.Prepared; break;
                case "UpdateFailed": if (AvailableRelease != null) state = UpdatePageState.PrepareFailed; break;
            }
            var message = T(updateMessageKey, updateMessageArgs);
            if (state == UpdatePageState.Available) message += Environment.NewLine + (string.IsNullOrWhiteSpace(AvailableRelease?.Notes) ? T("NoReleaseNotes") : AvailableRelease.Notes);
            updateTab.ShowState(state, message); }
        private string T(string key, params object[] args) => Localization.Get(CurrentLanguage, key, args);
        private string CurrentLanguage => settings.EffectiveLanguage;
        internal void SetHostedLanguage(string code)
        {
            settings.HostedLanguage = Localization.NormalizeLanguage(code);
            foreach (var page in new TabPage[] { language, updateTab, supportTab })
                if (tabs.TabPages.Contains(page)) tabs.TabPages.Remove(page);
            ApplyLanguage();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && settings?.HostedLanguage != null) { language.Dispose(); updateTab.Dispose(); supportTab.Dispose(); }
            base.Dispose(disposing);
        }
        private void OpenSupportLink(object sender, EventArgs e) { string error; if (TryOpenSupportLink(Process.Start, out error)) { supportStatus.ForeColor = Color.FromArgb(17, 108, 75); supportStatus.Text = T("BrowserOpened"); } else { supportStatus.ForeColor = Color.FromArgb(176, 65, 56); supportStatus.Text = T("LinkFailed", error); } }
        private static void Wide(TableLayoutPanel p, Control c, int row) { p.Controls.Add(c, 0, row); p.SetColumnSpan(c, 2); }
        private static Control SliderPanel(TrackBar s, Label v) { var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty }; v.Margin = new Padding(8, 8, 0, 0); p.Controls.Add(s); p.Controls.Add(v); return p; }
        private static void ApplyFontFamily(Control root, string family) { if (root is PluginUpdatePage || root is PluginLanguagePage) return; root.Font = new Font(family, root.Font.Size, root.Font.Style); foreach (Control child in root.Controls) ApplyFontFamily(child, family); }
        private static CheckBox C() => new CheckBox { AutoSize = true, ForeColor = Color.FromArgb(48, 61, 80), Anchor = AnchorStyles.Left };
        private static Label L() => new Label { AutoSize = true, ForeColor = Color.FromArgb(45, 58, 77), Anchor = AnchorStyles.Left };
        private static NumericUpDown N(decimal min, decimal max, decimal inc) => new NumericUpDown { Minimum = min, Maximum = max, Increment = inc, Width = 120, ThousandsSeparator = true, Anchor = AnchorStyles.Left };
        private static decimal Bound(NumericUpDown c, decimal v) => Math.Max(c.Minimum, Math.Min(c.Maximum, v));
        private static int LanguageIndex(string value) { switch (Localization.NormalizeLanguage(value)) { case "ja": return 1; case "zh-CN": return 2; case "ko": return 3; default: return 0; } }
        private static void Primary(Button b) { b.FlatStyle = FlatStyle.Flat; b.BackColor = Color.FromArgb(202, 101, 20); b.ForeColor = Color.White; b.FlatAppearance.BorderColor = Color.FromArgb(164, 79, 12); b.Cursor = Cursors.Hand; }
        private static void Secondary(Button b) { b.FlatStyle = FlatStyle.Flat; b.BackColor = Color.FromArgb(244, 247, 251); b.ForeColor = Color.FromArgb(48, 61, 80); b.FlatAppearance.BorderColor = Color.FromArgb(171, 181, 196); b.Cursor = Cursors.Hand; }
        public static bool TryOpenSupportLink(Func<ProcessStartInfo, Process> opener, out string error) { try { opener(new ProcessStartInfo(SupportUrl) { UseShellExecute = true }); error = null; return true; } catch (Exception ex) { error = ex.Message; return false; } }
    }
}
