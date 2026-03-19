// using Godot;
// using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
// using MegaCrit.Sts2.Core.Nodes.Combat;
//
// namespace BetterSovereignBlade.Scripts.Patch;
//
// internal static class HandDecisionPointUtil
// {
//     // 建议：使用 Hitbox 的全局中心作为判定点（最符合玩家直觉）
//     public static Vector2 GetDecisionPointGlobal(NHandCardHolder holder)
//     {
//         if (holder == null || !GodotObject.IsInstanceValid(holder))
//         {
//             return default;
//         }
//
//         // holder 和 Hitbox 都是 Control：GlobalPosition 是左上角（或控件原点），Size 是控件大小
//         Control hitbox = holder.Hitbox;
//         if (hitbox != null && GodotObject.IsInstanceValid(hitbox))
//         {
//             return hitbox.GlobalPosition + hitbox.Size * 0.5f;
//         }
//
//         // 兜底：用 holder 自己的中心
//         return holder.GlobalPosition + holder.Size * 0.5f;
//     }
// }
// internal partial class HandInsertDebugOverlayLive : Control
// {
//     public static bool Enabled = true;
//
//     private static HandInsertDebugOverlayLive _instance;
//     
//
//     private readonly List<Vector2> _cardDecisionPoints = new();
//     private readonly List<Vector2> _midpoints = new();
//
//     private bool _hasMouseX;
//     private float _mouseX;
//     private static WeakRef _trackedHand;
//
//     public static void TrackHand(NPlayerHand hand)
//     {
//         if (hand == null || !GodotObject.IsInstanceValid(hand))
//         {
//             _trackedHand = null;
//             return;
//         }
//
//         // Godot 4.5.1 mono: 通过 GodotObject.WeakRef(obj) 创建
//         _trackedHand = GodotObject.WeakRef(hand);
//
//         Ensure(hand.GetViewport());
//     }
//
//     private static NPlayerHand TryGetTrackedHand()
//     {
//         if (_trackedHand == null || !GodotObject.IsInstanceValid(_trackedHand))
//         {
//             return null;
//         }
//
//         Variant? v = _trackedHand.GetRef(); // Variant?
//         if (v == null)
//         {
//             return null;
//         }
//
//         GodotObject obj = v.Value.AsGodotObject();
//         if (obj == null || !GodotObject.IsInstanceValid(obj))
//         {
//             return null;
//         }
//
//         return obj as NPlayerHand;
//     }
//
//     public override void _Process(double delta)
//     {
//         if (!Enabled)
//         {
//             return;
//         }
//
//         NPlayerHand hand = TryGetTrackedHand();
//         if (hand == null || !GodotObject.IsInstanceValid(hand))
//         {
//             _cardDecisionPoints.Clear();
//             _midpoints.Clear();
//             _hasMouseX = false;
//             QueueRedraw();
//             return;
//         }
//
//         var vp = hand.GetViewport();
//         if (vp == null)
//         {
//             return;
//         }
//
//         // mouseX：直接取 viewport 的鼠标位置（屏幕/全局一致坐标系）
//         _mouseX = vp.GetMousePosition().X;
//         _hasMouseX = true;
//
//         var holders = hand.ActiveHolders;
//         _cardDecisionPoints.Clear();
//         _midpoints.Clear();
//
//         if (holders == null || holders.Count == 0)
//         {
//             QueueRedraw();
//             return;
//         }
//
//         // 红点：每张牌的判定点（Hitbox中心）
//         for (int i = 0; i < holders.Count; i++)
//         {
//             NHandCardHolder h = holders[i];
//             if (h == null || !GodotObject.IsInstanceValid(h))
//             {
//                 continue;
//             }
//
//             Vector2 point = HandDecisionPointUtil.GetDecisionPointGlobal(h);
//             point.Y -= 500; // 向上平移500像素
//             _cardDecisionPoints.Add(point);
//         }
//
// // 黄点：相邻 midpoint（边界）
//         for (int i = 0; i < holders.Count - 1; i++)
//         {
//             var a = holders[i];
//             var b = holders[i + 1];
//             if (!GodotObject.IsInstanceValid(a) || !GodotObject.IsInstanceValid(b))
//             {
//                 continue;
//             }
//
//             Vector2 pa = HandDecisionPointUtil.GetDecisionPointGlobal(a);
//             Vector2 pb = HandDecisionPointUtil.GetDecisionPointGlobal(b);
//             Vector2 mid = (pa + pb) * 0.5f;
//             mid.Y -= 500; // 向上平移500像素
//             _midpoints.Add(mid);
//         }
//
//         QueueRedraw();
//
//         // ...后续逻辑不变
//     }
//
//     public static HandInsertDebugOverlayLive Ensure(Viewport vp)
//     {
//         if (vp == null)
//         {
//             return null;
//         }
//
//         if (_instance != null && GodotObject.IsInstanceValid(_instance))
//         {
//             return _instance;
//         }
//
//         var layer = new CanvasLayer
//         {
//             Name = "HandInsertDebugOverlayLiveLayer",
//             Layer = 999
//         };
//
//         var overlay = new HandInsertDebugOverlayLive
//         {
//             Name = "HandInsertDebugOverlayLive",
//             MouseFilter = MouseFilterEnum.Ignore,
//             AnchorLeft = 0,
//             AnchorTop = 0,
//             AnchorRight = 1,
//             AnchorBottom = 1,
//             OffsetLeft = 0,
//             OffsetTop = 0,
//             OffsetRight = 0,
//             OffsetBottom = 0,
//             ProcessMode = ProcessModeEnum.Always
//         };
//
//         layer.AddChild(overlay);
//         vp.GetTree()?.Root?.AddChild(layer);
//
//         _instance = overlay;
//         return overlay;
//     }
//
//     
//
//     public override void _Draw()
//     {
//         if (!Enabled)
//         {
//             return;
//         }
//
//         // 鼠标X竖线（白色半透明）
//         if (_hasMouseX)
//         {
//             DrawLine(new Vector2(_mouseX, 0), new Vector2(_mouseX, Size.Y), new Color(1, 1, 1, 0.35f), 2f);
//         }
//
//         // 红点：判定点（大一些）
//         foreach (var p in _cardDecisionPoints)
//         {
//             DrawCircle(p, 6f, new Color(1f, 0f, 0f, 0.9f));
//             DrawCircle(p, 2f, new Color(1f, 1f, 1f, 0.9f));
//         }
//
//         // 黄点：midpoint（边界点��
//         foreach (var m in _midpoints)
//         {
//             DrawCircle(m, 4f, new Color(1f, 1f, 0f, 0.85f));
//         }
//     }
// }
