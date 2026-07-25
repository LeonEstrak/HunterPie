# Linux Compiler (Protontricks-based build framework)

HunterPie is a Windows-only WPF application (`net10.0-windows`), so it cannot
be compiled natively on Linux. This framework validates compilation (and runs
unit tests) by executing the **Windows .NET SDK inside a Proton prefix** via
`protontricks` — the same Wine environment used to run the games.

## Architecture

```
compile-check.sh
  ├─> restore-packages.sh          (native Linux .NET SDK)
  │     └─> dotnet restore  ──seeds──>  prefix NuGet cache
  │                                     C:\users\steamuser\.nuget\packages
  └─> run-in-prefix.sh  (protontricks → Proton prefix, default MHW 582010)
        └─> wine dotnet.exe build HunterPie/HunterPie.csproj
        └─> wine dotnet.exe test  HunterPie.Core.Tests   (with --tests)
```

Two SDKs are involved:

1. **Windows x64 .NET SDK** (`setup-toolchain.sh`) — runs the actual build
   inside Wine, giving true Windows-targeting semantics (WPF targets, Win32
   references). Downloaded once to
   `~/.local/share/hunterpie-compile-check/dotnet`.
2. **Native Linux .NET SDK** (`setup-linux-sdk.sh`) — used *only* to seed the
   prefix's NuGet package cache (`restore-packages.sh`). Required because
   **Wine's certificate store cannot validate NuGet package signatures**
   (NU3028/NU3037), while Linux disables signature verification by default.
   Packages already installed in the global packages folder are accepted by
   the Wine-side restore without verification.

## Flatpak note (this machine)

`/usr/bin/protontricks` here is a wrapper for the Flatpak
`com.github.Matoking.protontricks`, whose sandbox shadows `~/.local/share`.
`setup-toolchain.sh` therefore applies a one-time user-level override so the
sandbox can see the toolchain:

```bash
flatpak override --user com.github.Matoking.protontricks \
    --filesystem=$HOME/.local/share/hunterpie-compile-check
```

`--no-bwrap`/`--no-runtime` are not needed; the override is sufficient.

## Usage

```bash
# One-time setup
./Scripts/linux-compiler/setup-toolchain.sh
./Scripts/linux-compiler/setup-linux-sdk.sh   # auto-run on demand

# Compile validation
./Scripts/linux-compiler/compile-check.sh

# Compile + unit tests
./Scripts/linux-compiler/compile-check.sh --tests

# Release configuration
./Scripts/linux-compiler/compile-check.sh --configuration Release
```

Build logs: `~/.local/share/hunterpie-compile-check/logs/`.

## Configuration (environment variables)

| Variable | Default | Meaning |
|---|---|---|
| `HUNTERPIE_CC_APPID` | `582010` | Steam AppID of an installed Proton game whose prefix hosts the build |
| `HUNTERPIE_CC_TOOLCHAIN_DIR` | `~/.local/share/hunterpie-compile-check` | Toolchain + logs |
| `HUNTERPIE_CC_SDK_CHANNEL` | `10.0` | .NET SDK channel |
| `HUNTERPIE_CC_CONFIGURATION` | `Debug` | MSBuild configuration |
| `HUNTERPIE_CC_PROJECT` | `HunterPie/HunterPie.csproj` | Entry project (mirrors CI) |

## Caveats

- The build mirrors `.github/workflows/pull-request.yaml` (builds the
  `HunterPie` project; `HunterPie.Native`/vcxproj is excluded, as in CI).
- MSBuild runs with `-nr:false -m:1 -p:UseSharedCompilation=false` for Wine
  stability; builds are single-process and slower than native Windows.
- **This validates compilation and unit tests only.** Runtime testing of the
  app/overlay still requires Windows or running HunterPie under Proton.
