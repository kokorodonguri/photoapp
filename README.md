# PhotoCuller

フォルダー内の写真を「いる / いらない」で素早く仕分けし、
「いらない」だけを `_rejected` フォルダーへ移動するWindowsアプリです。

## 使い方

1. アプリを起動して「フォルダーを選択」をクリック
2. 画像を確認しながら以下のキーまたはボタンで仕分け
   - いる: `K` / `→` / `Space`
   - いらない: `D` / `Delete`
   - 元に戻す: `U`

## 対応拡張子

- JPG / PNG
- 一部のRAW形式（環境にインストールされているWICコーデックに依存）

## ビルド / 実行

```bash
# Visual Studio / dotnet CLI どちらでもOK

dotnet build PhotoCuller.sln

dotnet run --project PhotoCuller/PhotoCuller.csproj
```

## インストーラー作成 (Inno Setup)

Windows上で以下を実行してください。

1. 発行 (publish)

```bash
dotnet publish PhotoCuller/PhotoCuller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/PhotoCuller
```

2. Inno Setupで `installer/PhotoCuller.iss` を開き、Compile を実行  
   出力先: `dist/PhotoCuller-Setup.exe`

## メモ

- サブフォルダーは対象外（フォルダー直下のみ）です。
- RAWのプレビューはWindowsにインストールされているコーデックに依存します。
