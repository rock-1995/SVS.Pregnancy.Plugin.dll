# SVS Pregnancy Plugin

A BepInEx IL2CPP plugin that adds a lightweight pregnancy system to
Summer Vacation Scramble.

The project focuses on keeping the feature set self-contained for SVS: it
does not require external character-body plugins, and it includes its own
runtime belly morphing and clothing deformation support.

## Current build: 0.6.5 AL r3

[Download the DLL and source package](https://github.com/rock-1995/SVS.Pregnancy.Plugin.dll/releases/tag/0.6.5-al-r3).

This build ports the AL Pregnancy 0.2.26 belly geometry, vertex binding and
deformation controls to SVS. It includes SVS rig calibration fixes and smooths
the upper belly attachment field to reduce folded faces during posing.
Pregnancy gameplay and save handling retain the SVS implementation.

The F8 deformation panel includes the AL shape controls, growth-stage preview,
upper/lower attachment controls, navel controls, clothing displacement and
rebuild/reset buttons. Existing configuration files can be retained.

This is a **pre-release**: offline checks pass, but live rendering and frame
rate have not been verified. Extreme bends can still fold. See
[CHANGELOG.md](CHANGELOG.md) for the changes and validation limits.

## Features

- Pregnancy can occur during player-controlled H scenes.
- Pregnancy chance is affected by configurable ovulation rates for safe,
  normal, and dangerous days.
- Fertility calculations also consider character state, relationship context,
  chastity, resistance, weakness/lewdness state, and whether climax timing is
  synchronized.
- Optional futanari insemination support. Only biologically female characters
  can become pregnant.
- Pregnancy progression speed is configurable. The default is a 40-week
  pregnancy, with faster progression options available.
- Birth timing varies around term. Late pregnancy can result in healthy birth;
  earlier outcomes may produce premature birth or miscarriage.
- Pregnancy, birth, and miscarriage can affect character emotions and
  relationship values.
- Pregnancy data is saved beside the game save file and restored on load.
- Runtime belly morphing for pregnant characters.
- Clothing deformation support, including mesh-readable loading, surface-based
  clothing morphing, layered-clothing preservation, and reduced belly clipping.
- In-game debug UI, default key `F8`, for inspecting pregnancy state and tuning
  belly deformation parameters.
- Diagnostic mesh spy tools are available through BepInEx Configuration
  Manager and are disabled by default.

## Install

Copy the built plugin to:

```text
SamabakeScramble\BepInEx\plugins\SVS_plugins\SVS_Pregnancy.dll
```

The plugin requires a working BepInEx IL2CPP setup for Summer Vacation
Scramble.

## Configuration

After the first launch, settings are written to:

```text
BepInEx\config\SVS.SVSPregnancy.cfg
```

Most gameplay settings can also be edited through BepInEx Configuration
Manager:

- `General > Enable`
- `General > Log Enable`
- `General > Futanaris Can Inseminate`
- `General > Pregnancy progression speed`
- `General > Ovulation Rate in Safe Days`
- `General > Ovulation Rate in Normal Days`
- `General > Ovulation Rate in Dangerous Days`
- `Debug > Debug UI Key`
- `Debug > Enable Spy`

`Enable Spy` is intended for diagnostics only. The clothing mesh loader remains
active even when spy logging is disabled.

## Build

The plugin targets .NET 6. Build references are resolved from a local SVS
installation with BepInEx IL2CPP; proprietary game assemblies are not included.

```powershell
.\Build.ps1 -SVSGameDir "D:\Games\SamabakeScramble"
```

Alternatively:

```powershell
dotnet build .\src\SVS_Pregnancy.csproj -c Release -p:SVSGameDir="D:\Games\SamabakeScramble"
```

Output: `bin/Release/net6.0/SVS_Pregnancy.dll`.

## Tests

The offline regression runners require the .NET 10 SDK and do not launch the game:

```powershell
.\Test.ps1
```

Optional full-body replay requires a fixture extracted from your own SVS assets
with Python and UnityPy (the extraction was verified with UnityPy 1.25.4):

```powershell
python .\tests\ExtractSvsBodyFixture.py "D:\Games\SamabakeScramble\abdata\chara\body\body_00.unity3d" .\work\svs-body.json
.\Test.ps1 -SvsBodyFixture .\work\svs-body.json
```

Create the local `work` directory before extracting the fixture. Game meshes,
local settings, and generated build/test files should not be committed.

## Development Notes

- Binary downloads are attached to Releases.
- The UI preview changes the selected character's appearance without saving a
  pregnancy state. Normal gameplay does not require the debug UI.
- The upper attachment correction runs when shape parameters or the model change;
  pose updates continue to update matrix palettes and bounds.

## Credits

Thanks to the authors of Pregnancy Plus for the original work and ideas this
project builds on.

Thanks also to the author of "monkey version" pregnancy plugin, which provided
the starting point for it.https://zodgame.xyz/forum.php?mod=viewthread&tid=471375&extra=

The belly deformation port uses the AL Pregnancy 0.2.26 source design, with SVS-specific rig and upper-attachment adaptations.
