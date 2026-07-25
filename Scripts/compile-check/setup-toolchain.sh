#!/usr/bin/env bash
# Downloads the Windows x64 .NET SDK and installs it into the toolchain
# directory. The SDK is executed inside a Proton prefix via protontricks,
# so the *Windows* build of the SDK is required (not the Linux one).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=env.sh
source "$SCRIPT_DIR/env.sh"

SDK_ZIP_URL="https://aka.ms/dotnet/$HUNTERPIE_CC_SDK_CHANNEL/dotnet-sdk-win-x64.zip"
SDK_ZIP_PATH="$HUNTERPIE_CC_TOOLCHAIN_DIR/dotnet-sdk-win-x64.zip"

if [[ -f "$HUNTERPIE_CC_DOTNET_DIR/dotnet.exe" ]]; then
    echo "[setup-toolchain] Windows .NET SDK already present at $HUNTERPIE_CC_DOTNET_DIR"
    exit 0
fi

mkdir -p "$HUNTERPIE_CC_TOOLCHAIN_DIR" "$HUNTERPIE_CC_DOTNET_DIR"

echo "[setup-toolchain] Downloading Windows .NET SDK (channel $HUNTERPIE_CC_SDK_CHANNEL)"
echo "[setup-toolchain]   from: $SDK_ZIP_URL"
curl -fSL --retry 3 --progress-bar -o "$SDK_ZIP_PATH" "$SDK_ZIP_URL"

echo "[setup-toolchain] Extracting to $HUNTERPIE_CC_DOTNET_DIR"
unzip -q -o "$SDK_ZIP_PATH" -d "$HUNTERPIE_CC_DOTNET_DIR"
rm -f "$SDK_ZIP_PATH"

# protontricks is a Flatpak on this system (com.github.Matoking.protontricks).
# Its sandbox shadows ~/.local/share, so the toolchain directory must be
# explicitly shared with it (one-time, user-level override; no root needed).
if readlink -f "$(command -v protontricks)" | grep -q "^/usr/bin/protontricks$" \
   && grep -q "flatpak run" /usr/bin/protontricks 2>/dev/null; then
    FLATPAK_ID="$(sed -n 's|.*flatpak run \([^ ]*\).*|\1|p' /usr/bin/protontricks | head -1)"
    echo "[setup-toolchain] Detected Flatpak protontricks ($FLATPAK_ID)"
    echo "[setup-toolchain] Granting filesystem access to $HUNTERPIE_CC_TOOLCHAIN_DIR"
    flatpak override --user "$FLATPAK_ID" --filesystem="$HUNTERPIE_CC_TOOLCHAIN_DIR"
fi

echo "[setup-toolchain] Done. Verifying installation inside Proton prefix $HUNTERPIE_CC_APPID..."
"$SCRIPT_DIR/run-in-prefix.sh" "$HUNTERPIE_CC_WINE_DOTNET" --version
echo "[setup-toolchain] Toolchain ready."
