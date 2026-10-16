# SVS Pregnancy Plugin

A BepInEx IL2CPP plugin for **Summer Vacation Scramble** that adds pregnancy progression, a growing belly with clothing deformation, and body changes based on pregnancy progress and birth count.

[Download the latest release](https://github.com/rock-1995/SVS.Pregnancy.Plugin.dll/releases/latest) · [Version history](CHANGELOG.md)

## Features

- **Pregnancy gameplay:** conception during player-controlled H scenes, configurable ovulation rates, and fertility influenced by character state and relationship context. Optional futanari insemination is supported; only biologically female characters can become pregnant.
- **Pregnancy and birth:** a configurable progression speed, birth timing around term, premature birth and miscarriage outcomes, postpartum cooldown, and effects on character emotions and relationships.
- **Belly deformation:** progressive growth with adjustable fullness, width, downward sag, navel response, and pose response. Attachment and protection controls help preserve the chest, thighs, and lower-body shape.
- **Clothing deformation:** clothes follow the changing body, including layered clothing and skirt support, with adjustable displacement to help manage clipping.
- **Template body changes:** the game's body sliders and breast controls change gradually using two reference cards. Birth count preserves accumulated body changes after pregnancy; nipple color has a separate progress limit.
- **Saved state and in-game controls:** pregnancy data and birth counts are stored alongside game saves. The F8 panel provides character inspection, belly preview, shape tuning, and birth-count correction.

The plugin includes its own body and clothing deformation system. No separate character-body deformation plugin is required.

## Installation

Requires a working **BepInEx IL2CPP** installation for SVS. BepInEx Configuration Manager is optional and provides the F1 settings window when installed with its usual key binding.

1. Close the game and download `SVS_Pregnancy_0.6.10.zip` from the release page.
2. Extract its `BepInEx` folder into the `SamabakeScramble` game folder.
3. Check that the DLL and template folder are arranged as follows:

```text
SamabakeScramble/
  BepInEx/
    plugins/
      SVS_Pregnancy.dll
      SVSPregnancyTemplates/
        o.png
        p.png
```

If an older DLL is in `plugins/SVS_plugins` or another subfolder, replace it there and put `SVSPregnancyTemplates` beside that DLL, or move the installation to the layout above. Keep only one copy of the plugin DLL. Back up custom O/P cards before replacing the bundled templates.

Existing configuration files remain in use. To load the bundled belly defaults, use **F8 → Default shape → Save to File**.

## Playing and configuring

Pregnancy runs during normal gameplay; opening F8 is optional. The default pregnancy progression speed is **1**, corresponding to roughly 40 weeks. A speed of **2** gives roughly 20 weeks, **4** roughly 10 weeks, and so on.

Use Configuration Manager or edit `BepInEx/config/SVS.SVSPregnancy.cfg` while the game is closed:

| Section | Setting | Purpose |
| --- | --- | --- |
| General | Enable | Enable or disable the plugin. |
| General | Pregnancy progression speed | Control how quickly pregnancy and postpartum cooldown advance. |
| General | Ovulation Rate in Safe / Normal / Dangerous Days | Set the ovulation rates used in conception calculations. |
| General | Futanaris Can Inseminate | Allow futanari characters to impregnate female partners. |
| Pregnancy body changes | Births for full change | Set how many births are needed for the full template body change; default **5**. |
| Pregnancy body changes | Minimum / Maximum slider value | Set limits for affected body sliders; defaults **0–100**. |
| Pregnancy body changes | Nipple color progress maximum | Cap color progression separately; default **0.85**, and **0** disables the color change. |
| Debug | Debug UI Key | Change the F8 panel shortcut. |

### Belly shape and F8

Open **F8**, select a character, and use **Enable Preview** to inspect belly growth. **Growth stage** controls the preview; **Full-term shape** loads the bundled shape and previews full growth. Previewing the belly does not change pregnancy or birth count, and temporarily suspends template body changes for the previewed character. Turn preview off to return to normal progression.

Adjust fullness, width, sag, clothing displacement, navel response, and attachment/protection controls as needed. Numeric fields also accept values beyond the slider's displayed range. Use **Rebuild shape** to refresh the current deformation, **Default shape** to restore the bundled shape settings, and **Save to File** to keep your adjustments.

Belly settings are stored in `BepInEx/config/SVSPregnancy_belly.json`. **Pregnancy belly start day** controls when the growing belly begins to appear; its default is day **40**.

The character panel also shows pregnancy progress and birth count. **Set births → Apply births** edits the character's actual count, which is saved with the game. This is separate from the appearance-only belly preview.

## O/P body templates

`o.png` is the baseline and `p.png` is the reference for the full body change. Both must be valid female SVS character cards, placed in `SVSPregnancyTemplates` beside the DLL. The release includes a working pair. To make your own, save a baseline card as O, edit the desired body controls, and save the result as P.

For each affected body slider, the plugin adds the **P − O** difference to the character's own original value. It uses all 40 native body sliders and the breast softness, gravity, areola size, and secondary softness controls. Face, hairstyle, clothing, texture IDs, and other card settings are not copied to the character.

```text
change progress = min(1, (birth count + current pregnancy progress) / births for full change)
body value      = original character value + (P value − O value) × change progress
```

Current pregnancy progress runs from 0 to 1 and is zero when not pregnant. With the default threshold of 5, two births plus a half-completed pregnancy give **50%** of the body change. A threshold of 0 or less applies the full change immediately. Successful and premature births increase the count; miscarriages do not. Older saves without a birth count start at zero and can be adjusted in F8.

If the O/P nipple colors differ, color blends from the character's original color toward P, subject to **Nipple color progress maximum**. A black P darkens the original color without changing its hue. Body-slider limits do not control color. Matching O/P nipple colors leave color unchanged.

The growing pregnancy belly follows the current pregnancy. Birth-based template body changes can remain after birth. These are runtime appearance changes; the source character card is not rewritten. If the templates cannot be loaded, template body changes are skipped while belly deformation remains available.

## Saves and troubleshooting

Pregnancy state is saved as `<save-name>_pregnancy.json` beside the corresponding game save in `UserData/save/game`. Keep these files with the saves when backing up or moving a playthrough.

- **No template body change:** check the O/P folder location, that both cards are valid female SVS cards, the birth/progress values, and that manual belly preview is off.
- **A shape needs adjustment:** use F8 preview and the clothing/attachment controls. Results depend on the character, outfit, and pose.
- **Reporting a problem:** include the plugin version, steps to reproduce, and the relevant section of `BepInEx/LogOutput.log`. `General > Log Enable` and `Debug > Enable Spy` provide extra diagnostics; mesh spy is normally off.

## Building and testing

The plugin targets .NET 6. Build references come from your local game installation; proprietary game assemblies are not included.

```powershell
.\Build.ps1 -SVSGameDir "D:\Games\SamabakeScramble"
```

The DLL is written to `bin/Release/net6.0/SVS_Pregnancy.dll`, with the template folder alongside it.

The offline regression runners require the **.NET 10 SDK**:

```powershell
.\Test.ps1 -SVSGameDir "D:\Games\SamabakeScramble"
```

They cover deformation geometry, clothing, runtime restoration, body changes, persistence, and native hook selection. These checks do not launch the game. Generated build files and game assemblies should not be committed.

## Credits

Thanks to the authors of **Pregnancy Plus** for the original work and ideas this project builds on, and to the author of the ["monkey version" pregnancy plugin](https://zodgame.xyz/forum.php?mod=viewthread&tid=471375&extra=) for the starting point.

The belly and template body-change design draws on AL Pregnancy, with integration adapted for SVS.
