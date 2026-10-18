#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STAGE="$ROOT/packaging/stage"
DIST="$ROOT/dist"
VERSION=0.1.0
NAME="轻图-winui-${VERSION}-win64-绿色版"

cd "$ROOT"
# 框架依赖：体积小，需本机已装 .NET 8 桌面运行时 + Windows App SDK 1.6+（Win11 通常自带）
dotnet publish -c Release -r win-x64 -p:Platform=x64 \
  -p:SelfContained=false \
  -p:WindowsAppSdkSelfContained=false \
  -p:PublishTrimmed=false \
  -p:PublishReadyToRun=false \
  -o "$ROOT/publish-out"

PUBLISH="$ROOT/publish-out"
[[ -f "$PUBLISH/qingtu-winui.exe" ]] || { echo "未找到 qingtu-winui.exe"; exit 1; }

PORTABLE_DIR="$DIST/$NAME"
rm -rf "$DIST"
mkdir -p "$PORTABLE_DIR"
cp -a "$PUBLISH/." "$PORTABLE_DIR/"
cp -f "$STAGE/启动.bat" "$STAGE/使用说明.txt" "$PORTABLE_DIR/"

(cd "$DIST" && tar -a -cf "${NAME}.zip" "$NAME")
echo "完成: $DIST/${NAME}.zip ($(du -sh "$PORTABLE_DIR" | cut -f1))"
