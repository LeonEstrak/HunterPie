#!/usr/bin/env bash
# Seeds the Proton prefix's NuGet global packages folder using the native
# Linux .NET SDK (signature verification disabled by default on Linux).
#
# The restore targets the SAME folder the Wine-side build reads
# (C:\users\steamuser\.nuget\packages inside the prefix), so the subsequent
# Wine restore finds every package already installed and skips download +
# signature verification entirely.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=env.sh
source "$SCRIPT_DIR/env.sh"

LINUX_SDK_DIR="$HUNTERPIE_CC_TOOLCHAIN_DIR/dotnet-linux"
if [[ ! -x "$LINUX_SDK_DIR/dotnet" ]]; then
    echo "[restore-packages] Linux SDK missing, running setup..."
    "$SCRIPT_DIR/setup-linux-sdk.sh"
fi

PREFIX_NUGET_PACKAGES="$HOME/.local/share/Steam/steamapps/compatdata/$HUNTERPIE_CC_APPID/pfx/drive_c/users/steamuser/.nuget/packages"
if [[ ! -d "$HOME/.local/share/Steam/steamapps/compatdata/$HUNTERPIE_CC_APPID/pfx" ]]; then
    echo "[restore-packages] ERROR: no Proton prefix for AppID $HUNTERPIE_CC_APPID" >&2
    exit 1
fi
mkdir -p "$PREFIX_NUGET_PACKAGES"

echo "[restore-packages] Restoring packages (Linux SDK) into prefix package cache"
echo "[restore-packages]   target: $PREFIX_NUGET_PACKAGES"

cd "$HUNTERPIE_CC_REPO_ROOT"
for project in "$HUNTERPIE_CC_PROJECT" "HunterPie.Core.Tests/HunterPie.Core.Tests.csproj"; do
    echo "[restore-packages] Restoring $project"
    env \
        DOTNET_CLI_TELEMETRY_OPTOUT=1 \
        DOTNET_NOLOGO=1 \
        NUGET_PACKAGES="$PREFIX_NUGET_PACKAGES" \
        NUGET_XMLDOC_MODE=skip \
        "$LINUX_SDK_DIR/dotnet" restore "$project" \
            --nologo \
            -p:EnableWindowsTargeting=true
done

echo "[restore-packages] Restore complete."
