using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace BetterSovereignBlade.Scripts.Patch;

[HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx._Process))]
internal static class SovereignBladeDragPatch
{
    // Motion flags and target selection are independent: releasing the mouse can
    // leave a target selection pending, so they must not be collapsed into one mode.
    private sealed class BladeDragState
    {
        public bool IsDragging;
        public bool IsCoasting;
        public bool IsRestoringRestingRotation;
        public bool OwnsTargetSelection;
        public bool IsAwaitingTargetSelection;
        public Vector2 DragVelocity = Vector2.Zero;

        public NCreature? VisualLockTarget;
        public double VisualLockExpiresAtSeconds;

        public double PlayQueueGuardExpiresAtSeconds;

        public SovereignBladeTrailController? TrailController;
    }

    private static readonly ConditionalWeakTable<NSovereignBladeVfx, BladeDragState> States = new();

    private static readonly FieldInfo SpineField =
        AccessTools.Field(typeof(NSovereignBladeVfx), "_spineNode");

    private static readonly FieldInfo IsAttackingField =
        AccessTools.Field(typeof(NSovereignBladeVfx), "_isAttacking");

    private static readonly FieldInfo TargetingArrowField =
        AccessTools.Field(typeof(NTargetManager), "_targetingArrow");

    private static readonly PropertyInfo HoveredNodeProperty =
        AccessTools.Property(typeof(NTargetManager), "HoveredNode");

    private static bool DirectSwordDragEnabled = true;

    private const float FixedDragRotationRadians = 0f;

    private const float DragStartDistance = 80f;
    private const float MouseFollowDeadZoneDistance = 100f;
    private const float MouseFollowOffsetDistance = 100f;
    private const float MaxMouseFollowDistance = 5000f;

    private const float PositionFollowRatePerSecond = 8f;
    // Preserve the original per-frame damping so this refactor does not change drag feel.
    private const float DragVelocityDampingPerFrame = 0.15f;
    private const float CoastingDampingRatePerSecond = 5f;
    private const float CoastingStopSpeed = 5f;
    private const float VelocityFollowDistanceSeconds = 0.05f;
    private const float MinimumVelocityDeltaSeconds = 0.0001f;

    private const float DragRotationSpeedRadiansPerSecond = 36f;

    private const float TargetLockGlobalY = 320f;

    private const float RestingRotationSpeedRadiansPerSecond = 48f;
    private const float RestingRotationToleranceRadians = 0.02f;

    private const double HoverTargetGraceSeconds = 0.5;
    private const double PlayQueueGuardSeconds = 0.15;

    private static BladeDragState GetState(NSovereignBladeVfx vfx) => States.GetOrCreateValue(vfx);

    private static double NowSeconds => Time.GetTicksMsec() / 1000.0;

    private static bool RotateTowardsMouseWhileDragging => PaletteSettingsMod.IsDragRotateEnabled();

    private static bool Prefix(NSovereignBladeVfx __instance, double delta)
    {
        if (!DirectSwordDragEnabled)
        {
            return true;
        }

        if (!LocalContext.IsMine(__instance.Card))
        {
            return true;
        }

        BladeDragState state = GetState(__instance);
        float deltaSeconds = (float)delta;
        state.TrailController ??= new SovereignBladeTrailController();

        Node2D? spine = SpineField.GetValue(__instance) as Node2D;
        if (spine == null)
        {
            return true;
        }

        bool isAttacking = IsAttackingField.GetValue(__instance) is bool value && value;

        Vector2 mouseGlobalPosition = __instance.GetGlobalMousePosition();
        float distanceToMouse = spine.GlobalPosition.DistanceTo(mouseGlobalPosition);
        bool isLeftMousePressed = Input.IsMouseButtonPressed(MouseButton.Left);

        bool canPlayBladeCard = CanPlayBladeCard(__instance.Card as SovereignBlade, state);

        TryBeginDrag(__instance, state, isLeftMousePressed, distanceToMouse);

        // Keep targeting, visual locking and trail updates before the attack-animation guard.
        UpdateSwordTargeting(spine, state, canPlayBladeCard, isLeftMousePressed);
        UpdateVisualLockFromHover(state);

        bool showTrail = state.IsDragging && isLeftMousePressed && RotateTowardsMouseWhileDragging;
        state.TrailController.Update(__instance, spine, showTrail, state.DragVelocity, deltaSeconds);

        if (isAttacking)
        {
            return true;
        }

        if (state.IsDragging && isLeftMousePressed)
        {
            if (canPlayBladeCard && TryMoveToVisualLock(spine, state, deltaSeconds))
            {
                return false;
            }

            FollowMouse(spine, mouseGlobalPosition, state, deltaSeconds);
            return false;
        }

        if (state.IsDragging && !isLeftMousePressed)
        {
            HandleDragRelease(state, canPlayBladeCard);
            return false;
        }

        if (state.IsAwaitingTargetSelection)
        {
            TryMoveToVisualLock(spine, state, deltaSeconds);
            return false;
        }

        if (HasVisualLock(state))
        {
            if (TryMoveToVisualLock(spine, state, deltaSeconds))
            {
                return false;
            }
        }

        if (state.IsCoasting)
        {
            UpdateCoasting(__instance, spine, state, deltaSeconds);
            return false;
        }

        if (state.IsRestoringRestingRotation)
        {
            RestoreRestingRotation(spine, state, deltaSeconds);
            return false;
        }

        return true;
    }

    private static void TryBeginDrag(
        NSovereignBladeVfx instance,
        BladeDragState state,
        bool isLeftMousePressed,
        float distanceToMouse)
    {
        if (!state.IsDragging &&
            isLeftMousePressed &&
            distanceToMouse < DragStartDistance &&
            CanUseDirectSwordDrag(instance.Card as SovereignBlade))
        {
            state.IsDragging = true;
            state.IsCoasting = false;
            state.IsRestoringRestingRotation = false;
            state.DragVelocity = Vector2.Zero;
        }
    }

    private static void HandleDragRelease(BladeDragState state, bool canPlayBladeCard)
    {
        state.IsDragging = false;
        state.TrailController!.StopEmission();

        // Keep an owned selection alive: its async result decides whether to play or coast.
        if (!canPlayBladeCard || !state.OwnsTargetSelection)
        {
            ClearVisualLock(state);
            BeginCoasting(state);
        }
    }

    private static void BeginCoasting(BladeDragState state)
    {
        state.IsCoasting = true;
        state.IsRestoringRestingRotation = false;
    }

    private static void UpdateCoasting(
        NSovereignBladeVfx instance,
        Node2D spine,
        BladeDragState state,
        float deltaSeconds)
    {
        spine.GlobalPosition += state.DragVelocity * deltaSeconds;
        state.DragVelocity = state.DragVelocity.Lerp(Vector2.Zero, CoastingDampingRatePerSecond * deltaSeconds);

        if (state.DragVelocity.Length() < CoastingStopSpeed)
        {
            state.IsCoasting = false;
            state.IsRestoringRestingRotation = true;
            SnapOrbitProgress(instance, spine);
        }
    }

    private static void RestoreRestingRotation(Node2D spine, BladeDragState state, float deltaSeconds)
    {
        if (RotateTowardsAngle(spine, 0f, RestingRotationSpeedRadiansPerSecond, deltaSeconds, RestingRotationToleranceRadians))
        {
            state.IsRestoringRestingRotation = false;
        }
    }

    private static bool CanUseDirectSwordDrag(SovereignBlade? card)
    {
        if (card == null)
        {
            return false;
        }

        if (!LocalContext.IsMine(card))
        {
            return false;
        }

        NPlayerHand? hand = NPlayerHand.Instance;
        if (hand == null)
        {
            return false;
        }

        if (hand.InCardPlay)
        {
            return false;
        }

        if (NTargetManager.Instance?.IsInSelection ?? false)
        {
            return false;
        }

        return true;
    }

    private static void CancelStandaloneTargetingIfNeeded(BladeDragState state)
    {
        if (!state.OwnsTargetSelection)
        {
            return;
        }

        NTargetManager? manager = NTargetManager.Instance;
        if (manager?.IsInSelection ?? false)
        {
            manager.CancelTargeting();
        }

        state.OwnsTargetSelection = false;
        state.IsAwaitingTargetSelection = false;
    }

    private static void UpdateSwordTargeting(
        Node2D spine,
        BladeDragState state,
        bool canPlayBladeCard,
        bool isLeftMousePressed)
    {
        NTargetManager? manager = NTargetManager.Instance;
        if (manager == null)
        {
            state.OwnsTargetSelection = false;
            state.IsAwaitingTargetSelection = false;
            return;
        }

        NPlayerHand? hand = NPlayerHand.Instance;
        bool canUseHandCard =
            hand != null &&
            hand.CurrentMode == NPlayerHand.Mode.Play &&
            !hand.InCardPlay &&
            FindPlayableSovereignBladeInHand() != null;

        bool shouldOwnTargeting =
            canPlayBladeCard &&
            state.IsDragging &&
            isLeftMousePressed &&
            canUseHandCard;

        if (shouldOwnTargeting)
        {
            if (!state.OwnsTargetSelection && !manager.IsInSelection)
            {
                manager.StartTargeting(
                    TargetType.AnyEnemy,
                    spine.GlobalPosition,
                    TargetMode.ClickMouseToTarget,
                    null,
                    null);

                (TargetingArrowField.GetValue(manager) as NTargetingArrow)?.StopDrawing();

                state.OwnsTargetSelection = true;
                state.IsAwaitingTargetSelection = true;

                TaskHelper.RunSafely(WaitForSelectionResult(state));
            }
        }
        else if (state.OwnsTargetSelection && !state.IsAwaitingTargetSelection)
        {
            CancelStandaloneTargetingIfNeeded(state);
        }
    }

    private static async Task WaitForSelectionResult(BladeDragState state)
    {
        NTargetManager? manager = NTargetManager.Instance;
        if (manager == null)
        {
            state.OwnsTargetSelection = false;
            state.IsAwaitingTargetSelection = false;
            return;
        }

        Node? node = await manager.SelectionFinished();

        state.OwnsTargetSelection = false;
        state.IsAwaitingTargetSelection = false;

        if (node is NCreature creature)
        {
            state.VisualLockTarget = creature;
            state.VisualLockExpiresAtSeconds = NowSeconds + HoverTargetGraceSeconds;

            bool played = PlayCardLikeManual(
                FindPlayableSovereignBladeInHand(),
                creature.Entity);

            if (played)
            {
                state.PlayQueueGuardExpiresAtSeconds = NowSeconds + PlayQueueGuardSeconds;
            }
            else
            {
                BeginCoasting(state);
            }

            return;
        }

        ClearVisualLock(state);
        BeginCoasting(state);
    }

    private static bool CanPlayBladeCard(SovereignBlade? card, BladeDragState state)
    {
        if (card == null)
        {
            return false;
        }

        if (!LocalContext.IsMine(card))
        {
            return false;
        }

        if (NowSeconds < state.PlayQueueGuardExpiresAtSeconds)
        {
            return false;
        }

        if (NPlayerHand.Instance?.InCardPlay ?? false)
        {
            return false;
        }

        if (card.Owner.Creature.HasPower<SeekingEdgePower>())
        {
            return false;
        }

        return card.Pile?.Type == PileType.Hand && card.CanPlay();
    }

    private static SovereignBlade? FindPlayableSovereignBladeInHand()
    {
        NPlayerHand? hand = NPlayerHand.Instance;
        if (hand == null)
        {
            return null;
        }

        foreach (var holder in hand.ActiveHolders)
        {
            if (holder.CardModel is SovereignBlade blade &&
                LocalContext.IsMine(blade) &&
                blade.Pile?.Type == PileType.Hand &&
                !blade.Owner.Creature.HasPower<SeekingEdgePower>() &&
                blade.CanPlay())
            {
                return blade;
            }
        }

        return null;
    }

    private static bool PlayCardLikeManual(CardModel? card, Creature? target)
    {
        return card != null &&
            LocalContext.IsMine(card) &&
            card.Pile?.Type == PileType.Hand &&
            card.CanPlayTargeting(target) &&
            card.TryManualPlay(target);
    }

    private static void UpdateVisualLockFromHover(BladeDragState state)
    {
        NCreature? hovered = GetRealHoveredCreature(state);
        if (hovered != null)
        {
            state.VisualLockTarget = hovered;
            state.VisualLockExpiresAtSeconds = NowSeconds + HoverTargetGraceSeconds;
            return;
        }

        if (!HasVisualLock(state))
        {
            ClearVisualLock(state);
        }
    }

    private static NCreature? GetRealHoveredCreature(BladeDragState state)
    {
        if (!state.OwnsTargetSelection)
        {
            return null;
        }

        NTargetManager? manager = NTargetManager.Instance;
        if (manager == null || !manager.IsInSelection)
        {
            return null;
        }

        return HoveredNodeProperty.GetValue(manager) as NCreature;
    }

    private static bool HasVisualLock(BladeDragState state)
    {
        if (state.VisualLockTarget == null)
        {
            return false;
        }

        if (!GodotObject.IsInstanceValid(state.VisualLockTarget))
        {
            state.VisualLockTarget = null;
            return false;
        }

        if (!state.VisualLockTarget.Entity.IsAlive)
        {
            state.VisualLockTarget = null;
            return false;
        }

        return NowSeconds <= state.VisualLockExpiresAtSeconds;
    }

    private static void ClearVisualLock(BladeDragState state)
    {
        state.VisualLockTarget = null;
        state.VisualLockExpiresAtSeconds = 0.0;
    }

    private static bool TryMoveToVisualLock(
        Node2D spine,
        BladeDragState state,
        float deltaSeconds)
    {
        if (!HasVisualLock(state))
        {
            return false;
        }

        NCreature creature = state.VisualLockTarget!;
        Vector2 targetPosition = new Vector2(creature.GetTopOfHitbox().X, TargetLockGlobalY);

        Vector2 previousPosition = spine.GlobalPosition;
        spine.GlobalPosition = spine.GlobalPosition.Lerp(targetPosition, PositionFollowRatePerSecond * deltaSeconds);
        state.DragVelocity = (spine.GlobalPosition - previousPosition) / Mathf.Max(deltaSeconds, MinimumVelocityDeltaSeconds);

        RotateTowardsAngle(spine, Mathf.Pi / 2, DragRotationSpeedRadiansPerSecond, deltaSeconds);
        return true;
    }

    private static void FollowMouse(
        Node2D spine,
        Vector2 mouseGlobalPosition,
        BladeDragState state,
        float deltaSeconds)
    {
        float distanceToMouse = spine.GlobalPosition.DistanceTo(mouseGlobalPosition);

        if (distanceToMouse > MouseFollowDeadZoneDistance && distanceToMouse < MaxMouseFollowDistance)
        {
            Vector2 directionToMouse = (mouseGlobalPosition - spine.GlobalPosition).Normalized();
            Vector2 targetPosition = mouseGlobalPosition - directionToMouse * (MouseFollowOffsetDistance + state.DragVelocity.Length() * VelocityFollowDistanceSeconds);

            Vector2 previousPosition = spine.GlobalPosition;
            spine.GlobalPosition = spine.GlobalPosition.Lerp(targetPosition, PositionFollowRatePerSecond * deltaSeconds);
            state.DragVelocity = (spine.GlobalPosition - previousPosition) / Mathf.Max(deltaSeconds, MinimumVelocityDeltaSeconds);
        }
        else if (distanceToMouse <= MouseFollowDeadZoneDistance)
        {
            state.DragVelocity *= 1f - DragVelocityDampingPerFrame;
            spine.GlobalPosition += state.DragVelocity * deltaSeconds;
        }

        if (RotateTowardsMouseWhileDragging)
        {
            RotateTowards(spine, mouseGlobalPosition, DragRotationSpeedRadiansPerSecond, deltaSeconds);
        }
        else
        {
            RotateTowardsAngle(spine, FixedDragRotationRadians, DragRotationSpeedRadiansPerSecond, deltaSeconds);
        }
    }

    private static void SnapOrbitProgress(NSovereignBladeVfx instance, Node2D spine)
    {
        Path2D? orbitPath = instance.GetNodeOrNull<Path2D>("%Path");
        if (orbitPath == null)
        {
            return;
        }

        Curve2D curve = orbitPath.Curve;
        Vector2 localPosition = orbitPath.ToLocal(spine.GlobalPosition);

        float closestOffset = curve.GetClosestOffset(localPosition);
        float bakedLength = curve.GetBakedLength();

        if (bakedLength > 0f)
        {
            instance.OrbitProgress = closestOffset / bakedLength;
        }
    }

    private static void RotateTowards(
        Node2D node,
        Vector2 target,
        float maxRadiansPerSecond,
        float deltaSeconds)
    {
        float targetAngle = (target - node.GlobalPosition).Angle();
        RotateTowardsAngle(node, targetAngle, maxRadiansPerSecond, deltaSeconds);
    }

    private static bool RotateTowardsAngle(
        Node2D node,
        float targetAngle,
        float maxRadiansPerSecond,
        float deltaSeconds,
        float toleranceRadians = 0.02f)
    {
        float currentAngle = node.GlobalRotation;

        float angleDifference = Mathf.Wrap(
            targetAngle - currentAngle,
            -Mathf.Pi,
            Mathf.Pi);

        if (Mathf.Abs(angleDifference) <= toleranceRadians)
        {
            node.GlobalRotation = targetAngle;
            return true;
        }

        float rotationStep = Mathf.Clamp(
            angleDifference,
            -maxRadiansPerSecond * deltaSeconds,
            maxRadiansPerSecond * deltaSeconds);

        node.GlobalRotation += rotationStep;

        float remainingAngle = Mathf.Wrap(
            targetAngle - node.GlobalRotation,
            -Mathf.Pi,
            Mathf.Pi);

        return Mathf.Abs(remainingAngle) <= toleranceRadians;
    }

    [HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx._ExitTree))]
    private static class SovereignBladeDragCleanupPatch
    {
        private static void Prefix(NSovereignBladeVfx __instance)
        {
            if (!States.TryGetValue(__instance, out BladeDragState? state))
            {
                return;
            }

            state.TrailController?.Dispose();
            state.TrailController = null;
        }
    }
}

public static class ActiveCardHelper
{
    public static Player? GetCurrentPlayer()
    {
        return LocalContext.GetMe(CombatManager.Instance.DebugOnlyGetState())
               ?? LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState());
    }

    public static CardModel? GetCurrentlyPlayingCard()
    {
        NPlayerHand? hand = NCombatRoom.Instance?.Ui?.Hand;
        if (hand == null)
        {
            return null;
        }

        FieldInfo currentCardPlayField = AccessTools.Field(typeof(NPlayerHand), "_currentCardPlay");
        var currentCardPlay = currentCardPlayField?.GetValue(hand) as NCardPlay;
        if (currentCardPlay == null || !GodotObject.IsInstanceValid(currentCardPlay))
        {
            return null;
        }

        PropertyInfo holderProperty = AccessTools.Property(typeof(NCardPlay), "Holder");
        var holder = holderProperty?.GetValue(currentCardPlay) as NHandCardHolder;
        if (holder == null || !GodotObject.IsInstanceValid(holder))
        {
            return null;
        }

        // 如果 holder 的父节点是 CardHolderContainer，说明已回到手牌
        if (holder.GetParent() is Control parent && parent.Name == "CardHolderContainer")
        {
            return null;
        }

        return holder.CardModel;
    }
}
