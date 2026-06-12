# AFK4.NET BAFunctions

A tiny native **BAFunctions** DLL that plugs into the stock WiX Standard
Bootstrapper Application (WixStdBA) used by the client bundle
(`installers/bundle/Bundle.wxs`) and removes the two clicks a user would
otherwise make:

- **Auto-start install** — on `OnDetectComplete` it simulates a click on the
  Install button, so the install begins on its own (UAC is still shown by
  Windows; that elevation prompt cannot be removed for a per-machine install).
- **Auto-close** — on a successful `OnApplyComplete` with no pending reboot it
  closes the window, so the "Installation Successfully Completed / Close" page
  never needs a click.

Only the interactive (full-UI) case is touched; passive/quiet runs already
behave this way in WixStdBA and are skipped by guards.

## Why native + static CRT

The bundle installs the .NET runtime, so the BA itself runs **before** any .NET
exists on the machine — it must be native. It also links the CRT statically
(`/MT`) because a freshly-imaged machine has no VC++ redistributable.

## Vendored files (`wixsdk/`)

The BAFunctions SDK headers and message dispatcher are **not** shipped in a
NuGet package, so they are vendored verbatim from WiX **v7.0.0**
(`src/ext/Bal/wixstdfn/`), matching the pinned `wix` tool version:

- `BAFunctions.h`, `IBAFunctions.h`
- `BalBaseBAFunctions.h`
- `BalBaseBAFunctionsProc.h`, `BalBaseBAFunctionsProc.cpp`

Everything else (BalUtil/DUtil headers + `balutil.lib`/`dutil.lib`) comes from
the `WixToolset.BootstrapperApplicationApi` and `WixToolset.DUtil` NuGet
packages (pinned to 7.0.0 in the `.vcxproj`). If the `wix` tool version is
bumped, re-vendor these files from the matching tag.

## Build

Built as part of `scripts/build-client-packages.ps1` via MSBuild
(`-t:Restore;Build -p:Configuration=Release -p:Platform=x64`); the resulting
`AFK4.BAFunctions.dll` is passed to the bundle as a payload.
