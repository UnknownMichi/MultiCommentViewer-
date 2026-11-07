# 引き継ぎ書：マルチコメントビューアー拡張（VOICEVOX読み上げ・X対応）

最終更新: 2026-11-07 / 作業ブランチ: `feature/voicevox`

別のPCの Claude Code で作業を続けるための資料です。新しいセッションでは、まずこのファイルを読ませてください。

---

## 1. プロジェクトの目的

OBS で配信するときに使っている「棒読みちゃん」と「マルチコメントビューアー（MCV）」を、1つのアプリにまとめる。そのために MCV をフォークし、機能を追加する。

- **配信先:** YouTube Live と、X（旧Twitter）のライブ配信。X は現行の MCV では取得できない。
- **リポジトリ:** https://github.com/UnknownMichi/MultiCommentViewer-
  - 本家 ryu-s/MultiCommentViewer の v0.6.34（2023年9月）をコピーしたもの
- **ライセンス:** GPL-3.0。自分で使う分には自由。exe を他人に配る場合はソースの公開が必要。
- **検討して見送ったもの:** わんコメ。X に対応しておらず、オープンソースでもないためフォークできない。

## 2. 追加する機能と進み具合

| # | 機能 | 状態 |
|---|---|---|
| 1 | VOICEVOX による読み上げを内蔵する | **実装済み・Windowsでの動作確認待ち**（コミット `e69d394`） |
| 2 | 投稿者ごとに声を変える | 未着手。設計は下の 5. を参照 |
| 3 | X のライブコメントに対応する | 未着手。まず取得方法の調査から。下の 6. を参照 |

## 3. 決めた方針

1. **本体はほぼ触らず、新しいプラグインとして足す。**
   - 読み上げ機能は `VoicevoxPlugin`（IPlugin）として実装する。既存の `BouyomiPlugin` と同じ構造。
   - 本体側の変更は、ソリューションへの登録と、ビルド後のコピー処理の追加だけにとどめる。
   - 理由: 本家は YouTube の仕様変更への対応をよく出すので、それを後から取り込みやすくするため。
2. **ブランチ運用:** `master` に直接コミットせず、`feature/*` で作業する。PR はユーザーが頼んだときだけ作る。
3. **ビルドと動作確認はユーザーが Windows で行う。** MCV は .NET Framework 4.6.2 の WPF アプリで、Linux ではフルビルドも実行もできない。Claude 側の検証方法は 8. を参照。
4. **棒読みちゃん連携は削除しない。** ただし両方を有効にすると同じコメントを二重に読み上げるので、棒読みちゃん側はオフにしてもらう。

## 4. 実装済み：VoicevoxPlugin（ステップ1）

### ファイル構成

```
VoicevoxPlugin/
  VoicevoxPlugin.csproj   net462 / WPF / NAudio 1.10.0 / MvvmLight（Common経由）
  main.cs                 IPluginの実装。OnMessageReceived → 読み上げる文を作る → キューに入れる
  Options.cs              設定（DynamicOptionsBase。名前=値 の行形式で保存）
  VoicevoxClient.cs       VOICEVOXのHTTP API（/version, /speakers, /audio_query, /synthesis）
  SpeechQueue.cs          1本のワーカーで「合成→再生」を順番に処理する。上限を超えたら古いものを捨てる
  AudioPlayer.cs          NAudioのWaveOutEventで再生。出力先はデバイス名で指定
  SpeechTextBuilder.cs    名前・敬称・URL省略・文字数での切り捨て
  ConfigView.xaml(.cs)    設定画面（×ボタンでは閉じずに隠す。BouyomiPluginと同じ作り）
  ConfigViewModel.cs      設定画面のViewModel
  README.md               ユーザー向けの使い方
```

本体側の変更点:
- `MultiCommentViewer.sln`: プロジェクトを登録。GUID は `{F083C64C-D43E-47F7-80E3-EA2763927142}`。MultiCommentViewer の依存プロジェクトにも追加した。
- `MultiCommentViewer/MultiCommentViewer.csproj`: ProjectReference を追加。PostBuild で `plugins\Voicevox` へコピーする処理を追加。

### 処理の流れ

1. `OnMessageReceived` でコメントを受け取る。
2. 次のものは読まない: 無効設定のとき、NGユーザー（`IsNgUser` / `IsSiteNgUser`）、接続直後の過去コメント（`IsInitialComment`）、184コメント（設定でオンにしていない場合）。
3. 名前とコメントを取り出す。各サイトへの対応は既存の `PluginCommon.Tools.GetData(message)` をそのまま使っている。
4. コテハン（`messageMetadata.User.Nickname`）があれば、名前の代わりに使う。
5. `SpeechTextBuilder.Build` で「名前さん、コメント」の形の文を作る。
6. `ResolveSpeakerId(metadata)` で話者を決める。今は設定値の話者を返すだけで、ステップ2でここを差し替える。
7. `SpeechQueue.Enqueue` でキューに入れる。ワーカーが `/audio_query` → パラメータ上書き → `/synthesis` → 再生を、終わるまで待ってから次に進む。

### 主な設定値（既定値）

| 項目 | 既定値 |
|---|---|
| EngineUrl | `http://localhost:50021` |
| SpeakerId | 14（冥鳴ひまり ノーマル）。春日部つむぎ ノーマルは 8 |
| 話速 / 音高 / 抑揚 / 音量 | 1.0 / 0.0 / 1.0 / 1.0 |
| 最大文字数 | 60 |
| 読み上げ待ちの上限 | 10 |
| 出力先デバイス | 既定のデバイス |

### 検証の状況

- [x] Linux で net462 向けの C# コンパイル（XAML を除く）が通った
- [x] 偽の VOICEVOX サーバーを相手にした29項目のテストがすべて通った
  - 文の組み立て、設定の保存と読み込み（小数点が「,」になる地域設定を含む）、API への送信内容、エラー時のメッセージ
- [x] XAML の書式が正しいこと、バインディング名が ViewModel のプロパティと一致していること
- [ ] **Windows / Visual Studio でのフルビルド**（XAML のコンパイルを含む）← 次にやること
- [ ] MCV 本体のプラグインメニューに表示され、設定画面が開くこと
- [ ] 実際の VOICEVOX でのテスト再生と、出力先デバイスの切り替え
- [ ] YouTube Live の実際のコメントが読み上げられること

### Windows での確認手順

1. `git fetch` → `git checkout feature/voicevox`
2. Visual Studio 2022 で `MultiCommentViewer.sln` を開いてビルドする
   - submodule `YouTubeLiveMessageParser` が空の場合は、先に `git submodule update --init` を実行する
3. VOICEVOX を起動し、MCV の「プラグイン」→「VOICEVOX読み上げ」を開く
4. 「接続テスト・話者一覧を取得」→ 話者を選ぶ →「テスト再生」
5. 棒読みちゃん連携をオフにし、「コメントをVOICEVOXで読み上げる」をオンにして、YouTube のコメントで確認する

## 5. 次にやること：ステップ2（投稿者ごとに声を変える）の設計案

- **話者の候補リスト:** 設定画面で `/speakers` の一覧から複数の話者にチェックを入れて決める（例: 冥鳴ひまり、春日部つむぎ など落ち着いた声）。
- **自動割り当て:** 「サイトのGUID（`messageMetadata.SiteContextGuid`）+ ユーザーID（`messageMetadata.User.UserId`）」から固定のハッシュ値（FNV-1a や SHA1 など）を計算し、`候補リスト[hash % 件数]` を使う。
  - **`string.GetHashCode()` は使わない。** 実行環境によって値が変わるので、再起動のたびに声が変わってしまう可能性がある。
- **手動での上書き:** ユーザーごとに声を指定できるようにし、別ファイルの JSON（例: `settings/VOICEVOX読み上げ_users.json`）に保存する。
  - `DynamicOptionsBase` は行を `=` で分割するので、値に `=` を含むデータ（辞書など）は入れられない。そのため別ファイルにする。
  - 設定画面では、コメント一覧のユーザーから指定するか、ID を入力して指定する。どちらにするかはユーザーと相談する。
- **組み込む場所:** `VoicevoxPlugin/main.cs` の `ResolveSpeakerId`。
- 候補リストが空なら、今と同じく `SpeakerId` を使う。
- 判定ロジック（ハッシュ・割り当て）は純粋な関数として書き、8. の方法でテストする。

## 6. その次：ステップ3（X対応）の調査メモ

- X のライブ配信は Periscope の仕組みの上で動いている。MCV には古い `PeriscopeSitePlugin` が残っている（`proxsee.pscp.tv/api/v2/accessVideo` などを使用）。
- 調べること:
  - 今の X ライブ（`x.com/i/broadcasts/...`）のコメントを、ログインなしで、またはブラウザのクッキーで取れるか
  - チャットに使われている API や WebSocket（chatman 系など）が今も同じ形で使えるか
- 公式 API は有料なので、最後の手段にする。
- 実装はステップ1と2が終わってから、別ブランチ（例: `feature/x-live`）で行う。

## 7. 注意点・ハマりどころ

- **NAudio は 1.10.0 に固定する。** 2.x は net472 以上が必要で、このプロジェクト（net462）では使えない。
- **double の保存は InvariantCulture で行う。** 地域設定によっては小数点が「,」になるため。`Options.DoubleItem` を参照。
- **Options のプロパティは `dynamic` で値を持っている。** int のプロパティに double を代入すると、実行時エラーになる。ViewModel では型をそろえてから代入する。
- **プラグインの読み込み:** `plugins\<サブフォルダ>\*Plugin.dll` が MEF で読み込まれる（`IPluginManager.cs`）。アセンブリ名は `...Plugin.dll` で終わる必要がある。
- **VOICEVOX のクレジット表記:** 配信や動画で使う場合は「VOICEVOX:冥鳴ひまり」のような表記が必要。設定画面にも注意書きを入れてある。
- `MultiCommentViewer/MultiCommentViewer.csproj.rej` と `TexTraTranslate.cs` は本家由来のファイルなので、触らなくてよい。

## 8. Claude Code での検証方法（Windows でない場合）

Linux のクラウド環境では、WPF の SDK（`Microsoft.NET.Sdk.WindowsDesktop`）がないため、フルビルドはできない。代わりに次の2つで確認していた。

1. **コンパイルチェック:** `Sdk="Microsoft.NET.Sdk"`・net462 の検証用 csproj をリポジトリ外（scratchpad）に作る。
   - `VoicevoxPlugin/*.cs` と `Common/DynamicOptionsBase.cs` を直接 Compile に含める
   - `IPlugin` / `ISitePlugin` / `PluginCommon` は ProjectReference で参照する（これらは普通の SDK なので Linux でもビルドできる）
   - `ConfigView.InitializeComponent()` は空のメソッドを用意して代用する
   - パッケージ: NAudio 1.10.0、Newtonsoft.Json 13.0.1、MvvmLightLibs 5.4.1.1、System.ValueTuple
2. **ロジックテスト:** net8.0 のコンソールアプリに `Options.cs` / `SpeechTextBuilder.cs` / `VoicevoxClient.cs` / `DynamicOptionsBase.cs` を含めて実行する。VOICEVOX の代わりに、Python で書いた偽サーバー（`/version`, `/speakers`, `/audio_query`, `/synthesis` に応答するもの）を相手にする。

.NET SDK は `apt-get update && apt-get install -y dotnet-sdk-8.0` で入る。dot.net からのダウンロードはプロキシで遮断されていた。

**Windows の PC で Claude Code を使う場合は、こうした代用は不要。** Visual Studio の MSBuild（または `dotnet build`）で、ソリューションを直接ビルドして確認できる。

## 9. 新しいセッションの始め方（例）

> HANDOFF.md を読んで、続きから作業して。まず feature/voicevox を Windows でビルドして、エラーがあれば直して。

または

> HANDOFF.md を読んで、ステップ2（投稿者ごとの声色）を実装して。
