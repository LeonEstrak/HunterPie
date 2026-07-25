#!/usr/bin/env bash
# Installs the NATIVE Linux .NET SDK into the toolchain directory.
#
# Why a Linux SDK when we build under Wine? NuGet signature verification
# (NU3028/NU3037) cannot pass under Wine because Wine's certificate store
# cannot validate NuGet's timestamping certificates. On Linux, signature
# verification is DISABLED by default, so we use the Linux SDK to seed the
# Proton prefix's NuGet package cache (see restore-packages.sh). Once a
# package is installed in the global packages folder, the Wine-side restore
# no longer needs to verify or download it.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=env.sh
source "$SCRIPT_DIR/env.sh"

LINUX_SDK_DIR="$HUNTERPIE_CC_TOOLCHAIN_DIR/dotnet-linux"
INSTALL_SCRIPT="$HUNTERPIE_CC_TOOLCHAIN_DIR/dotnet-install.sh"

if [[ -x "$LINUX_SDK_DIR/dotnet" ]]; then
    echo "[setup-linux-sdk] Linux .NET SDK already present at $LINUX_SDK_DIR"
    exit 0
fi

mkdir -p "$HUNTERPIE_CC_TOOLCHAIN_DIR"

echo "[setup-linux-sdk] Downloading dotnet-install.sh"
curl -fsSL -o "$INSTALL_SCRIPT" https://dot.net/v1/dotnet-install.sh

echo "[setup-linux-sdk] Installing Linux .NET SDK (channel $HUNTERPIE_CC_SDK_CHANNEL) to $LINUX_SDK_DIR"
bash "$INSTALL_SCRIPT" --channel "$HUNTERPIE_CC_SDK_CHANNEL" --install-dir "$LINUX_SDK_DIR" --no-path

echo "[setup-linux-sdk] Installed: $("$LINUX_SDK_DIR/dotnet" --version)"
