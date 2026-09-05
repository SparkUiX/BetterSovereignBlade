# Better Sovereign Blade

[简体中文](README.zh-CN.md) | English

Better Sovereign Blade enhances the interaction, visual feedback, and customization of the Regent's Sovereign Blade, transforming it from a passive effect into a responsive and player-controlled combat element.

This mod focuses on three core improvements:

- Direct manipulation
- Clear targeting feedback
- Full visual customization

## Overview

The original Sovereign Blade has a strong concept but limited interactivity.

This mod expands its design by turning the blade into a controllable entity. Instead of acting as a static VFX, the blade now responds dynamically to player input and clearly communicates targeting intent.

In addition, the mod introduces a flexible color customization system, allowing you to personalize the blade's appearance and effects.

## Features

### Drag the Blade

You can directly drag the Sovereign Blade across the battlefield.

- Freely reposition the blade with your mouse
- Creates a sense of physical control
- Makes the blade feel like an actual weapon rather than a visual effect

### Floating Target Indicator

When picking up the Sovereign Blade card:

- The blade floats above enemies
- Automatically aligns toward the current target

This provides immediate and intuitive targeting feedback during combat.

### Drag-to-Target Play

When the card is playable:

- Drag the blade itself instead of the card
- Move it onto a specific enemy
- Release to play the card

This creates a more immersive and direct targeting experience compared to the default system.

### Custom Blade Colors

You can fully customize the visual appearance of the blade:

- Choose from preset color themes
- Manually adjust colors for individual components
- Customize the blade model, slash effects, and energy effects

This allows you to tailor the blade's visual style to your preference, from subtle variations to completely unique looks.

## Why This Mod?

This mod is designed to make Sovereign Blade feel:

- More interactive
- More responsive
- More expressive

By combining direct control with clear feedback and visual customization, the blade becomes an active part of gameplay rather than a passive effect.

## Compatibility

- Designed specifically for the Regent's Sovereign Blade
- Does not modify other cards
- Compatible with most gameplay mods

Game updates or another mod patching the same UI or visual-effect methods may still cause compatibility issues.

## Notes

- Only affects interaction and visual presentation
- Does not change balance or card mechanics
- Current manifest version: **0.1.0**

## Installation

1. Download the latest release from the [Nexus Mods page](https://www.nexusmods.com/slaythespire2/mods/48?tab=description).
2. Create `<Slay the Spire 2>/mods/BetterSovereignBlade/` if it does not exist.
3. Place `BetterSovereignBlade.dll` and `BetterSovereignBlade.json` in that folder.
4. Start the game and verify that the mod is enabled under **Settings → Mod Settings**.

The local `mods` directory sits inside the game installation directory on Windows and Linux. To temporarily start without any mods, use the game's `-nomods` launch option.

## Configuration

Open the game's general settings. Better Sovereign Blade adds controls for:

- overall color presets;
- dynamic RGB and animation speed;
- rotation and trail effects while dragging;
- individual component colors and per-channel RGB toggles.

Use the **Default (no changes)** preset to preserve the game's original colors.

## Building from Source

Requirements:

- .NET SDK 9.0 or newer;
- a local Slay the Spire 2 installation containing `data_sts2_windows_x86_64/sts2.dll` and `0Harmony.dll`.

Set the game directory, then build:

```powershell
$env:STS2_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2'
dotnet build .\BetterSovereignBlade.sln -c Release
```

You can also pass the path for a single build:

```powershell
dotnet build .\BetterSovereignBlade.sln -c Release -p:Sts2Dir='D:\SteamLibrary\steamapps\common\Slay the Spire 2'
```

After a successful build, the project copies the DLL and manifest to `<Sts2Dir>/mods/BetterSovereignBlade/`.

## Links

- [Nexus Mods](https://www.nexusmods.com/slaythespire2/mods/48?tab=description)
- [Source code and issue tracker](https://github.com/SparkUiX/BetterSovereignBlade)

## License

Source code is available under the [MIT License](LICENSE).

Slay the Spire 2 and its related names and assets belong to their respective owners. This is an unofficial community project and is not affiliated with or endorsed by Mega Crit.
