# 開発者向けガイド

ビルド、配布パッケージの作成、翻訳データの編集、内部処理の説明です。利用方法は[利用者向けREADME](README.md)を参照してください。

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

Windowsと**.NET 10 SDK**が必要です。アプリとテストは`net10.0-windows`、翻訳処理ライブラリは`net10.0`を対象としています。初回の復元・発行で必要な.NETの参照パックやランタイムパックを取得するため、ネットワーク接続が必要になる場合があります。

Visual Studioでは.NET 10対応のVisual Studio 2026（18.0以降、使用するSDKに対応したバージョン）と「.NETデスクトップ開発」ワークロードを使用してsolutionを開きます。VSCodeではC#拡張と.NET 10 SDKを用意します。`.vscode/tasks.json`と`launch.json`を含んでいます。SDKとVisual Studioの組み合わせは[公式の互換性情報](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)を参照してください。

リポジトリルートから実行します。

```powershell
dotnet build
# またはReleaseビルド
.\build.bat
```

`build.bat`はrestoreとReleaseビルドを実行します。エラーで中断して終了コード1、成功時は終了コード0を返し、出力先を表示します。英語のコンソール表示でバッチの日本語文字化けを避けています。出力先は`src/VRChatSDKInspectorJPLocalization/bin/Release/net10.0-windows/`です。実行ファイルは`VRChatSDKInspectorJPLocalization.exe`です。開発用ビルドの起動には.NET 10 Desktop Runtimeが必要です。

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

Release / `win-x64` / Self-contained / Single Fileで発行し、続いて`scripts/package.ps1`で配布フォルダーを作成します。Windows標準のWindows PowerShellを使用するため、追加のインストールは不要です。完成したフォルダーは次の2ファイルだけを含みます。

```text
artifacts/distribution/VRChatSDKInspectorJPLocalization-win-x64/
├─ VRChatSDKInspectorJPLocalization.exe
└─ README.md
```

**この`VRChatSDKInspectorJPLocalization-win-x64`フォルダーをZIPに圧縮して配布してください。** ZIPの作成は自動では行いません。READMEはリポジトリ直下の利用者向け`README.md`のコピーで、開発者向けの本ファイルは含めません。利用者向けREADMEには配布先で壊れる相対リンクを使用しないでください。

`artifacts/publish/win-x64/`は発行処理の出力先です。配布用には上記の`artifacts/distribution/`内のフォルダーを使用します。再発行時はEXEとREADMEを更新し、コピー元とのSHA256一致とファイル数を検証します。配布フォルダー内に別のファイルやサブフォルダーがある場合は、勝手に削除せずエラーにするため、追加したものを移動してから再実行してください。フォルダー作成・コピー・検証に失敗した場合も`publish.bat`は終了コード1を返します。失敗後のフォルダーは配布せず、成功後のものを使用してください。

CSVと.NET 10 RuntimeはEXEに含まれるため、利用者によるCSVの配置や.NET Runtimeのインストールは不要です。WinFormsの互換性を優先し、トリミングは無効です。ネイティブライブラリは起動時に.NETの一時領域へ展開されます。

配布容量を抑えるため、`EnableCompressionInSingleFile=true`で埋め込みアセンブリの圧縮を有効にしています。利用者が手動で解凍する必要はありません。起動時にメモリ上で解凍するため、圧縮なしの場合より起動に時間がかかる場合があります。発行設定を変更する際は、サイズと起動動作を確認してください。

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
