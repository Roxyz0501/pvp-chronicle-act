using System;
using System.Collections.Generic;
using System.Globalization;

namespace PvPChronicle
{
    public static class Localization
    {
        public const string English = "en", Japanese = "ja", Chinese = "zh-CN", Korean = "ko";
        private static readonly Dictionary<string, Dictionary<string, string>> R = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [English] = Table(
                "PluginTab=PvP Chronicle|Subtitle=PvP kills and assists|Settings=Settings|Support=Support|Language=Language|Enabled=Enable overlay|Locked=Lock position and size (click-through)|Preview=Show positioning preview|Duration=Assist window (seconds)|Lines=Recent records (N)|Opacity=Opacity|FontSize=Text size|Hint=History is retained. Unlock to move, resize or scroll the overlay.|PreviewText=Preview|PreviewDetail=Preview|SupportHeading=Support Roxyz0501's development|SupportDescription=If this plugin is useful, you can support continued development through Ko-fi.|SupportBody=Support is completely optional. Every feature remains available with no differences if you do not support.|SupportSafety=The link opens only when you click the button. No startup prompts or automatic redirects are used.|SupportButton=Support Roxyz0501 on Ko-fi|BrowserOpened=Ko-fi opened in your default browser.|LinkFailed=Could not open the link: {0}|Started=PvP Chronicle: recording|Stopped=PvP Chronicle: stopped|SaveError=Could not save settings: {0}|EstimateNote=Assists are estimated from your direct damage before a player dies. Last action is not a confirmed finishing blow.|Updates=Update|UpdateTitle=PvP Chronicle updates|UpdateDescription=Check stable GitHub Releases. Install updates manually after closing ACT.|CheckStartup=Check for updates when the plugin starts|CheckNow=Check now|CurrentVersion=Current version|LatestVersion=Latest version|ReleaseNotes=Release notes|NoReleaseNotes=No release notes were provided.|NotChecked=Not checked yet|Checking=Checking for updates…|UpToDate=You are using the latest stable version ({0}).|NoRelease=No stable release was found.|CheckFailed=Update check failed; normal operation continues.|UpdateAvailable=Stable update available: {0} → {1}|UpdateNow=Manual install instructions|Later=Later|UpdateSkipped=Version {0} was postponed.|Preparing=Downloading and verifying the update…|Prepared=Update verified. Close ACT to apply it, then restart ACT.|UpdateFailed=Could not prepare the update: {0}"),
            [Japanese] = Table(
                "PluginTab=PvP Chronicle|Subtitle=PvPのキル・アシスト履歴|Settings=設定|Support=支援|Language=言語|Enabled=オーバーレイを有効にする|Locked=位置とサイズを固定（クリック透過）|Preview=位置調整プレビューを表示|Duration=アシスト判定時間（秒）|Lines=直近の表示件数（N）|Opacity=透明度|FontSize=文字サイズ|Hint=履歴は消えずに保持されます。固定を解除して移動・サイズ変更・スクロールできます。|PreviewText=プレビュー|PreviewDetail=プレビュー|SupportHeading=Roxyz0501の開発を支援|SupportDescription=このプラグインが役立った場合は、Ko-fiから継続開発を支援できます。|SupportBody=支援は完全に任意です。支援しなくてもすべての機能を利用でき、機能差はありません。|SupportSafety=リンクはボタンを明示的に押した場合だけ開きます。起動時案内や自動遷移はありません。|SupportButton=Ko-fiでRoxyz0501を支援する|BrowserOpened=既定のブラウザでKo-fiを開きました。|LinkFailed=リンクを開けませんでした: {0}|Started=PvP Chronicle: 記録中|Stopped=PvP Chronicle: 停止|SaveError=設定を保存できませんでした: {0}|EstimateNote=アシストは自分の直接攻撃から判定秒数以内に、味方が撃破した場合のみ記録します。所属不明は対象外です。最終攻撃は確定したとどめではありません。|Updates=更新|UpdateTitle=PvP Chronicleの更新|UpdateDescription=GitHub Releasesの安定版を確認します。更新はACT終了後に手動で適用してください。|CheckStartup=起動時に更新を確認する|CheckNow=今すぐ確認|CurrentVersion=現在のバージョン|LatestVersion=最新バージョン|ReleaseNotes=更新内容|NoReleaseNotes=更新内容はありません。|NotChecked=未確認|Checking=更新を確認しています…|UpToDate=最新の安定版を使用しています（{0}）。|NoRelease=安定版Releaseがありません。|CheckFailed=更新確認に失敗しました。通常機能は継続します。|UpdateAvailable=安定版の更新があります: {0} → {1}|UpdateNow=手動更新の案内|Later=後で|UpdateSkipped=バージョン {0} を後回しにしました。|Preparing=更新をダウンロードして検証しています…|Prepared=更新を検証しました。ACTを終了すると適用されます。完了後にACTを再起動してください。|UpdateFailed=更新を準備できませんでした: {0}"),
            [Chinese] = Table(
                "PluginTab=PvP Chronicle|Subtitle=PvP击杀与助攻记录|Settings=设置|Support=支持|Language=语言|Enabled=启用悬浮窗|Locked=锁定位置和大小（鼠标穿透）|Preview=显示位置预览|Duration=助攻判定时间（秒）|Lines=最近显示条数（N）|Opacity=透明度|FontSize=文字大小|Hint=保留历史记录。解锁后可移动、调整大小或滚动悬浮窗。|PreviewText=预览|PreviewDetail=预览|SupportHeading=支持Roxyz0501的开发|SupportDescription=如果此插件对您有帮助，可以通过Ko-fi支持后续开发。|SupportBody=支持完全自愿。即使不支持，也可以使用全部功能，不存在功能差异。|SupportSafety=只有明确点击按钮时才会打开链接，不会在启动时提示或自动跳转。|SupportButton=在Ko-fi上支持Roxyz0501|BrowserOpened=已在默认浏览器中打开Ko-fi。|LinkFailed=无法打开链接: {0}|Started=PvP Chronicle: 记录中|Stopped=PvP Chronicle: 已停止|SaveError=无法保存设置: {0}|EstimateNote=助攻根据目标死亡前您造成的直接伤害推定。最后攻击不代表已确认的致命技能。|Updates=更新|UpdateTitle=PvP Chronicle更新|UpdateDescription=检查GitHub Releases稳定版并准备经过验证的更新。|CheckStartup=插件启动时检查更新|CheckNow=立即检查|CurrentVersion=当前版本|LatestVersion=最新版本|ReleaseNotes=更新内容|NoReleaseNotes=未提供更新内容。|NotChecked=尚未检查|Checking=正在检查更新…|UpToDate=当前已是最新稳定版（{0}）。|NoRelease=未找到稳定版Release。|CheckFailed=检查更新失败，插件会继续正常运行。|UpdateAvailable=发现稳定版更新: {0} → {1}|UpdateNow=下载并准备更新|Later=稍后|UpdateSkipped=已暂缓版本 {0}。|Preparing=正在下载并验证更新…|Prepared=更新已验证。关闭ACT后将应用更新，然后请重新启动ACT。|UpdateFailed=无法准备更新: {0}"),
            [Korean] = Table(
                "PluginTab=PvP Chronicle|Subtitle=PvP 처치 및 도움 기록|Settings=설정|Support=후원|Language=언어|Enabled=오버레이 사용|Locked=위치와 크기 잠금(클릭 통과)|Preview=위치 미리보기 표시|Duration=도움 판정 시간(초)|Lines=최근 표시 건수(N)|Opacity=투명도|FontSize=글자 크기|Hint=기록은 유지됩니다. 잠금을 해제하여 이동, 크기 조절 및 스크롤할 수 있습니다.|PreviewText=미리보기|PreviewDetail=미리보기|SupportHeading=Roxyz0501의 개발 후원|SupportDescription=이 플러그인이 유용하다면 Ko-fi를 통해 지속적인 개발을 후원할 수 있습니다.|SupportBody=후원은 완전히 선택 사항입니다. 후원하지 않아도 모든 기능을 차이 없이 사용할 수 있습니다.|SupportSafety=버튼을 명시적으로 클릭한 경우에만 링크가 열리며 시작 알림이나 자동 이동은 없습니다.|SupportButton=Ko-fi에서 Roxyz0501 후원하기|BrowserOpened=기본 브라우저에서 Ko-fi를 열었습니다.|LinkFailed=링크를 열 수 없습니다: {0}|Started=PvP Chronicle: 기록 중|Stopped=PvP Chronicle: 중지됨|SaveError=설정을 저장할 수 없습니다: {0}|EstimateNote=도움은 대상 사망 전 내가 준 직접 피해로 추정합니다. 마지막 공격은 확인된 결정타가 아닙니다.|Updates=업데이트|UpdateTitle=PvP Chronicle 업데이트|UpdateDescription=GitHub Releases의 안정 버전을 확인하고 검증된 업데이트를 준비합니다.|CheckStartup=플러그인 시작 시 업데이트 확인|CheckNow=지금 확인|CurrentVersion=현재 버전|LatestVersion=최신 버전|ReleaseNotes=업데이트 내용|NoReleaseNotes=업데이트 내용이 없습니다.|NotChecked=확인하지 않음|Checking=업데이트 확인 중…|UpToDate=최신 안정 버전을 사용 중입니다({0}).|NoRelease=안정 버전 Release가 없습니다.|CheckFailed=업데이트 확인에 실패했습니다. 일반 기능은 계속됩니다.|UpdateAvailable=안정 버전 업데이트가 있습니다: {0} → {1}|UpdateNow=업데이트 다운로드 및 준비|Later=나중에|UpdateSkipped=버전 {0}을(를) 나중으로 미뤘습니다.|Preparing=업데이트 다운로드 및 검증 중…|Prepared=업데이트가 검증되었습니다. ACT를 종료하면 적용됩니다. 이후 ACT를 다시 시작하세요.|UpdateFailed=업데이트를 준비할 수 없습니다: {0}")
        };
        static Localization()
        {
            R[English]["DamageDealt"] = "Damage dealt"; R[English]["DamageTaken"] = "Damage taken";
            R[Japanese]["DamageDealt"] = "与ダメージ"; R[Japanese]["DamageTaken"] = "被ダメージ";
            R[Chinese]["DamageDealt"] = "造成伤害"; R[Chinese]["DamageTaken"] = "承受伤害";
            R[Korean]["DamageDealt"] = "준 피해"; R[Korean]["DamageTaken"] = "받은 피해";
            R[English]["ManualUpdateOnly"] = "Automatic installation is disabled. Download the release and replace the plugin after closing ACT.";
            R[Japanese]["ManualUpdateOnly"] = "自動インストールは無効です。Releaseをダウンロードし、ACT終了後にプラグインを置き換えてください。";
            R[Chinese]["ManualUpdateOnly"] = "自动安装已禁用。请下载Release，并在关闭ACT后替换插件。";
            R[Korean]["ManualUpdateOnly"] = "자동 설치가 비활성화되었습니다. Release를 다운로드하고 ACT를 종료한 뒤 플러그인을 교체하세요.";
            R[English]["HistoryQueueFull"] = "History queue is full; could not accept {0} records.";
            R[Japanese]["HistoryQueueFull"] = "履歴キューが満杯のため、{0}件を受け付けられませんでした。";
            R[Chinese]["HistoryQueueFull"] = "历史队列已满，无法接收{0}条记录。";
            R[Korean]["HistoryQueueFull"] = "기록 대기열이 가득 차서 {0}개 항목을 받지 못했습니다.";
            R[English]["HistoryShutdownFailure"] = "Could not flush {0} pending history records: {1}";
            R[Japanese]["HistoryShutdownFailure"] = "終了時に履歴{0}件を保存できませんでした: {1}";
            R[Chinese]["HistoryShutdownFailure"] = "退出时无法保存{0}条待处理记录：{1}";
            R[Korean]["HistoryShutdownFailure"] = "종료 중 대기 기록 {0}개를 저장하지 못했습니다: {1}";
            R[Chinese]["UpdateDescription"] = "检查GitHub Releases稳定版。请在关闭ACT后手动安装更新。";
            R[Chinese]["UpdateNow"] = "手动安装说明";
            R[Korean]["UpdateDescription"] = "GitHub Releases 안정 버전을 확인합니다. ACT를 종료한 후 수동으로 설치하세요.";
            R[Korean]["UpdateNow"] = "수동 설치 안내";
            R[English]["RecordAssists"] = "Record and display assists";
            R[Japanese]["RecordAssists"] = "アシストを記録・表示する";
            R[Chinese]["RecordAssists"] = "记录并显示助攻";
            R[Korean]["RecordAssists"] = "도움 기록 및 표시";
            R[English]["EstimateNote"] = "Assists require your direct damage within the time window and a kill by your party/alliance. Unknown affiliation is excluded. Last attack is not a confirmed finishing blow.";
            R[Japanese]["EstimateNote"] = "自分の攻撃から判定秒数以内に、味方パーティ／アライアンスが撃破した場合のみアシストを記録します。所属不明は対象外です。最終攻撃は確定したとどめではありません。";
            R[Chinese]["EstimateNote"] = "仅在您造成直接伤害后的判定时间内，由友方小队或团队成员击杀时记录助攻。归属不明时不记录。最后攻击不代表确定的致命技能。";
            R[Korean]["EstimateNote"] = "내 직접 피해 후 판정 시간 이내에 아군 파티/연합원이 처치한 경우만 도움을 기록합니다. 소속 불명은 제외합니다. 마지막 공격은 확인된 결정타가 아닙니다.";
            foreach (var pair in Table("Kill=Kill|EstimatedAssist=Assist|LastAction=Last attack|Unknown=Unknown|PreviewTarget=Example opponent|PreviewAction=Example action|History=History|OpenHistory=Open history folder|Time=Time|Target=Target|Kind=Result|HistoryError=Could not save history: {0}|LoadError=Could not load history: {0}|Waiting=Waiting for FFXIV player data|FolderError=Could not open the history folder: {0}")) R[English][pair.Key] = pair.Value;
            foreach (var pair in Table("Kill=キル|EstimatedAssist=アシスト|LastAction=最終攻撃|Unknown=不明|PreviewTarget=表示例の対戦相手|PreviewAction=表示例の技|History=履歴|OpenHistory=履歴フォルダーを開く|Time=時刻|Target=対象|Kind=結果|HistoryError=履歴を保存できませんでした: {0}|LoadError=履歴を読み込めませんでした: {0}|Waiting=FFXIVのプレイヤー情報を待っています|FolderError=履歴フォルダーを開けませんでした: {0}")) R[Japanese][pair.Key] = pair.Value;
            foreach (var pair in Table("Kill=击杀|EstimatedAssist=助攻|LastAction=最后攻击|Unknown=未知|PreviewTarget=示例对手|PreviewAction=示例技能|History=历史|OpenHistory=打开记录文件夹|Time=时间|Target=目标|Kind=结果|HistoryError=无法保存记录: {0}|LoadError=无法读取记录: {0}|Waiting=等待FFXIV玩家数据|FolderError=无法打开记录文件夹: {0}")) R[Chinese][pair.Key] = pair.Value;
            foreach (var pair in Table("Kill=처치|EstimatedAssist=도움|LastAction=마지막 공격|Unknown=알 수 없음|PreviewTarget=상대 예시|PreviewAction=기술 예시|History=기록|OpenHistory=기록 폴더 열기|Time=시각|Target=대상|Kind=결과|HistoryError=기록 저장 실패: {0}|LoadError=기록 읽기 실패: {0}|Waiting=FFXIV 플레이어 정보 대기 중|FolderError=기록 폴더를 열 수 없습니다: {0}")) R[Korean][pair.Key] = pair.Value;
        }
        private static Dictionary<string, string> Table(string packed) { var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); foreach (var p in packed.Split('|')) { var n = p.IndexOf('='); d[p.Substring(0, n)] = p.Substring(n + 1); } return d; }
        public static string NormalizeLanguage(string value) { if (string.IsNullOrWhiteSpace(value)) return English; if (value.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return Japanese; if (value.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return Chinese; if (value.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return Korean; return English; }
        public static string MapCulture(CultureInfo value) => NormalizeLanguage(value == null ? null : value.Name);
        public static string FontFamily(string value) { switch (NormalizeLanguage(value)) { case Japanese: return "Yu Gothic UI"; case Chinese: return "Microsoft YaHei UI"; case Korean: return "Malgun Gothic"; default: return "Segoe UI"; } }
        public static string Get(string language, string key, params object[] args) { string value; var normalized = NormalizeLanguage(language); if (key == "BarrierAbsorbed") value = normalized == Japanese ? "バリア吸収 {0} — {1}" : normalized == Chinese ? "护盾吸收 {0} — {1}" : normalized == Korean ? "보호막 흡수 {0} — {1}" : "Barrier absorbed {0} — {1}"; else { Dictionary<string, string> t; if (!R.TryGetValue(normalized, out t) || !t.TryGetValue(key, out value)) R[English].TryGetValue(key, out value); } value = value ?? key; return args == null || args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args); }
    }
}
