using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using BetterSovereignBlade.Scripts.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BetterSovereignBlade.Scripts.Patch;

internal static class SovereignBladeTargetingDebugState
{
    internal static CardModel? ActiveCard;
    internal static bool TargetingActive;

    internal static bool IsSovereignBladeActive => ActiveCard is SovereignBlade;

    internal static string DescribeCard(CardModel card)
    {
        return $"{card.GetType().Name}#{card.GetHashCode():x}";
    }

    internal static string DescribeCreature(NCreature creature)
    {
        string name = creature.Entity?.GetType().Name ?? creature.Name;
        return $"{name} at {creature.VfxSpawnPosition}";
    }

    internal static void SetActiveCard(CardModel? card, string source)
    {
        if (card is SovereignBlade)
        {
            ActiveCard = card;
            TargetingActive = false;
            ModConsole.Print($"[SB] CardPlay start ({source}): {DescribeCard(card)}");
        }
        else
        {
            ActiveCard = null;
            TargetingActive = false;
        }
    }
}

internal static class SovereignBladeTargetingDebugReflection
{
    internal static CardModel? TryGetCardModel(object? instance)
    {
        if (instance == null)
            return null;

        object? holder = AccessTools.Property(instance.GetType(), "Holder")?.GetValue(instance);
        if (holder == null)
            return null;

        return AccessTools.Property(holder.GetType(), "CardModel")?.GetValue(holder) as CardModel;
    }
}

[HarmonyPatch(typeof(NCardHolder), "OnMousePressed")]
internal class SovereignBladeTargetingDebug_OnMousePressed
{
    private static void Postfix(NCardHolder __instance, InputEvent inputEvent)
    {
        ModConsole.Print("[SB] Mouse pressed on card holder");
        if (inputEvent is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Left || !mouse.Pressed)
            return;
    
        CardModel? card = __instance.CardModel;
        ModConsole.Print("[SB] Card selected: " + card?.GetType().Name);
        if (card is SovereignBlade)
        {
            SovereignBladeTargetingDebugState.ActiveCard = card;
            SovereignBladeTargetingDebugState.TargetingActive = false;
            ModConsole.Print($"[SB] Hold start: {SovereignBladeTargetingDebugState.DescribeCard(card)}");
        }
        else
        {
            SovereignBladeTargetingDebugState.ActiveCard = null;
            SovereignBladeTargetingDebugState.TargetingActive = false;
        }
    }
}

[HarmonyPatch(typeof(NCardHolder), "OnMouseReleased")]
internal class SovereignBladeTargetingDebug_OnMouseReleased
{
    private static void Postfix(NCardHolder __instance, InputEvent inputEvent)
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        if (inputEvent is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Left)
            return;

        if (!SovereignBladeTargetingDebugState.TargetingActive)
        {
            ModConsole.Print("[SB] Hold end (no targeting)");
            SovereignBladeTargetingDebugState.ActiveCard = null;
        }
    }
}

[HarmonyPatch(typeof(NTargetManager), "StartTargeting", new Type[] { typeof(TargetType), typeof(Vector2), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>) })]
internal class SovereignBladeTargetingDebug_StartTargeting_Position
{
    private static void Postfix()
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        SovereignBladeTargetingDebugState.TargetingActive = true;
        ModConsole.Print("[SB] Targeting began (position)");
    }
}

[HarmonyPatch(typeof(NTargetManager), "StartTargeting", new Type[] { typeof(TargetType), typeof(Control), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>) })]
internal class SovereignBladeTargetingDebug_StartTargeting_Control
{
    private static void Postfix()
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        SovereignBladeTargetingDebugState.TargetingActive = true;
        ModConsole.Print("[SB] Targeting began (control)");
    }
}

[HarmonyPatch(typeof(NTargetManager), "OnCreatureHovered")]
internal class SovereignBladeTargetingDebug_OnCreatureHovered
{
    private static void Postfix(NCreature creature)
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        ModConsole.Print($"[SB] Target hovered: {SovereignBladeTargetingDebugState.DescribeCreature(creature)}");
    }
}

[HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
internal class SovereignBladeTargetingDebug_FinishTargeting
{
    private static void Prefix(bool cancel)
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        string result = cancel ? "canceled" : "confirmed";
        ModConsole.Print($"[SB] Targeting ended: {result}");
        SovereignBladeTargetingDebugState.ActiveCard = null;
        SovereignBladeTargetingDebugState.TargetingActive = false;
    }
}

[HarmonyPatch]
internal class SovereignBladeTargetingDebug_MouseCardPlay_Start
{
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method("MegaCrit.Sts2.Core.Nodes.Combat.NMouseCardPlay:Start");
    }

    private static void Postfix(object __instance)
    {
        CardModel? card = SovereignBladeTargetingDebugReflection.TryGetCardModel(__instance);
        SovereignBladeTargetingDebugState.SetActiveCard(card, "mouse");
    }
}

[HarmonyPatch]
internal class SovereignBladeTargetingDebug_ControllerCardPlay_Start
{
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method("MegaCrit.Sts2.Core.Nodes.Combat.NControllerCardPlay:Start");
    }

    private static void Postfix(object __instance)
    {
        CardModel? card = SovereignBladeTargetingDebugReflection.TryGetCardModel(__instance);
        SovereignBladeTargetingDebugState.SetActiveCard(card, "controller");
    }
}
