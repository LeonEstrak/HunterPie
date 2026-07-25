#!/usr/bin/env bash
# Validates that HunterPie compiles, using the Windows .NET SDK running
# inside a Proton prefix (via protontricks).
#
# Usage:
#   ./compile-check.sh [--tests] [--configuration Debug|Release]
#
# Mirrors .github/workflows/pull-request.yaml: builds the HunterPie project
# (transitively builds Core, DI, Integrations, Platforms, UI) and optionally
# runs the HunterPie.Core.Tests unit tests.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=env.sh
source "$SCRIPT_DIR/env.sh"

RUN_TESTS=0
while [[ $# -gt 0 ]]; do
    case "$1" in
        --tests) RUN_TESTS=1; shift ;;
        --configuration) export HUNTERPIE_CC_CONFIGURATION="$2"; shift 2 ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done

if [[ ! -x "$HUNTERPIE_CC_DOTNET_DIR/dotnet.exe" ]]; then
    echo "[compile-check] Toolchain missing, running setup..."
    "$SCRIPT_DIR/setup-toolchain.sh" || exit 1
fi

LOG_DIR="$HUNTERPIE_CC_TOOLCHAIN_DIR/logs"
mkdir -p "$LOG_DIR"
BUILD_LOG="$LOG_DIR/build-$(date +%Y%m%d-%H%M%S).log"

# Seed the prefix's NuGet cache from Linux first: Wine cannot validate
# NuGet package signatures (NU3028/NU3037), but already-installed packages
# are accepted by the Wine-side restore without verification.
"$SCRIPT_DIR/restore-packages.sh" >> "$BUILD_LOG" 2>&1 || {
    echo "[compile-check] PACKAGE RESTORE FAILED (see $BUILD_LOG)"
    exit 1
}

# -nr:false            -> do not keep MSBuild nodes resident (Wine stability)
# -m:1                 -> single-process build (Wine stability)
# UseSharedCompilation -> avoid the Roslyn compiler server (named pipes under Wine)
BUILD_ARGS=(
    build
    "$HUNTERPIE_CC_WINE_REPO_ROOT/$HUNTERPIE_CC_PROJECT"
    --nologo
    -c "$HUNTERPIE_CC_CONFIGURATION"
    -nr:false
    -m:1
    -p:UseSharedCompilation=false
)

echo "[compile-check] Building $HUNTERPIE_CC_PROJECT ($HUNTERPIE_CC_CONFIGURATION) in prefix $HUNTERPIE_CC_APPID"
echo "[compile-check] Log: $BUILD_LOG"

"$SCRIPT_DIR/run-in-prefix.sh" "$HUNTERPIE_CC_WINE_DOTNET" "${BUILD_ARGS[@]}" 2>&1 | tee "$BUILD_LOG"
# tee masks the exit code; detect failure from the compiler output instead.
if grep -qE "error (CS|MSB|NETSDK|NU)[0-9]+" "$BUILD_LOG"; then
    echo "[compile-check] BUILD FAILED (see $BUILD_LOG)"
    exit 1
fi
if grep -q "Build succeeded" "$BUILD_LOG"; then
    echo "[compile-check] BUILD SUCCEEDED"
else
    echo "[compile-check] WARNING: could not confirm build success marker (see $BUILD_LOG)"
fi

if [[ "$RUN_TESTS" -eq 1 ]]; then
    TEST_ARGS=(
        test
        "$HUNTERPIE_CC_WINE_REPO_ROOT/HunterPie.Core.Tests/HunterPie.Core.Tests.csproj"
        --nologo
        -c "$HUNTERPIE_CC_CONFIGURATION"
        -nr:false
        -m:1
        -p:UseSharedCompilation=false
    )
    echo "[compile-check] Running unit tests..."
    "$SCRIPT_DIR/run-in-prefix.sh" "$HUNTERPIE_CC_WINE_DOTNET" "${TEST_ARGS[@]}" 2>&1 | tee -a "$BUILD_LOG"
fi
