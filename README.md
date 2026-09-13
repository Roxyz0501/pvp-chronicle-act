# PvP Chronicle for ACT

FFXIV PvPのキルと推定アシストを記録し、直近N件をオーバーレイに表示するACTプラグインです。

## 導入

1. [Releases](https://github.com/Roxyz0501/pvp-chronicle-act/releases/latest) から `PvPChronicle-v1.0.0.zip` をダウンロードして展開します。
2. ACTのPluginsから `PvPChronicle.dll` を追加します。FFXIV_ACT_Pluginを先に有効にしてください。
3. 設定で表示件数（1〜30、既定6）、アシスト判定秒数（1〜60、既定10）、文字サイズ、透明度を指定します。
4. 固定を解除するとドラッグで移動、右下でサイズ変更できます。枠に収まらない履歴はホイールでスクロールできます。固定時はクリック透過です。プレビューは実履歴に保存されません。

英語、日本語、簡体字中国語、韓国語に対応します。初回はOSのUI言語を選択し、以後は設定した言語を保持します。翻訳がないキーは英語にフォールバックします。

## 判定と制限

- **キル（ログ判定）**: プレイヤーの撃破ログ（25）の攻撃者IDが自分。公式スコアボードとの一致を保証するものではありません。
- **推定アシスト**: 他のプレイヤーが撃破した対象に、自分が直前の設定秒数内に直接ダメージを与えていた場合。公式アシストではありません。
- **最終攻撃**: 同じ対象への最後に確認できた自分の直接攻撃（21/22）。撃破と時間的に近い攻撃であり、確定したとどめの技ではありません。対応が取れない場合は不明です。
- 保存時刻は撃破時刻です。最後の攻撃時刻・ID、対象・攻撃者ID、エリア、判定秒数もJSONLに保存します。
- 回復、DoTのみ、ペット由来、反射、環境ダメージ、NPCが倒した対象、ログ欠落によるアシストは検出しません。0ダメージもアシスト対象外です。
- 対象がプレイヤーであることと直接攻撃を利用してPvEのNPC撃破を除外します。コンテンツIDの限定はせず、ウルヴズジェイルのデュエルも含みます。
- ゾーン・キャラクター変更で判定用の攻撃履歴をリセットします。保存済み履歴は残ります。ログの過去インポートは記録しません。
- アシスト判定秒数は独自の推定設定です。10秒はゲーム公式の判定時間を示しません。

形式は [cactbot LogGuide](https://github.com/OverlayPlugin/cactbot/blob/main/docs/LogGuide.md) の21/22（攻撃）、25（撃破）、01（エリア）、02（自分）を参照しています。実際のPvP試合ログによる検証は未実施です。

## 保存先

`%APPDATA%/Advanced Combat Tracker/PvPChronicle/History/YYYY-MM-DD.jsonl`

履歴タブからフォルダーを開けます。UTF-8の1行1JSONです。日別履歴を自動削除しません。設定は `%APPDATA%/Advanced Combat Tracker/Config/PvPChronicle.xml` に保存します。
直近1000件を設定画面へ読み込み、現在のプレイヤーIDに一致する直近N件をオーバーレイに表示します。再ログインでIDが変化した場合、以前の履歴は履歴タブとファイルに残ります。日別ファイルの読み込みは末尾1MiBに限定します。
表示OFFでも記録は継続します。履歴にプレイヤー名が含まれるため、共有するファイルはご自身で確認してください。プラグインは履歴を外部送信しません。

## 更新

専用の更新タブで現在版・最新版・更新内容を確認できます。起動時確認は既定ONで変更可能です。「今すぐ確認」で手動確認、「更新をダウンロードして準備」で明示更新、「後で」で延期します。通信失敗でも記録を継続します。

更新元はこのリポジトリの安定版GitHub Releasesのみです。HTTPS、asset名、SHA-256、パッケージ内容、DLL/Updaterのアセンブリ名・会社名・バージョンを検証します。ZIPはルートの `PvPChronicle.dll`、`PvPChronicle.Updater.exe`、`README.md` のみを許可します。パストラバーサルや他ファイルを拒否します。トークンは使いません。
ACTに登録されている実DLLパスを使用し、取得できなければ更新を中止します。Release内の検証済みUpdaterがACT終了を待ち、バックアップを作って置換します。失敗時は復元します。完了後にACTを再起動してください。ACTでロード中のDLLは上書きしません。

## ビルド・Release

Windows、.NET SDK、.NET Framework 4.8とACTが必要です。

```powershell
./build.ps1 -ActPath 'path/to/Advanced Combat Tracker.exe'
```

GitHub Actionsはmain/PRでビルド・テスト・配布物を検証します。両プロジェクトのVersionを揃え、mainにpushして `vX.Y.Z` タグをpushすると、成功時だけ安定版Releaseを公開します。
assetは `PvPChronicle-vX.Y.Z.zip` と `PvPChronicle-vX.Y.Z.sha256`。マニフェストは `<sha256>  <zip-name>` 形式です。外部ActionはコミットSHAに固定しています。

任意統合テストは `PVPCHRONICLE_UPDATE_INTEGRATION=1` を設定してテストEXEを実行します。公開済み実Releaseを取得・展開してハッシュ、DLL、Updaterを検証します。ACTを終了したり更新を適用したりしません。

## Support / 支援 / 支持 / 후원

[Ko-fiでRoxyz0501の開発を支援](https://ko-fi.com/roxyz0501)。支援は完全に任意です。支援しなくても全機能を利用でき、機能差はありません。
Support is entirely optional; all features are available without supporting. 支持完全自愿，所有功能均可使用。후원은 선택 사항이며 모든 기능을 동일하게 사용할 수 있습니다.

ライセンスは未指定です。
