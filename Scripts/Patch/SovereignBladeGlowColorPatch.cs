using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace BetterSovereignBlade.Scripts.Patch;

internal static class SovereignBladeGlowColorState
{
    private static readonly FieldInfo BladeGlowField = AccessTools.Field(typeof(NSovereignBladeVfx), "_bladeGlow");

    internal static Color? CurrentColor;

    internal static void SetColor(Color color)
    {
        CurrentColor = color;
        ApplyToActiveSwords();
    }

    internal static void ApplyToActiveSwords()
    {
        if (CurrentColor == null)
        {
            return;
        }

        var room = NCombatRoom.Instance;
        if (room?.CreatureNodes == null)
        {
            return;
        }

        foreach (var creature in room.CreatureNodes)
        {
            if (creature == null)
            {
                continue;
            }

            foreach (var sword in creature.GetChildren().OfType<NSovereignBladeVfx>())
            {
                TryApply(sword);
            }
        }
    }

    internal static bool TryApply(NSovereignBladeVfx instance)
    {
        if (CurrentColor == null)
        {
            return false;
        }

        if (BladeGlowField.GetValue(instance) is Node2D bladeGlow
            && GodotObject.IsInstanceValid(bladeGlow))
        {
            bladeGlow.Modulate = CurrentColor.Value;
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(NSovereignBladeVfx), "_Ready")]
internal class SovereignBladeGlowColorPatch_Ready
{
    
    private static void Postfix(NSovereignBladeVfx __instance)
    { 
        Node2D _blade=__instance.GetNode<Node2D>("SpineSword/SwordBone/ScaleContainer/Blade"); 
        Node2D _stepped=__instance.GetNode<Node2D>("SpineSword/SwordBone/ScaleContainer/SteppedFireMix");
        Node2D _blade2=__instance.GetNode<Node2D>("SpineSword/SwordBone/ScaleContainer/Blade2");
        TextureRect _bladeOutline2=__instance.GetNode<TextureRect>("SpineSword/SwordBone/ScaleContainer/BladeOutline2");
        SovereignBladeGlowColorState.TryApply(__instance);
    }
}

