# VRChatSDK-Inspector翻訳機

Unity Editorの日本語Localizationファイル（`ja.po`）に不足する翻訳を追加し、VRChat SDK Inspectorの英語表示を日本語で補完するWindows向けWinFormsアプリです。

**現在の翻訳データは動作確認用の3件（Pull / Spring / Gravity Falloff）のみです。** VRCSDK全体の日本語翻訳データは、アプリケーションの検証後に追加する予定です。SDK側がUnityのLocalizationを利用していない表示は、この方式では翻訳できません。

## 対応環境

- Windows 64bit（Windows 10 / 11）
- **Unity 2022.3.22f1のみ正式対応**
- 対象Unityの日本語Language Packが必要
- UI言語を日本語にしたUnity Editor

他バージョンはフォルダー名にかかわらず変更を拒否します。Unityの実行ファイルの製品バージョンと`Editor/Data`の構造、`Editor/Data/Localization/ja.po`の存在を確認します。標準パスは次のとおりです。

```text
C:\Program Files\Unity\Hub\Editor\2022.3.22f1
```

## 使い方

1. アプリを通常の権限で起動します。起動だけではUACは表示されません。
2. 初回は標準パスを確認します。前回指定したパスがあれば再利用します。見つからない場合は案内に従い、`Editor`を含むUnityのインストールフォルダーを選択します。`ja.po`そのものを選ぶ必要はありません。
3. 日本語Language Pack、`ja.po`、翻訳のインストール状態を確認します。「インストール先を変更…」から別の場所を選択できます。
4. Language PackがなければUnity Hubで追加します。案内の「日本語化の方法を開く」ボタンから説明ページを開けます。追加後は「状態を再確認」を押します。
5. **Unity Editorをすべて終了**します。実行中のEditorが見つかった場合は変更しません。
6. 「管理者権限で再起動」を押してUACを承認します。Program Files内の共有ファイルを変更するため管理者権限が必要です。UACをキャンセルした場合は元のアプリを継続利用できます。
7. 「インストール」を押します。インストール済みの場合は「再インストール」「アンインストール」が使用できます。
8. 完了後、Unity Editorを起動し直して表示を確認します。

「再インストール」は現在の専用ブロックを除去して、実行中のアプリに埋め込まれたCSVから置き換えます。翻訳更新時は新しいアプリで実行してください。「アンインストール」は専用ブロックを削除します。いずれも実ファイルの変更は1回の置換で行います。

パス設定は`%LOCALAPPDATA%\VRChatSDKInspectorJPLocalization\settings.json`、エラー詳細は同じフォルダーの`errors.log`へ保存します。別ユーザーの管理者資格情報で再起動した場合も、現在選択中のパスは起動引数で引き継ぎます。

## 変更の仕組みと安全性

既存の`ja.po`全体を再生成せず、末尾に次のマーカーで囲んだ専用ブロックを追加します。

```text
# ===== VRChatSDK-Inspector-JP BEGIN =====
…形式バージョン、翻訳バージョン、挿入改行数、内容ハッシュ、POエントリ…
# ===== VRChatSDK-Inspector-JP END =====
```

- 既存の`msgid`があればUnity側を優先し、重複して追加しません。複数行の文字列を連結して比較します。文脈付き・複数形の既存エントリも保守的に優先します。
- CSVのカンマ、引用符、引用符内の改行を扱い、POの引用符・バックスラッシュ・改行をエスケープします。
- UTF-8とLFを検証し、元のBOM有無を保持します。CRLF、不正UTF-8、空ファイル、未対応のPO構文は勝手に変換せず中止します。
- 対象Unityの既存POにはC1文字や`msgstr`内の非標準エスケープ（`\ n`など）が含まれるため、これらは修正せず原文のまま維持します。重複判定に必要な`msgid`内の未知のエスケープは拒否します。
- マーカーの片側欠落・重複・逆順、ブロックの形式やハッシュの不一致では変更しません。本ツールのブロックを直接編集した場合も、自動修復や自動削除を行いません。
- 専用ブロックだけでなく追加した区切り改行数も記録し、アンインストール時に元の末尾を復元します。ブロックより後ろに追加された他の内容は残します。
- 操作直前に対象と権限を再確認します。名前付きMutexで同じパスへの本ツールの同時操作を防ぎ、元ファイルは書き込み共有なしで開きます。
- 変更内容をメモリ上で検証した後、**元のバイト列をバックアップして検証**します。失敗したら置換へ進みません。
- 同じフォルダーに新規一時ファイルを作成してフラッシュ・検証し、元ファイルが変わっていないことを再確認して`File.Replace`で置換します。置換後もバイト列を検証します。失敗時に直接上書きする代替処理はありません。
- 読み取り専用ファイル、シンボリックリンク、ジャンクションを含むパスは変更を拒否します。

バックアップは元ファイルと同じフォルダーに`ja.po.vrcjp-<UTC日時>-<GUID>.bak`として、操作ごとに作成します。既存のバックアップは上書き・自動削除しません。失敗時の一時ファイルが残る場合があります。

異常時は表示される理由と詳細ログを確認してください。バックアップを手動復元する場合はUnityを終了し、対象Editorとバックアップ作成日時を確認したうえで行ってください。バックアップ全体を戻すと、それ以降の他ツールや手作業による変更も戻るため、通常のアンインストールには使用しません。

このツールは同じEditorを使う全プロジェクトの表示に影響します。Unityのアップデート・再インストール・Language Packの再導入で`ja.po`が置き換わることがあります。更新後は状態を再確認してください。別バージョンのUnityへ古い`ja.po`やバックアップをコピーしないでください。

外部ツールによる同時置換や電源断など、OS・ストレージ全体の障害まで完全に防げるものではありません。Unityや他のLocalization編集ツールを終了した状態で使用してください。

## プロジェクト構成

```text
VRChatSDKInspectorJPLocalization.sln
src/
  VRChatSDKInspectorJPLocalization/       WinForms UIとWindows固有処理
    Forms/MainForm.cs                    画面と状態表示
    Services/UnityDetector.cs            対象Unityの検証
    Services/AdministratorService.cs     管理者権限とUAC再起動
    Services/AppSettings.cs              パス設定の保存
    Services/TranslationInstaller.cs     操作の事前条件と更新の連携
  VRChatSDKInspectorJPLocalization.Core/  UIに依存しない処理
    TranslationCsvReader.cs              CSV解析と埋め込みデータ読み込み
    PoSyntax.cs                          PO構文・エスケープ・msgid収集
    PoDocument.cs                        状態判定と専用ブロックの追加・削除
    Utf8Document.cs                      UTF-8/LF検証とBOM保持
    AtomicPoFile.cs                      バックアップとファイル置換
    AppConstants.cs                      バージョン・パス・マーカー・案内URL
    Resources/translations.csv           編集可能なサンプル翻訳
tests/
  VRChatSDKInspectorJPLocalization.Tests/ 依存パッケージ不要の回帰テスト
```

フォームはコードで構築しています。デザイナー生成ファイルはありません。標準.NET APIを使用し、アプリ・テストともに外部NuGetパッケージへの依存はありません。

PO構文の参考: [GNU gettextのPOエントリ仕様](https://www.gnu.org/software/gettext/manual/html_node/PO-File-Entries.html)、[文脈付きエントリ](https://www.gnu.org/software/gettext/manual/html_node/Entries-with-Context.html)。未知の構文に遭遇した場合は既存内容を推測して更新せず、対応実装とテストを追加する方針です。

## 開発とビルド

Windowsと**.NET 8 SDK**が必要です。互換性のある新しいSDKからも.NET 8ターゲットをビルドできます。初回の復元・発行で必要な.NETの参照パックやランタイムパックを取得するため、ネットワーク接続が必要になる場合があります。

Visual Studioでは.NET 8対応のVisual Studio 2022と「.NETデスクトップ開発」ワークロードを使用してsolutionを開きます。VSCodeではC#拡張と.NET SDKを用意します。`.vscode/tasks.json`と`launch.json`を含んでいます。

リポジトリルートから実行します。

```powershell
dotnet build
# またはReleaseビルド
.\build.bat
```

`build.bat`はrestoreとReleaseビルドを実行します。エラーで中断して終了コード1、成功時は終了コード0を返し、出力先を表示します。英語のコンソール表示でバッチの日本語文字化けを避けています。出力先は`src/VRChatSDKInspectorJPLocalization/bin/Release/net8.0-windows/`です。実行ファイルは`VRChatSDKInspectorJPLocalization.exe`です。開発用ビルドの起動には.NET 8 Desktop Runtimeが必要です。

```powershell
dotnet run --project tests/VRChatSDKInspectorJPLocalization.Tests -c Release
```

テストはコンソール型の小規模な回帰テストです。各結果と合計を表示し、失敗時には終了コード1を返します。**`dotnet test`では実行されません。** CSV、PO構文、重複回避、マーカー異常、再インストール、既存内容の保存、UTF-8/LF、バックアップとファイル置換を検証します。通常のテストは文字列とテスト専用一時フォルダーだけを使用し、Program Files内の実ファイルを変更しません。

実環境のUnityについて、検出とメモリ内での変換・復元を追加検証する場合は次を実行できます。このオプションでも実ファイルへの書き込みは行いません。

```powershell
dotnet run --project tests/VRChatSDKInspectorJPLocalization.Tests -c Release -- --inspect-unity "C:\Program Files\Unity\Hub\Editor\2022.3.22f1"
```

制限された環境でMSBuildの並列ワーカーを起動できない場合は`dotnet build -m:1 -nr:false`で確認できます。

初期実装の実測結果と残る手動確認項目は[検証記録](docs/verification.md)を参照してください。

## 配布用の発行

```powershell
.\publish.bat
```

Release / `win-x64` / Self-contained / Single Fileで発行します。配布物は`artifacts/publish/win-x64/VRChatSDKInspectorJPLocalization.exe`です。CSVと.NET Runtimeは含まれるため、利用者によるCSVの配置や.NET Runtimeのインストールは不要です。WinFormsの互換性を優先し、トリミングは無効です。ネイティブライブラリは起動時に.NETの一時領域へ展開されます。

`build.bat`、`publish.bat`には管理者権限は不要で、Unityや`ja.po`へ触れる処理は含みません。出力、キャッシュ、発行成果物はGit管理から除外します。

## 翻訳データの更新

`src/VRChatSDKInspectorJPLocalization.Core/Resources/translations.csv`をUTF-8で編集します。見出しは`English,Japanese`です。カンマ・引用符・改行を含むフィールドは一般的なCSVの引用規則に従ってください。

`#`で始まる独立した行をコメントとして記述できます。コンポーネント名や補足の記載に使用してください。コメントの前の半角スペース・タブ、レコード間の空行も使用できます。見出しはファイル内に1回だけ記載します（見出しより前のコメントも可能です）。

```csv
English,Japanese

# コンポーネントA
Pull,引っ張り
Spring,ばね

# コンポーネントB
# 他のコンポーネントと同じ項目を再掲しても問題ありません
Pull,引っ張り
Gravity Falloff,重力減衰
```

同じEnglishが複数回登場してもエラーにはせず、POへ追加する際は1件にまとめます。**Japaneseが異なる場合も、CSVの先頭側にある訳を採用**します。英語は大文字・小文字や前後の空白を区別して比較し、フィールド内の改行はLFに統一して比較します。既存のUnityのja.poに同じmsgidがある場合は、従来どおりUnity側を優先します。

コンポーネント名のコメントは辞書整理用です。現在の方式では、同じ英語の訳をコンポーネントごとに切り替えることはできません。

コメントは独立した行だけに記載してください。`Pull,引っ張り # コメント`のように訳の後ろへ書いた文字は、訳そのものとして扱われます。`#`で始まる英語は`"#Label",ラベル`のように引用符で囲んでください。引用符内の改行や`#`で始まる行・空行は翻訳文の一部として維持します。

空欄、列数の誤り、引用符の不正は引き続きエラーになります（重複した行も形式を確認します）。コメント拡張を含むため、外部のCSV編集ツールを使う場合はコメント行が維持されることを確認してください。

更新時は`AppConstants.TranslationVersion`も変更し、テスト・ビルド・発行を実行します。CSVはEmbedded Resourceとして組み込まれるため、配布EXEの隣へCSVを配置しても翻訳は変更されません。Unityの対応バージョンや案内URL、マーカーも`AppConstants`で管理します。日本語化案内のURLは暫定的なUnity公式マニュアルで、専用案内ページへの差し替えTODOがあります。

## ライセンス

未選択です。`LICENSE`は未選択であることを示すファイルで、特定のOSSライセンス本文ではありません。権利者によるライセンス選択後に更新してください。
