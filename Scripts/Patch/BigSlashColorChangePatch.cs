using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using Godot;

namespace BetterSovereignBlade.Scripts.Patch;

public class BigSlashColorChangePatch
{
   
}

[HarmonyPatch(typeof(NBigSlashImpactVfx), nameof(NBigSlashImpactVfx.Create), new[] { typeof(Vector2) })]
internal static class BigSlashColorChangePatch_Create
{
    private static readonly Color CustomTint = Color.FromHtml("#ff7ad9");
    private const float RotationDegrees = 60f;

    private static bool Prefix(Vector2 targetCenterPosition, ref NBigSlashImpactVfx __result)
    {
        __result = NBigSlashImpactVfx.Create(targetCenterPosition, RotationDegrees, CustomTint);
        return false;
    }
}
