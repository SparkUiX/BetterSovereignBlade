using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using System.Runtime.CompilerServices;
using BetterSovereignBlade.Scripts.Debug;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Nodes.Debug;

namespace BetterSovereignBlade.Scripts.Patch;
[HarmonyPatch(typeof(NSovereignBladeVfx), "_Process")]
class SovereignBladeDragPatch
{
    static bool dragging = false;

    static bool Prefix(NSovereignBladeVfx __instance, double delta)
    {
        Node2D spine = AccessTools.Field(
            typeof(NSovereignBladeVfx),
            "_spineNode"
        ).GetValue(__instance) as Node2D;

        if (spine == null)
            return true;

        Vector2 mouse = __instance.GetGlobalMousePosition();

        // 鼠标按住 -> 拖动
        if (Input.IsMouseButtonPressed(MouseButton.Left)&&spine.GlobalPosition.DistanceTo(mouse) < 80f)
        {
            dragging = true;
            spine.GlobalPosition = mouse;
            return false; // 阻止原逻辑
        }

        // 鼠标刚松开
        if (dragging)
        {
            dragging = false;

            Path2D orbitPath = AccessTools.Field(
                typeof(NSovereignBladeVfx),
                "_orbitPath"
            ).GetValue(__instance) as Path2D;

            if (orbitPath != null)
            {
                Curve2D curve = orbitPath.Curve;

                Vector2 local = orbitPath.ToLocal(spine.GlobalPosition);

                float offset = curve.GetClosestOffset(local);
                float bakedLength = curve.GetBakedLength();

                __instance.OrbitProgress = offset / bakedLength;
            }
        }

        return true; // 恢复原 _Process
    }
}