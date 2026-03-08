using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using System.Runtime.CompilerServices;
using BetterSovereignBlade.Scripts.Debug;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
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
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace BetterSovereignBlade.Scripts.Patch;

[HarmonyPatch(typeof(NSovereignBladeVfx), nameof(NSovereignBladeVfx._Process))]
internal static class SovereignBladeDragPatch
{
private sealed class BladeState
{
public bool Dragging;
public bool Inertia;
public bool Returning;
public bool OwnsTargeting;
public bool AwaitingSelection;
public Vector2 Velocity = Vector2.Zero;

public NCreature? VisualLockedTarget;
public double VisualLockUntil;

public double PlayQueuedUntil;
}

private static readonly ConditionalWeakTable<NSovereignBladeVfx, BladeState> States = new();

private static readonly System.Reflection.FieldInfo SpineField =
AccessTools.Field(typeof(NSovereignBladeVfx), "_spineNode");

private static readonly System.Reflection.FieldInfo IsAttackingField =
AccessTools.Field(typeof(NSovereignBladeVfx), "_isAttacking");

private static readonly System.Reflection.FieldInfo TargetingArrowField =
AccessTools.Field(typeof(NTargetManager), "_targetingArrow");

private static readonly System.Reflection.PropertyInfo HoveredNodeProperty =
AccessTools.Property(typeof(NTargetManager), "HoveredNode");

static bool specialMode = true;

static bool rotateTowardsMouseWhileDragging = false;

const float keepDragRotationAngle = 0f;

const float startDragRadius = 80f;
const float followRadius = 100f;
const float dragRadius = 5000f;

const float dragSpeed = 8f;
const float slowDown = 0.15f;

const float maxRotationSpeed = 36f;

const float lockedY = 320f;

const float returnRotateSpeed = 48f;
const float returnSnapEpsilon = 0.02f;

const double hoverLeaveHoldSeconds = 0.5;
const double playQueueGuardSeconds = 0.15;

private static BladeState GetState(NSovereignBladeVfx vfx) => States.GetOrCreateValue(vfx);

private static double NowSeconds => Time.GetTicksMsec() / 1000.0;

static bool Prefix(NSovereignBladeVfx __instance, double delta)
{
if (!specialMode)
{
return true;
}

if (!LocalContext.IsMine(__instance.Card))
{
return true;
}

BladeState state = GetState(__instance);
float dt = (float)delta;

Node2D? spine = SpineField.GetValue(__instance) as Node2D;
if (spine == null)
{
return true;
}

bool isAttacking = IsAttackingField.GetValue(__instance) is bool value && value;

Vector2 mouse = __instance.GetGlobalMousePosition();
float distToMouse = spine.GlobalPosition.DistanceTo(mouse);
bool leftPressed = Input.IsMouseButtonPressed(MouseButton.Left);

bool canAttack = CanAttack(__instance.Card as SovereignBlade, state);

if (!state.Dragging &&
leftPressed &&
distToMouse < startDragRadius &&
!(NPlayerHand.Instance?.InCardPlay ?? false))
{
state.Dragging = true;
state.Inertia = false;
state.Returning = false;
state.Velocity = Vector2.Zero;
}

UpdateSwordTargeting(spine, state, canAttack, leftPressed);
UpdateVisualLockFromHover(state);

if (isAttacking)
{
return true;
}

if (state.Dragging && leftPressed)
{
if (canAttack && TryMoveToVisualLock(spine, state, dt))
{
return false;
}

FollowMouse(spine, mouse, state, dt);
return false;
}

if (state.Dragging && !leftPressed)
{
    state.Dragging = false;

    if (!canAttack || !state.OwnsTargeting)
    {
        ClearVisualLock(state);
        state.Inertia = true;
        state.Returning = false;
        return false;
    }

    return false;
}

if (state.AwaitingSelection)
{
    TryMoveToVisualLock(spine, state, dt);
    return false;
}

if (HasVisualLock(state))
{
if (TryMoveToVisualLock(spine, state, dt))
{
return false;
}
}

if (state.Inertia)
{
spine.GlobalPosition += state.Velocity * dt;
state.Velocity = state.Velocity.Lerp(Vector2.Zero, 5f * dt);

if (state.Velocity.Length() < 5f)
{
state.Inertia = false;
state.Returning = true;
SnapOrbitProgress(__instance, spine);
}

return false;
}

if (state.Returning)
{
if (RotateTowardsAngle(spine, 0f, returnRotateSpeed, dt, returnSnapEpsilon))
{
state.Returning = false;
}

return false;
}

return true;
}

private static void UpdateSwordTargeting(
Node2D spine,
BladeState state,
bool canAttack,
bool leftPressed)
{
NTargetManager? manager = NTargetManager.Instance;
if (manager == null)
{
return;
}

bool shouldOwnTargeting = canAttack && state.Dragging && leftPressed;

if (shouldOwnTargeting)
{
if (!state.OwnsTargeting && !manager.IsInSelection)
{
manager.StartTargeting(
TargetType.AnyEnemy,
spine.GlobalPosition,
TargetMode.ClickMouseToTarget,
null,
null);

(TargetingArrowField.GetValue(manager) as NTargetingArrow)?.StopDrawing();

state.OwnsTargeting = true;
state.AwaitingSelection = true;

TaskHelper.RunSafely(WaitForSelectionResult(state));
}
}
else if (state.OwnsTargeting && !state.AwaitingSelection)
{
if (manager.IsInSelection)
{
manager.CancelTargeting();
}

state.OwnsTargeting = false;
}
}

private static async Task WaitForSelectionResult(BladeState state)
{
NTargetManager? manager = NTargetManager.Instance;
if (manager == null)
{
state.OwnsTargeting = false;
state.AwaitingSelection = false;
return;
}

Node? node = await manager.SelectionFinished();

state.OwnsTargeting = false;
state.AwaitingSelection = false;

if (node is NCreature creature)
{
state.VisualLockedTarget = creature;
state.VisualLockUntil = NowSeconds + hoverLeaveHoldSeconds;

// ModConsole.Print(creature.Entity);

bool played = PlayCardLikeManual(
FindPlayableSovereignBladeInHand(),
creature.Entity);

if (played)
{
state.PlayQueuedUntil = NowSeconds + playQueueGuardSeconds;
}
else
{
state.Inertia = true;
state.Returning = false;
}

return;
}

ClearVisualLock(state);
state.Inertia = true;
state.Returning = false;
}

private static bool CanAttack(SovereignBlade? card, BladeState state)
{
if (card == null)
{
return false;
}

if (!LocalContext.IsMine(card))
{
return false;
}

if (NowSeconds < state.PlayQueuedUntil)
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

private static void UpdateVisualLockFromHover(BladeState state)
{
NCreature? hovered = GetRealHoveredCreature(state);
if (hovered != null)
{
state.VisualLockedTarget = hovered;
state.VisualLockUntil = NowSeconds + hoverLeaveHoldSeconds;
return;
}

if (!HasVisualLock(state))
{
ClearVisualLock(state);
}
}

private static NCreature? GetRealHoveredCreature(BladeState state)
{
if (!state.OwnsTargeting)
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

private static bool HasVisualLock(BladeState state)
{
if (state.VisualLockedTarget == null)
{
return false;
}

if (!GodotObject.IsInstanceValid(state.VisualLockedTarget))
{
state.VisualLockedTarget = null;
return false;
}

if (!state.VisualLockedTarget.Entity.IsAlive)
{
state.VisualLockedTarget = null;
return false;
}

return NowSeconds <= state.VisualLockUntil;
}

private static void ClearVisualLock(BladeState state)
{
state.VisualLockedTarget = null;
state.VisualLockUntil = 0.0;
}

private static bool TryMoveToVisualLock(
Node2D spine,
BladeState state,
float dt)
{
if (!HasVisualLock(state))
{
return false;
}

NCreature creature = state.VisualLockedTarget!;
Vector2 target = new Vector2(creature.GetTopOfHitbox().X, lockedY);

Vector2 oldPos = spine.GlobalPosition;
spine.GlobalPosition = spine.GlobalPosition.Lerp(target, dragSpeed * dt);
state.Velocity = (spine.GlobalPosition - oldPos) / Math.Max(dt, 0.0001f);

RotateTowardsAngle(spine, Mathf.Pi/2, maxRotationSpeed, dt);
return true;
}

private static void FollowMouse(
Node2D spine,
Vector2 mouse,
BladeState state,
float dt)
{
float dist = spine.GlobalPosition.DistanceTo(mouse);

if (dist > followRadius && dist < dragRadius)
{
Vector2 dir = (mouse - spine.GlobalPosition).Normalized();
Vector2 target = mouse - dir * (100f + state.Velocity.Length() * 0.05f);

Vector2 oldPos = spine.GlobalPosition;
spine.GlobalPosition = spine.GlobalPosition.Lerp(target, dragSpeed * dt);
state.Velocity = (spine.GlobalPosition - oldPos) / Math.Max(dt, 0.0001f);
}
else if (dist <= followRadius)
{
state.Velocity *= 1f - slowDown;
spine.GlobalPosition += state.Velocity * dt;
}

if (rotateTowardsMouseWhileDragging)
{
RotateTowards(spine, mouse, maxRotationSpeed, dt);
}
else
{
RotateTowardsAngle(spine, keepDragRotationAngle, maxRotationSpeed, dt);
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
Vector2 local = orbitPath.ToLocal(spine.GlobalPosition);

float offset = curve.GetClosestOffset(local);
float bakedLength = curve.GetBakedLength();

if (bakedLength > 0f)
{
instance.OrbitProgress = offset / bakedLength;
}
}

private static void RotateTowards(
Node2D node,
Vector2 target,
float maxSpeed,
float dt)
{
float targetAngle = (target - node.GlobalPosition).Angle();
RotateTowardsAngle(node, targetAngle, maxSpeed, dt);
}

private static bool RotateTowardsAngle(
Node2D node,
float targetAngle,
float maxSpeed,
float dt,
float epsilon = 0.02f)
{
float current = node.GlobalRotation;

float diff = Mathf.Wrap(
targetAngle - current,
-Mathf.Pi,
Mathf.Pi);

if (Mathf.Abs(diff) <= epsilon)
{
node.GlobalRotation = targetAngle;
return true;
}

float step = Mathf.Clamp(
diff,
-maxSpeed * dt,
maxSpeed * dt);

node.GlobalRotation += step;

float remain = Mathf.Wrap(
targetAngle - node.GlobalRotation,
-Mathf.Pi,
Mathf.Pi);

return Mathf.Abs(remain) <= epsilon;
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
                return null;

            FieldInfo currentCardPlayField = AccessTools.Field(typeof(NPlayerHand), "_currentCardPlay");
            var currentCardPlay = currentCardPlayField?.GetValue(hand) as NCardPlay;
            if (currentCardPlay == null || !GodotObject.IsInstanceValid(currentCardPlay))
                return null;

            PropertyInfo holderProp = AccessTools.Property(typeof(NCardPlay), "Holder");
            var holder = holderProp?.GetValue(currentCardPlay) as NHandCardHolder;
            if (holder == null || !GodotObject.IsInstanceValid(holder))
                return null;

            // 如果 holder 的父节点是 CardHolderContainer，说明已回到手牌
            if (holder.GetParent() is Control parent && parent.Name == "CardHolderContainer")
                return null;

            return holder.CardModel;
        }
    }
