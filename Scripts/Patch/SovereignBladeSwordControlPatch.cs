using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using BetterSovereignBlade.Scripts.Debug;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BetterSovereignBlade.Scripts.Patch;

internal static class SovereignBladeSwordControlState
{
    internal static NSovereignBladeVfx? ActiveSword;
    internal static Node2D? SpineNode;
    internal static Vector2 DesiredPosition;
    internal static float DesiredRotation;
    internal static bool IsActive;

    internal static float RotationSpeedRad = 12.0f;
    internal static float HoldRotationSpeedRad = 16.0f;
    internal static float MoveSpeed = 3000.0f;
    internal static float HoldMoveSpeed = 8000.0f;
    internal static float DownAngleRad = Mathf.Pi / 2.0f;
    internal static float RotationOffsetRad = 0.0f;
    internal static Vector2 TargetOffset = new(0f, -120f);
    internal static Vector2 ScreenAnchor = new(0.8f, 0.2f);

    internal static float LogIntervalSeconds = 0.25f;
    internal static float LogCooldown;

    internal static Vector2 FixedHoldPosition = new(1470f, 320f);

    internal static float HoldReleaseDelaySeconds = 0.5f;
    internal static bool ReleasePending;
    internal static float ReleaseCooldown;

    internal static float HoveredHoldSeconds = 0.08f;
    internal static float HoveredTimeout;
    internal static NCreature? HoveredCreature;

    internal static void Reset()
    {
        ActiveSword = null;
        SpineNode = null;
        IsActive = false;
        LogCooldown = 0f;
        ReleasePending = false;
        ReleaseCooldown = 0f;
        HoveredTimeout = 0f;
        HoveredCreature = null;
    }
}

internal static class SovereignBladeSwordControl
{
    internal static bool TrySetActiveSword()
    {
        NCreature? playerNode = NCombatRoom.Instance?.CreatureNodes
            ?.FirstOrDefault(c => c.Entity?.IsPlayer ?? false);
        if (playerNode == null)
            return false;

        NSovereignBladeVfx? sword = playerNode.GetChildren()
            .OfType<NSovereignBladeVfx>()
            .FirstOrDefault();
        if (sword == null)
            return false;

        Node2D? spine = AccessTools.Field(typeof(NSovereignBladeVfx), "_spineNode")
            .GetValue(sword) as Node2D;
        if (spine == null)
            return false;

        SovereignBladeSwordControlState.ActiveSword = sword;
        SovereignBladeSwordControlState.SpineNode = spine;
        SovereignBladeSwordControlState.IsActive = true;
        return true;
    }

    internal static void SetDesiredToFirstEnemy()
    {
        NCreature? enemy = NCombatRoom.Instance?.CreatureNodes
            ?.FirstOrDefault(c => c.Entity?.IsEnemy ?? false);
        if (enemy == null || SovereignBladeSwordControlState.SpineNode == null)
            return;

        SovereignBladeSwordControlState.DesiredPosition = enemy.VfxSpawnPosition + SovereignBladeSwordControlState.TargetOffset;
        SovereignBladeSwordControlState.DesiredRotation = SovereignBladeSwordControlState.DownAngleRad + SovereignBladeSwordControlState.RotationOffsetRad;
    }

    internal static void SetDesiredToCreature(NCreature creature)
    {
        if (SovereignBladeSwordControlState.SpineNode == null)
            return;

        Vector2 dir = (creature.VfxSpawnPosition - SovereignBladeSwordControlState.SpineNode.GlobalPosition);
        if (dir.LengthSquared() > 0.0001f)
        {
            SovereignBladeSwordControlState.DesiredRotation = dir.Angle() + SovereignBladeSwordControlState.RotationOffsetRad;
        }
    }

    internal static bool SetDesiredToScreenAnchor()
    {
        if (SovereignBladeSwordControlState.SpineNode == null)
            return false;

        SovereignBladeSwordControlState.DesiredPosition = SovereignBladeSwordControlState.FixedHoldPosition;
        SovereignBladeSwordControlState.DesiredRotation = SovereignBladeSwordControlState.DownAngleRad + SovereignBladeSwordControlState.RotationOffsetRad;
        return true;
    }

    internal static void StartHoverAim(NCreature creature)
    {
        SovereignBladeSwordControlState.HoveredCreature = creature;
    }

    internal static void UpdateAimRotation(NCreature creature, Vector2 fromPosition)
    {
        Vector2 dir = creature.VfxSpawnPosition - fromPosition;
        if (dir.LengthSquared() <= 0.0001f)
            return;

        SovereignBladeSwordControlState.DesiredRotation = dir.Angle() + SovereignBladeSwordControlState.RotationOffsetRad;
    }
}

internal static class SovereignBladeMath
{
    private static float Wrap(float value, float min, float max)
    {
        float range = max - min;
        if (range == 0f)
            return min;
        float wrapped = (value - min) % range;
        if (wrapped < 0f)
            wrapped += range;
        return wrapped + min;
    }

    internal static float MoveTowardAngle(float current, float target, float maxDelta)
    {
        float delta = Wrap(target - current, -Mathf.Pi, Mathf.Pi);
        if (Mathf.Abs(delta) <= maxDelta)
            return target;
        return current + Mathf.Sign(delta) * maxDelta;
    }
}

[HarmonyPatch(typeof(NTargetManager), "StartTargeting", new Type[] { typeof(TargetType), typeof(Vector2), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>) })]
internal class SovereignBladeSwordControl_StartTargeting_Position
{
    private static void Postfix()
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        if (!SovereignBladeSwordControl.TrySetActiveSword())
            return;

        SovereignBladeSwordControlState.ReleasePending = false;
        SovereignBladeSwordControlState.ReleaseCooldown = 0f;
        SovereignBladeSwordControlState.HoveredCreature = null;
        SovereignBladeSwordControlState.HoveredTimeout = 0f;
        SovereignBladeSwordControl.SetDesiredToScreenAnchor();
        ModConsole.Print("[SB] Sword control: targeting start (position)");
    }
}

[HarmonyPatch(typeof(NTargetManager), "StartTargeting", new Type[] { typeof(TargetType), typeof(Control), typeof(TargetMode), typeof(Func<bool>), typeof(Func<Node, bool>) })]
internal class SovereignBladeSwordControl_StartTargeting_Control
{
    private static void Postfix()
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        if (!SovereignBladeSwordControl.TrySetActiveSword())
            return;

        SovereignBladeSwordControlState.ReleasePending = false;
        SovereignBladeSwordControlState.ReleaseCooldown = 0f;
        SovereignBladeSwordControlState.HoveredCreature = null;
        SovereignBladeSwordControlState.HoveredTimeout = 0f;
        SovereignBladeSwordControl.SetDesiredToScreenAnchor();
        ModConsole.Print("[SB] Sword control: targeting start (control)");
    }
}

[HarmonyPatch(typeof(NTargetManager), "OnCreatureHovered")]
internal class SovereignBladeSwordControl_OnCreatureHovered
{
    private static void Postfix(NCreature creature)
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        if (!SovereignBladeSwordControlState.IsActive)
            return;

        SovereignBladeSwordControl.StartHoverAim(creature);
    }
}

[HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
internal class SovereignBladeSwordControl_FinishTargeting
{
    private static void Postfix()
    {
        if (!SovereignBladeSwordControlState.IsActive)
            return;

        SovereignBladeSwordControlState.ReleasePending = true;
        SovereignBladeSwordControlState.ReleaseCooldown = SovereignBladeSwordControlState.HoldReleaseDelaySeconds;
        SovereignBladeSwordControlState.HoveredCreature = null;
        SovereignBladeSwordControlState.HoveredTimeout = 0f;
        ModConsole.Print("[SB] Sword control: targeting end (delay)");
    }
}

[HarmonyPatch(typeof(NSovereignBladeVfx), "_Process")]
internal class SovereignBladeSwordControl_Process
{
    private static bool Prefix(NSovereignBladeVfx __instance, double delta)
    {
        if (!SovereignBladeSwordControlState.IsActive)
            return true;

        if (!ReferenceEquals(SovereignBladeSwordControlState.ActiveSword, __instance))
            return true;

        Node2D? spine = SovereignBladeSwordControlState.SpineNode;
        if (spine == null)
            return true;

        float dt = (float)delta;

        if (!SovereignBladeTargetingDebugState.TargetingActive && SovereignBladeSwordControlState.ReleasePending)
        {
            SovereignBladeSwordControlState.ReleaseCooldown -= dt;
            if (SovereignBladeSwordControlState.ReleaseCooldown <= 0f)
            {
                spine.GlobalRotation = SovereignBladeSwordControlState.RotationOffsetRad;
                SovereignBladeSwordControlState.Reset();
                ModConsole.Print("[SB] Sword control: hold canceled (delayed)");
                return true;
            }
        }

        float moveSpeed = SovereignBladeTargetingDebugState.TargetingActive
            ? SovereignBladeSwordControlState.MoveSpeed
            : SovereignBladeSwordControlState.HoldMoveSpeed;

        Vector2 currentPos = spine.GlobalPosition;
        Vector2 desiredPos = SovereignBladeSwordControlState.DesiredPosition;
        spine.GlobalPosition = currentPos.MoveToward(desiredPos, moveSpeed * dt);

        // if (SovereignBladeTargetingDebugState.TargetingActive && SovereignBladeSwordControlState.HoveredCreature != null)
        // {
        //     SovereignBladeSwordControlState.HoveredTimeout -= dt;
        //     if (SovereignBladeSwordControlState.HoveredTimeout <= 0f)
        //     {
        //         SovereignBladeSwordControlState.HoveredCreature = null;
        //         SovereignBladeSwordControlState.DesiredRotation = SovereignBladeSwordControlState.DownAngleRad + SovereignBladeSwordControlState.RotationOffsetRad;
        //     }
        // }

        if (SovereignBladeTargetingDebugState.TargetingActive && SovereignBladeSwordControlState.HoveredCreature != null)
        {
            SovereignBladeSwordControl.UpdateAimRotation(SovereignBladeSwordControlState.HoveredCreature, spine.GlobalPosition);
        }

        float currentRot = spine.GlobalRotation;
        float desiredRot = SovereignBladeSwordControlState.DesiredRotation;
        float rotSpeed = SovereignBladeTargetingDebugState.TargetingActive
            ? SovereignBladeSwordControlState.RotationSpeedRad
            : SovereignBladeSwordControlState.HoldRotationSpeedRad;
        float maxStep = rotSpeed * dt;
        spine.GlobalRotation = SovereignBladeMath.MoveTowardAngle(currentRot, desiredRot, maxStep);

        if (!SovereignBladeTargetingDebugState.TargetingActive)
        {
            SovereignBladeSwordControlState.LogCooldown -= dt;
            if (SovereignBladeSwordControlState.LogCooldown <= 0f)
            {
                ModConsole.Print($"[SB] Sword pos (hold, no target): {spine.GlobalPosition}");
                SovereignBladeSwordControlState.LogCooldown = SovereignBladeSwordControlState.LogIntervalSeconds;
            }
        }

        return false;
    }
}

[HarmonyPatch]
internal class SovereignBladeSwordControl_MouseCardPlay_Start
{
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method("MegaCrit.Sts2.Core.Nodes.Combat.NMouseCardPlay:Start");
    }

    private static void Postfix(object __instance)
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        if (!SovereignBladeSwordControl.TrySetActiveSword())
            return;

        SovereignBladeSwordControlState.ReleasePending = false;
        SovereignBladeSwordControlState.ReleaseCooldown = 0f;
        SovereignBladeSwordControlState.HoveredCreature = null;
        SovereignBladeSwordControlState.HoveredTimeout = 0f;
        if (SovereignBladeSwordControl.SetDesiredToScreenAnchor())
        {
            ModConsole.Print("[SB] Sword control: hold anchor set (mouse)");
        }
    }
}

[HarmonyPatch]
internal class SovereignBladeSwordControl_ControllerCardPlay_Start
{
    private static MethodBase? TargetMethod()
    {
        return AccessTools.Method("MegaCrit.Sts2.Core.Nodes.Combat.NControllerCardPlay:Start");
    }

    private static void Postfix(object __instance)
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        if (!SovereignBladeSwordControl.TrySetActiveSword())
            return;

        SovereignBladeSwordControlState.ReleasePending = false;
        SovereignBladeSwordControlState.ReleaseCooldown = 0f;
        SovereignBladeSwordControlState.HoveredCreature = null;
        SovereignBladeSwordControlState.HoveredTimeout = 0f;
        if (SovereignBladeSwordControl.SetDesiredToScreenAnchor())
        {
            ModConsole.Print("[SB] Sword control: hold anchor set (controller)");
        }
    }
}

[HarmonyPatch(typeof(NCardHolder), "OnMouseReleased")]
internal class SovereignBladeSwordControl_OnMouseReleased
{
    private static void Postfix(NCardHolder __instance, InputEvent inputEvent)
    {
        if (!SovereignBladeTargetingDebugState.IsSovereignBladeActive)
            return;

        if (SovereignBladeTargetingDebugState.TargetingActive)
            return;

        if (inputEvent is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Left)
            return;

        if (SovereignBladeSwordControlState.IsActive)
        {
            SovereignBladeSwordControlState.ReleasePending = true;
            SovereignBladeSwordControlState.ReleaseCooldown = SovereignBladeSwordControlState.HoldReleaseDelaySeconds;
            ModConsole.Print("[SB] Sword control: hold canceled (delay)");
        }
    }
}
