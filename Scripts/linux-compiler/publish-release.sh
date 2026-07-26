#!/usr/bin/env bash
# Publishes a Release build of HunterPie using the Windows .NET SDK inside
# the Proton prefix and packages the output as a zip archive.
#
# Output:
#   $HUNTERPIE_CC_TOOLCHAIN_DIR/artifacts/HunterPie-v<version>-win-x64/
#   $HUNTERPIE_CC_TOOLCHAIN_DIR/artifacts/HunterPie-v<version>-win-x64.zip
#
# The output is framework-dependent: it requires the .NET Desktop Runtime 10
# on the target machine (same as official HunterPie releases).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}" )" && pwd)"
# shellcheck source=env.sh
source "$SCRIPT_DIR/env.sh"

# --self-contained: bundle the .NET Desktop Runtime into the output so the
# app runs without any runtime installation (e.g. inside a Proton prefix).
SELF_CONTAINED=0
for arg in "$@"; do
    case "$arg" in
        --self-contained) SELF_CONTAINED=1 ;;
        *) echo "unknown argument: $arg" >&2; exit 2 ;;
    esac
done

if [[ ! -f "$HUNTERPIE_CC_DOTNET_DIR/dotnet.exe" ]]; then
    echo "[publish] Toolchain missing, running setup..."
    "$SCRIPT_DIR/setup-toolchain.sh"
fi

# Ensure packages are restored (Linux-side seed, Wine-side path fixup)
"$SCRIPT_DIR/restore-packages.sh"

VERSION="$(sed -n 's/.*AssemblyVersion("\([^"]*\)").*/\1/p' "$HUNTERPIE_CC_REPO_ROOT/HunterPie/Properties/AssemblyInfo.cs" | head -1)"
VERSION="${VERSION:-0.0.0.0}"

ARTIFACTS_DIR="$HUNTERPIE_CC_TOOLCHAIN_DIR/artifacts"
SUFFIX="win-x64"
[[ "$SELF_CONTAINED" -eq 1 ]] && SUFFIX="win-x64-selfcontained"
OUTPUT_DIR="$ARTIFACTS_DIR/HunterPie-v$VERSION-$SUFFIX"
WINE_OUTPUT_DIR="$(to_wine_path "$OUTPUT_DIR")"

echo "[publish] Publishing HunterPie v$VERSION (Release) to $OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

PUBLISH_ARGS=(
    publish
    "$HUNTERPIE_CC_WINE_REPO_ROOT/HunterPie/HunterPie.csproj"
    --nologo
    -c Release
    -o "$WINE_OUTPUT_DIR"
    -nr:false
    -m:1
    -p:UseSharedCompilation=false
    -p:SkipPostBuild=true
)

if [[ "$SELF_CONTAINED" -eq 1 ]]; then
    PUBLISH_ARGS+=(
        -r win-x64
        --self-contained true
    )
fi

LOG_FILE="$HUNTERPIE_CC_TOOLCHAIN_DIR/logs/publish-$(date +%Y%m%d-%H%M%S).log"
mkdir -p "$HUNTERPIE_CC_TOOLCHAIN_DIR/logs"

"$SCRIPT_DIR/run-in-prefix.sh" "$HUNTERPIE_CC_WINE_DOTNET" "${PUBLISH_ARGS[@]}" 2>&1 | tee "$LOG_FILE" | grep -vE "fixme|wineusb|ntsync"

if [[ ! -f "$OUTPUT_DIR/HunterPie.exe" ]]; then
    echo "[publish] PUBLISH FAILED (see $LOG_FILE)" >&2
    exit 1
fi

# The csproj PostBuild event copies localization files via $(SolutionDir),
# which is undefined when building the project directly (same on Windows CI).
# Copy them explicitly so the shipped package includes Languages/.
if [[ -d "$HUNTERPIE_CC_REPO_ROOT/Localization/localization" ]]; then
    mkdir -p "$OUTPUT_DIR/Languages"
    cp "$HUNTERPIE_CC_REPO_ROOT/Localization/localization/"*.xml "$OUTPUT_DIR/Languages/"
    echo "[publish] Copied $(ls "$OUTPUT_DIR/Languages" | wc -l) localization files"
else
    echo "[publish] WARNING: Localization submodule not initialized, Languages/ will be missing" >&2
fi

# Seed config.json: core logic (scanning, API server, trackers) enabled,
# game overlay UI disabled. HunterPie populates missing properties from
# coded defaults, so only the overrides are shipped.
SEED_CONFIG="$SCRIPT_DIR/package/config.json"
if [[ -f "$SEED_CONFIG" ]]; then
    cp "$SEED_CONFIG" "$OUTPUT_DIR/config.json"
    echo "[publish] Seeded config.json (overlay disabled, core + API enabled)"
fi

if [[ "$SELF_CONTAINED" -eq 0 ]]; then
    echo "[publish] Creating archive..."
    ZIP_PATH="$ARTIFACTS_DIR/HunterPie-v$VERSION-$SUFFIX.zip"
    rm -f "$ZIP_PATH"
    (cd "$ARTIFACTS_DIR" && zip -q -r "$ZIP_PATH" "$(basename "$OUTPUT_DIR")")
    echo "[publish]   archive: $ZIP_PATH ($(du -h "$ZIP_PATH" | cut -f1))"
fi

echo "[publish] Done."
echo "[publish]   folder: $OUTPUT_DIR"
