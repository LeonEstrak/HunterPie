#!/usr/bin/env bash
# Shared configuration for the HunterPie linux-compiler framework.
# Sourced by the other scripts in this directory.

# Steam AppID whose Proton prefix hosts the build environment.
# 367520 = Hollow Knight. Any installed Proton game works; the prefix is
# only used as a Wine runtime container. Note: pick a game whose compat
# tool protontricks supports (NOT SteamTinkerLaunch-managed games).
export HUNTERPIE_CC_APPID="${HUNTERPIE_CC_APPID:-367520}"

# Where the Windows .NET SDK toolchain lives (Linux-side path).
export HUNTERPIE_CC_TOOLCHAIN_DIR="${HUNTERPIE_CC_TOOLCHAIN_DIR:-$HOME/.local/share/hunterpie-compile-check}"
export HUNTERPIE_CC_DOTNET_DIR="$HUNTERPIE_CC_TOOLCHAIN_DIR/dotnet"

# SDK channel to download (https://aka.ms/dotnet/<channel>/dotnet-sdk-win-x64.zip)
export HUNTERPIE_CC_SDK_CHANNEL="${HUNTERPIE_CC_SDK_CHANNEL:-10.0}"

# Repository root (absolute Linux path).
export HUNTERPIE_CC_REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Build configuration matching the project's GitHub Actions workflow.
export HUNTERPIE_CC_CONFIGURATION="${HUNTERPIE_CC_CONFIGURATION:-Debug}"

# Project to build. Mirrors .github/workflows/pull-request.yaml which builds
# the HunterPie project only (transitively covers Core/UI/Integrations/
# Platforms/DI) and avoids HunterPie.Native (vcxproj, not buildable by
# the .NET SDK) and HunterPie.Playground.
export HUNTERPIE_CC_PROJECT="${HUNTERPIE_CC_PROJECT:-HunterPie/HunterPie.csproj}"

# Converts an absolute Linux path to a Wine Z: drive path using forward
# slashes (Wine accepts them and they survive shell quoting, unlike
# backslashes). e.g. /var/home/user/repo -> Z:/var/home/user/repo
to_wine_path() {
    printf 'Z:%s' "$1"
}

export HUNTERPIE_CC_WINE_REPO_ROOT="$(to_wine_path "$HUNTERPIE_CC_REPO_ROOT")"
export HUNTERPIE_CC_WINE_DOTNET="$(to_wine_path "$HUNTERPIE_CC_DOTNET_DIR")/dotnet.exe"
export HUNTERPIE_CC_WINE_DOTNET_DIR="$(to_wine_path "$HUNTERPIE_CC_DOTNET_DIR")"
