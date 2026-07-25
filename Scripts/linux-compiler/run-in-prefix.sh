#!/usr/bin/env bash
# Runs a Windows executable inside the configured Proton prefix.
# Usage: run-in-prefix.sh <exe> [args...]
#
# Each argument is shell-quoted individually, so paths with spaces are safe.
# Use forward-slash Z: paths (see to_wine_path in env.sh) for Windows paths.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=env.sh
source "$SCRIPT_DIR/env.sh"

if [[ $# -lt 1 ]]; then
    echo "usage: $0 <exe> [args...]" >&2
    exit 2
fi

if ! command -v protontricks >/dev/null 2>&1; then
    echo "[run-in-prefix] ERROR: protontricks not found in PATH" >&2
    exit 1
fi

PREFIX_DIR="$HOME/.local/share/Steam/steamapps/compatdata/$HUNTERPIE_CC_APPID"
if [[ ! -d "$PREFIX_DIR/pfx" ]]; then
    echo "[run-in-prefix] ERROR: no Proton prefix found for AppID $HUNTERPIE_CC_APPID at $PREFIX_DIR" >&2
    echo "[run-in-prefix] Set HUNTERPIE_CC_APPID to an installed Proton game (see: protontricks -l)" >&2
    exit 1
fi

# .NET CLI settings, exported on the host side; Wine maps the host
# environment into the Windows process environment.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_UI_LANGUAGE=en
export NUGET_XMLDOC_MODE=skip
export DOTNET_ROOT="$HUNTERPIE_CC_WINE_DOTNET_DIR"
# Wine's certificate store cannot validate NuGet's repository signature
# timestamping certificates (NU3028/NU3037); signature verification is a
# package-tampering check, irrelevant for local compile validation.
export DOTNET_NUGET_SIGNATURE_VERIFICATION=false

# Build a safely-quoted command line for the /bin/sh that protontricks spawns.
quote_arg() {
    # wrap in single quotes, escaping embedded single quotes
    printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"
}

CMD='"$WINE"'
for arg in "$@"; do
    CMD="$CMD $(quote_arg "$arg")"
done

# --no-bwrap: the Steam Runtime sandbox does not bind-mount /var/home
# (where both this repo and the toolchain live on this system), which
# would make Wine's Z: paths dangle. The build does not need the sandbox.
exec protontricks --no-term --no-bwrap -c "$CMD" "$HUNTERPIE_CC_APPID"
