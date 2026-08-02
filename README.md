# MGA AfterDrive

Google Drive for desktop の起動・接続を待ってから、登録したアプリを遅延起動する Windows 用ユーティリティです。

Google Drive が一時的に切断されたときは、Drive 上のアプリを強制終了し、復帰後に再起動します。

| 名称 | 説明 |
|------|------|
| **MGA AfterDrive** | メインアプリ（監視・遅延起動・トレイ常駐） |
| **MGA AfterDrive Setting** | 起動エントリと共通設定の編集 |

現在のバージョン: **1.0.0**

## 主な機能

- Google Drive プロセス（`GoogleDriveFS`）とマウント先へのアクセス確認
- エントリごとの Delay（秒）に従った順次起動
- Google Drive 上のアプリを Restart 対象として自動判定（切断時に強制終了、復帰時に再起動）
- Setting 表示中はカウントダウンを一時停止。Delay を変更して保存した場合は、再開時にタイマーを再設定
- タスクトレイ常駐、起動時のトレイ格納オプション
- ダークテーマ UI（DPI 対応）

## 動作環境

- Windows 10 / 11
- [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) Desktop Runtime（ビルド時は SDK）
- [Google Drive for desktop](https://www.google.com/drive/download/)

## 使い方

1. `MGA-AfterDrive.exe` を起動します。初回（設定未作成時）は Setting が開きます。
2. Setting に実行ファイル（`.exe` / `.bat` / `.cmd` / `.com`）をドラッグ＆ドロップで追加します。
3. 各行の **Delay**（秒）や起動引数（**Option**）を編集し、**Save** します。
4. メインアプリが Google Drive の準備完了後、登録順（Delay 順）にアプリを起動します。

**Restart** 列は読取専用です。パスが Google Drive マウント配下のとき自動で ✓ が付き、切断／復帰時の管理対象になります。

トレイアイコンから Setting を開き直せます。

## 設定の保存場所

```
%LocalAppData%\MGA\MGA AfterDrive\
  delay-entries.json   … 起動エントリ
  settings.json        … 最大待機時間・トレイ起動など
```

## ビルド

```powershell
dotnet build src\MGA-AfterDrive\MGA-AfterDrive.csproj -c Release
```

出力先の例:

```
src\MGA-AfterDrive\bin\Release\net8.0-windows\
```

メイン EXE と同じフォルダに Setting もコピーされます。

## ライセンス

このソフトウェアは [MIT License](LICENSE) の下で提供されます。

Copyright (c) 2026 MIYABI GAME AUDIO INC.

サードパーティ由来の表記は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
