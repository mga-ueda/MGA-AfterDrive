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
- エントリごとの Delay（秒）に従った順次起動（起動済みプロセスはスキップ）
- Restart チェックで切断時の強制終了・復帰時の再起動を指定（Google Drive 上のアプリは自動で ON）
- Setting 表示中はカウントダウンを一時停止。Delay を変更して保存した場合は、再開時にタイマーを再設定
- タスクトレイ常駐、起動時のトレイ格納オプション、二重起動防止
- ダークテーマ UI（DPI 対応）

## 動作環境

- Windows 10 / 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（フレームワーク依存の単一 EXE）
- [Google Drive for desktop](https://www.google.com/drive/download/)

## 使い方

1. `MGA-AfterDrive.exe` を起動します。初回（設定未作成時）は Setting が開きます。
2. Setting に実行ファイル（`.exe` / `.bat` / `.cmd` / `.com`）をドラッグ＆ドロップで追加します。
3. 各行の **Delay**（秒）や起動引数（**Option**）を編集し、**Save** します。
4. メインアプリが Google Drive の準備完了後、登録順（Delay 順）にアプリを起動します。

**Restart** にチェックを入れると、Google Drive 切断時に強制終了し、復帰後に再起動します。パスが Google Drive マウント配下のときは自動でオンになります（手動のオンオフも可能です）。

トレイアイコンから Setting を開き直せます。

## 設定の保存場所

```
%LocalAppData%\MGA\MGA AfterDrive\
  delay-entries.json   … 起動エントリ
  settings.json        … 最大待機時間・トレイ起動など
  app\                 … 公開版で展開される Setting（自動）
```

## 開発ビルド

```powershell
dotnet build src\MGA-AfterDrive\MGA-AfterDrive.csproj -c Debug
```

出力先の例:

```
src\MGA-AfterDrive\bin\Debug\net8.0-windows\
```

メイン EXE と同じフォルダに Setting もコピーされます。

## リリース（単一 EXE）

配布物は **`MGA-AfterDrive.exe` のみ**です。Setting は EXE 内に埋め込まれ、初回起動時に `%LocalAppData%\MGA\MGA AfterDrive\app\` へ展開されます。

```powershell
.\publish.ps1
```

または:

```powershell
dotnet publish src\MGA-AfterDrive\MGA-AfterDrive.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o publish
```

出力: `publish\MGA-AfterDrive.exe`

完全オフライン配布にする場合は `--self-contained true` を付けてください（ファイルサイズが増えます）。

## ライセンス

このソフトウェアは [MIT License](LICENSE) の下で提供されます。

Copyright (c) 2026 MIYABI GAME AUDIO INC.

サードパーティ由来の表記は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
