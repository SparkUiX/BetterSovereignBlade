using System;
using System.Globalization;
using System.Linq;
using Godot;
using BetterSovereignBlade.Scripts.Patch;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BetterSovereignBlade.Scripts.Debug;

public sealed class BladeConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "bladeglow";
    public override string Args => "Color(r,g,b,a) | r g b [a]";
    public override string Description => "Set Sovereign Blade glow color (modulate).";
    public override bool IsNetworked => false;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!TryParseColor(args, out Color color, out string error))
        {
            return new CmdResult(success: false, error);
        }

        SovereignBladeGlowColorState.SetColor(color);
        return new CmdResult(success: true,
            $"[SB] BladeGlow modulate set to Color({color.R:0.###}, {color.G:0.###}, {color.B:0.###}, {color.A:0.###})");
    }

    private static bool TryParseColor(string[] args, out Color color, out string error)
    {
        color = default;
        error = "Usage: bladeglow Color(r,g,b,a) | r g b [a]";

        if (args.Length == 0)
        {
            return false;
        }

        if (args.Length == 1)
        {
            string token = args[0].Trim();
            if (token.StartsWith("Color(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(")"))
            {
                string inner = token.Substring(6, token.Length - 7);
                string[] parts = inner.Split(',');
                return TryParseComponents(parts, out color, out error);
            }
        }

        return TryParseComponents(args, out color, out error);
    }

    private static bool TryParseComponents(string[] parts, out Color color, out string error)
    {
        color = default;
        error = "Usage: bladeglow Color(r,g,b,a) | r g b [a]";

        string[] cleaned = parts
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        if (cleaned.Length is < 3 or > 4)
        {
            return false;
        }

        if (!TryParseFloat(cleaned[0], out float r)
            || !TryParseFloat(cleaned[1], out float g)
            || !TryParseFloat(cleaned[2], out float b))
        {
            error = "Invalid color components. Expected floats like 1, 0.45, 0, 1.";
            return false;
        }

        float a = 1f;
        if (cleaned.Length == 4 && !TryParseFloat(cleaned[3], out a))
        {
            error = "Invalid alpha component. Expected float like 1.";
            return false;
        }

        color = new Color(
            Mathf.Clamp(r, 0f, 1f),
            Mathf.Clamp(g, 0f, 1f),
            Mathf.Clamp(b, 0f, 1f),
            Mathf.Clamp(a, 0f, 1f));

        return true;
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
