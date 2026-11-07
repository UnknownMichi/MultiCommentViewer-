# CLAUDE.md

このリポジトリは MultiCommentViewer（MCV）のフォークで、VOICEVOX による読み上げと X ライブ配信への対応を追加している。
**作業を始める前に `HANDOFF.md` を読むこと。** 目的・方針・進み具合・次にやることがまとめてある。

## 守ること

- `master` に直接コミットしない。作業は `feature/*` ブランチで行う（現在は `feature/voicevox`）。
- 本体（`MultiCommentViewer/`、各 `*SitePlugin`）への変更は最小限にする。機能はプラグイン（例: `VoicevoxPlugin/`）として足す。本家の更新を取り込みやすくするため。
- 対象は .NET Framework 4.6.2 / WPF / C# 8.0。NAudio は 1.10.0 に固定（2.x は net472 以上が必要）。
- コメントや UI の文言は日本語で書く（既存コードに合わせる）。
- 作業が一区切りしたら `HANDOFF.md` の「進み具合」と「検証の状況」を更新する。
