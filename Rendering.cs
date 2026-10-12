using System;
using System.Collections.Generic;
using System.Numerics;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileNameplates.Core;
using ExileNameplates.Settings;
using ImGuiNET;
using Color = SharpDX.Color;
using RectangleF = SharpDX.RectangleF;

namespace ExileNameplates.Rendering;

internal static class DrawUtil
{
    private static readonly Vector2[] Offsets4 = { new(0, -1), new(-1, 0), new(1, 0), new(0, 1) };

    public static Color ToColor(this Vector4 v, float opacity = 1f) => new(v.X, v.Y, v.Z, v.W * opacity);

    public static void OutlinedText(Graphics graphics, string text, Vector2 pos, Color color, FontAlign align, bool outline)
    {
        if (outline)
            foreach (var o in Offsets4)
                graphics.DrawText(text, pos + o, Color.Black, align);
        graphics.DrawText(text, pos, color, align);
    }
}

public static class BarRenderer
{
    public static void Draw(Graphics graphics, in Snapshot s, TextureCache tex)
    {
        var b = s.Style.Bar;
        var op = b.Opacity;

        var total = s.HpMax + s.EsMax;
        var hpFrac = total > 0f ? Math.Clamp(s.HpCur, 0f, s.HpMax) / total : 0f;
        var esFrac = total > 0f ? Math.Clamp(s.EsCur, 0f, s.EsMax) / total : 0f;
        var manaFrac = s.ManaMax > 0f ? Math.Clamp(s.ManaCur / s.ManaMax, 0f, 1f) : 0f;

        var fullRect = new RectangleF(s.Pos.X, s.Pos.Y, b.Width, b.Height);
        var hpRect   = new RectangleF(s.Pos.X, s.Pos.Y, b.Width * hpFrac, b.Height);
        var esRect   = new RectangleF(s.Pos.X + b.Width * hpFrac, s.Pos.Y, b.Width * esFrac, b.Height);
        var manaH    = Math.Max(1.5f, b.Height * 0.22f);
        var manaRect = new RectangleF(s.Pos.X, s.Pos.Y + b.Height - manaH, b.Width * manaFrac, manaH);

        var trackName = b.Background.TrackTexture;
        if (tex.Get(trackName) != 0)
            graphics.DrawImage(trackName, fullRect, b.Background.Color.ToColor(b.Background.Opacity * op));

        if (b.Health.Enabled && tex.Get(b.Health.FillTexture) != 0 && hpFrac > 0f)
            graphics.DrawImage(b.Health.FillTexture, hpRect, b.Health.Color.ToColor(op));

        if (b.EnergyShield.Enabled && tex.Get(b.EnergyShield.FillTexture) != 0 && esFrac > 0f)
            graphics.DrawImage(b.EnergyShield.FillTexture, esRect, b.EnergyShield.Color.ToColor(op));

        if (b.Mana.Enabled && tex.Get(b.Mana.FillTexture) != 0 && manaFrac > 0f)
            graphics.DrawImage(b.Mana.FillTexture, manaRect, b.Mana.Color.ToColor(op));

        var dot = b.DotOverlay;
        if (dot.Enabled && s.DotLoss > 0f && total > 0f && tex.Get(dot.FillTexture) != 0)
        {
            var hpRight = s.Pos.X + b.Width * hpFrac;
            var lossW = b.Width * (Math.Min(s.DotLoss, s.HpCur) / total);
            graphics.DrawImage(dot.FillTexture, new RectangleF(hpRight - lossW, s.Pos.Y, lossW, b.Height), dot.Color.ToColor(op));
        }

        var fr = b.Frame;
        if (fr.Thickness > 0f)
            graphics.DrawFrame(fullRect, fr.Color.ToColor(op), fr.CornerSize, (int)fr.Thickness, 0);
    }
}

public static class BuffIconRenderer
{
    private static readonly RectangleF FullUv = new(0f, 0f, 1f, 1f);

    public static void Draw(Graphics graphics, in Snapshot s,
                            IReadOnlyList<ResolvedStatus> buffs, IReadOnlyList<ResolvedStatus> debuffs,
                            TextureCache tex, bool outline)
    {
        var b = s.Style.Bar;
        var bar = new RectangleF(s.Pos.X, s.Pos.Y, b.Width, b.Height);
        DrawBlock(graphics, s.Style.Buffs, buffs, bar, tex, outline);
        DrawBlock(graphics, s.Style.Debuffs, debuffs, bar, tex, outline);
    }

    public static (float Above, float Below) Extent(BuffBlock block, IReadOnlyList<ResolvedStatus> list, float barHeight)
    {
        var n = block.Enabled ? Math.Min(list.Count, block.MaxIcons) : 0;
        if (n == 0) return (0f, 0f);

        var cols = Math.Max(1, block.Columns);
        var rows = (n + cols - 1) / cols;
        var gh = block.Spacing * (rows - 1);
        for (int r = 0; r < rows; r++)
        {
            var h = 0f;
            for (int i = r * cols; i < Math.Min(n, (r + 1) * cols); i++)
                h = Math.Max(h, IconSize(block, list[i]));
            gh += h;
        }

        return block.Anchor switch
        {
            BlockAnchor.Above => (block.Spacing + gh, 0f),
            BlockAnchor.Below => (0f, block.Spacing + gh),
            _ => (0f, Math.Max(0f, gh - barHeight)),
        };
    }

    private static float IconSize(BuffBlock block, in ResolvedStatus st) => block.IconSize * (st.Scale <= 0f ? 1f : st.Scale);

    private static void DrawBlock(Graphics graphics, BuffBlock block,
                                  IReadOnlyList<ResolvedStatus> list, RectangleF bar, TextureCache tex, bool outline)
    {
        if (!block.Enabled || list.Count == 0) return;

        var n = Math.Min(list.Count, block.MaxIcons);
        var cols = Math.Max(1, block.Columns);
        var spacing = block.Spacing;
        var usedCols = Math.Min(n, cols);
        var rows = (n + cols - 1) / cols;

        Span<float> colWidth = stackalloc float[usedCols];
        Span<float> rowHeight = stackalloc float[rows];
        Span<float> colOffset = stackalloc float[usedCols];
        Span<float> rowOffset = stackalloc float[rows];
        for (int i = 0; i < n; i++)
        {
            var size = IconSize(block, list[i]);
            colWidth[i % cols] = Math.Max(colWidth[i % cols], size);
            rowHeight[i / cols] = Math.Max(rowHeight[i / cols], size);
        }
        for (int c = 1; c < usedCols; c++)
            colOffset[c] = colOffset[c - 1] + colWidth[c - 1] + spacing;
        for (int r = 1; r < rows; r++)
            rowOffset[r] = rowOffset[r - 1] + rowHeight[r - 1] + spacing;

        var gridW = colOffset[usedCols - 1] + colWidth[usedCols - 1];

        for (int i = 0; i < n; i++)
        {
            var st = list[i];
            var iconSize = IconSize(block, st);
            var cx = colOffset[i % cols];
            var ry = rowOffset[i / cols];

            var (x, y) = block.Anchor switch
            {
                BlockAnchor.Above => (bar.X + cx, bar.Y - spacing - ry - iconSize),
                BlockAnchor.Below => (bar.X + cx, bar.Y + bar.Height + spacing + ry),
                BlockAnchor.Left => (bar.X - gridW - spacing + cx, bar.Y + ry),
                _ => (bar.X + bar.Width + spacing + cx, bar.Y + ry),
            };
            var rect = new RectangleF(x, y, iconSize, iconSize);

            var (uv0, uv1) = StatusAtlas.GetUVPair(st.Icon);
            var uv = new RectangleF(uv0.X, uv0.Y, uv1.X - uv0.X, uv1.Y - uv0.Y);
            if (st.Style == IconStyle.IconOverBadge && tex.Get("Icons_Status_Badge_Gem") != 0)
                graphics.DrawImage("Icons_Status_Badge_Gem", rect, FullUv, st.BadgeTint.ToColor());
            var atlas = st.Style == IconStyle.IconBadge ? "Icons_Status_Gem" : "Icons_Status_Gem_Bare";
            if (tex.Get(atlas) != 0)
                graphics.DrawImage(atlas, rect, uv, st.Tint.ToColor());

            var showStacks = block.ShowStacks && st.Stacks > 1;
            var showDur = block.ShowDuration && st.Remaining > 0f;
            if (!showStacks && !showDur) continue;

            using (graphics.SetTextScale(Math.Clamp(rect.Height / 28f, 0.5f, 1f)))
            {
                if (showStacks)
                    DrawUtil.OutlinedText(graphics, st.Stacks.ToString(), new Vector2(rect.X + rect.Width, rect.Y), Color.White, FontAlign.Right, outline);
                if (showDur)
                    DrawUtil.OutlinedText(graphics, st.Remaining.ToString("0.0"), new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height), Color.White, FontAlign.Center, outline);
            }
        }
    }
}

public static class DataTextRenderer
{
    private enum Anchor { Above, OnBar, Below }

    public static void Draw(Graphics graphics, in Snapshot s, int nameAbbrevThreshold, bool outline)
    {
        var style = s.Style;
        if (!style.Above.Enabled && !style.OnBar.Enabled && !style.Below.Enabled) return;

        var name = NameAbbreviator.Abbreviate(s.Name, nameAbbrevThreshold);
        DrawSlot(graphics, style.Above, s, name, Anchor.Above, outline);
        DrawSlot(graphics, style.OnBar, s, name, Anchor.OnBar, outline);
        DrawSlot(graphics, style.Below, s, name, Anchor.Below, outline);
    }

    private static void DrawSlot(Graphics graphics, DataTextSlot slot, in Snapshot s, string name, Anchor anchor, bool outline)
    {
        if (!slot.Enabled) return;
        var text = slot.Compiled().Render(s, name);
        if (string.IsNullOrEmpty(text)) return;

        var bar = s.Style.Bar;
        var x = s.Pos.X + slot.Align switch
        {
            FontAlign.Left => 0f,
            FontAlign.Right => bar.Width,
            _ => bar.Width / 2f,
        };

        using (graphics.SetTextScale(slot.Scale))
        {
            var h = graphics.MeasureText(text).Y;
            var y = s.Pos.Y + anchor switch
            {
                Anchor.Above => -h,
                Anchor.Below => bar.Height,
                _ => (bar.Height - h) / 2f,
            };
            DrawUtil.OutlinedText(graphics, text, new Vector2(x + slot.Offset.X, y + slot.Offset.Y), slot.Color.ToColor(), slot.Align, outline);
        }
    }
}

public static class DebugOverlay
{
    private static readonly Color Green = new(90, 220, 90);
    private static readonly Color Red = new(235, 70, 70);
    private static readonly Color Yellow = new(235, 205, 70);
    private static readonly Color White = new(235, 235, 235);
    private static readonly Color BoxBg = new(0, 0, 0, 170);
    private static readonly Color BtnBg = new(40, 70, 120, 200);
    private static readonly Color BtnHover = new(70, 120, 190, 230);

    private const float Scale = 0.7f;
    private const float Pad = 3f;
    private const string BtnText = "[+ ignore]";

    private static readonly List<(string text, Color color)> Lines = new();

    public static void Draw(Graphics gfx, GameController gc, ExileNameplatesSettings s)
    {
        var cam = gc.IngameState.Camera;
        if (cam == null) return;
        var win = gc.Window.GetWindowRectangle();
        var player = gc.Player;
        var maxDist = s.Global.MaxDistance;

        var mouse = ImGui.GetMousePos();
        bool click = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        foreach (var type in EntityClassifier.Types)
        {
            if (!gc.EntityListWrapper.ValidEntitiesByType.TryGetValue(type, out var list)) continue;
            foreach (var e in list)
            {
                if (e == null) continue;
                e.TryGetComponent<Render>(out var render);
                var world = e.PosNum;
                if (render != null) world.Z -= 2f * render.BoundsNum.Z;
                var screen = cam.WorldToScreen(world);
                if (screen.X < win.Left || screen.X > win.Right || screen.Y < win.Top || screen.Y > win.Bottom)
                    continue;

                var dist = e.DistancePlayer;
                var liveBucket = EntityClassifier.Classify(e, player);
                var np = e.GetHudComponent<Nameplate>();
                var cached = np is { HasResolved: true };

                Lines.Clear();
                Lines.Add(($"valid {e.IsValid}", e.IsValid ? Green : Red));
                Lines.Add(($"alive {e.IsAlive}", e.IsAlive ? Green : Red));
                Lines.Add(($"target {e.IsTargetable}", e.IsTargetable ? Green : Yellow));
                Lines.Add(($"dist {dist:F0}/{maxDist:F0}", dist <= maxDist ? Green : Red));
                Lines.Add(Cmp("hostile", e.IsHostile, cached ? np.CachedHostile : null));
                Lines.Add(Cmp("type", e.Type, cached ? np.CachedType : null));
                Lines.Add(Cmp("rarity", e.Rarity, cached ? np.CachedRarity : null));
                Lines.Add(Cmp("bucket", liveBucket, cached ? np.Bucket : null));
                Lines.Add(Cmp("show", s.Buckets.For(liveBucket).Show, cached ? np.Resolved.Show : null));

                if (DrawBlock(gfx, screen.X, screen.Y, mouse) && click)
                {
                    click = false;
                    var key = PathUtil.KeyOf(e);
                    if (key != "" && !s.IgnoredPaths.Contains(key))
                    {
                        s.IgnoredPaths.Add(key);
                        s.SettingsVersion++;
                    }
                }
            }
        }
    }

    private static (string, Color) Cmp<T>(string label, T live, T? cachedVal) where T : struct
    {
        if (cachedVal == null) return ($"{label} {live}", White);
        bool same = EqualityComparer<T>.Default.Equals(live, cachedVal.Value);
        return (same ? $"{label} {live}" : $"{label} {live} (cache {cachedVal.Value})", same ? Green : Red);
    }

    private static bool DrawBlock(Graphics gfx, float cx, float headY, Vector2 mouse)
    {
        using (gfx.SetTextScale(Scale))
        {
            float w = 0f, h = 0f;
            foreach (var line in Lines)
            {
                var size = gfx.MeasureText(line.text);
                w = Math.Max(w, size.X);
                h += size.Y;
            }

            float left = cx - w / 2f - Pad;
            float top = headY - h - Pad * 2f - 4f;

            var bs = gfx.MeasureText(BtnText);
            float btnW = bs.X + Pad * 2f, btnH = bs.Y + Pad * 2f;
            float btnLeft = cx - btnW / 2f;
            float btnTop = top - btnH - 2f;
            bool hover = mouse.X >= btnLeft && mouse.X <= btnLeft + btnW &&
                         mouse.Y >= btnTop && mouse.Y <= btnTop + btnH;
            gfx.DrawBox(new RectangleF(btnLeft, btnTop, btnW, btnH), hover ? BtnHover : BtnBg);
            gfx.DrawText(BtnText, new Vector2(btnLeft + Pad, btnTop + Pad), White);

            gfx.DrawBox(new RectangleF(left, top, w + Pad * 2f, h + Pad * 2f), BoxBg);
            float y = top + Pad;
            foreach (var line in Lines)
            {
                gfx.DrawText(line.text, new Vector2(left + Pad, y), line.color);
                y += gfx.MeasureText(line.text).Y;
            }

            return hover;
        }
    }
}

public static class PreviewOverlay
{
    private static readonly Bucket[] AllBuckets = Enum.GetValues<Bucket>();

    public static void Draw(Graphics graphics, ExileNameplatesSettings s, TextureCache tex, bool moveMode)
    {
        foreach (var b in AllBuckets)
        {
            var style = s.Buckets.For(b);
            if (!style.PreviewEnabled) continue;
            var pos = style.PreviewPos;
            if (moveMode)
            {
                DraggableOverlay.Drag($"prev_{b}", ref pos, new Vector2(style.Bar.Width * 3f, style.Bar.Height * 3f));
                style.PreviewPos = pos;
            }

            var preview = style.Clone();
            preview.Bar.Width *= 3f;
            preview.Bar.Height *= 3f;
            preview.Bar.Frame.CornerSize *= 3f;
            BarRenderer.Draw(graphics, new Snapshot
            {
                Pos = pos, Style = preview,
                HpCur = 65, HpMax = 100, EsCur = 40, EsMax = 100, ManaCur = 70, ManaMax = 100,
            }, tex);
        }
    }
}

public static class UnitFrameRenderer
{
    private static readonly List<ResolvedStatus> Empty = new();

    private static readonly List<FrameDraw> Items = new();
    private static readonly List<float> Above = new();
    private static readonly List<float> Height = new();

    public static void Draw(Graphics graphics, GameController gc, ExileNameplatesSettings s,
                            UnitFrameEngine engine, TextureCache tex, bool moveMode)
    {
        var grp = s.Frames.Group;

        Items.Clear();
        Items.AddRange(engine.Draws);
        if (moveMode)
        {
            foreach (var slot in UnitFrameEngine.Slots)
            {
                var style = s.Frames.For(slot);
                if (!style.Enabled || engine.CountFor(slot) > 0) continue;
                var name = UnitFrameEngine.SlotNames[(int)slot];
                Items.Add(new FrameDraw
                {
                    Slot = slot,
                    Style = style,
                    Snap = new Snapshot
                    {
                        Style = style, Name = name, Rarity = "", Type = name,
                        HpCur = 65, HpMax = 100, EsCur = 40, EsMax = 100, ManaCur = 70, ManaMax = 100,
                    },
                });
            }
        }
        if (Items.Count == 0) return;

        Above.Clear();
        Height.Clear();
        var stackH = 0f;
        var maxW = 0f;
        foreach (var it in Items)
        {
            var style = it.Style;
            var barH = style.Bar.Height;
            float textAbove = 0f, textBelow = 0f;
            if (style.Above.Enabled)
                using (graphics.SetTextScale(style.Above.Scale)) textAbove = graphics.MeasureText("Ay").Y;
            if (style.Below.Enabled)
                using (graphics.SetTextScale(style.Below.Scale)) textBelow = graphics.MeasureText("Ay").Y;
            var (ba, bb) = BuffIconRenderer.Extent(style.Buffs, it.Buffs ?? Empty, barH);
            var (da, db) = BuffIconRenderer.Extent(style.Debuffs, it.Debuffs ?? Empty, barH);

            var above = Math.Max(textAbove, Math.Max(ba, da));
            var height = above + barH + Math.Max(textBelow, Math.Max(bb, db));
            Above.Add(above);
            Height.Add(height);
            stackH += height;
            maxW = Math.Max(maxW, style.Bar.Width);
        }
        stackH += grp.Spacing * (Items.Count - 1);

        var top = grp.VGrow == FrameVGrow.Up ? grp.Pos.Y - stackH : grp.Pos.Y;

        if (moveMode)
        {
            var box = new Vector2(AlignLeft(grp, maxW), top);
            var before = box;
            DraggableOverlay.Drag("uf_group", ref box, new Vector2(maxW, stackH));
            if (box != before)
            {
                grp.Pos += box - before;
                top += box.Y - before.Y;
            }
        }
        else if (grp.HideBehindPanels)
        {
            var box = new RectangleF(AlignLeft(grp, maxW), top, maxW, stackH);
            var ui = gc.IngameState.IngameUi;
            if (Overlaps(ui.OpenLeftPanel, box) || Overlaps(ui.OpenRightPanel, box)) return;
            var large = ui.LargePanels;
            if (large != null)
                foreach (var panel in large)
                    if (Overlaps(panel, box)) return;
        }

        var y = top;
        for (int step = 0; step < Items.Count; step++)
        {
            var k = grp.VGrow == FrameVGrow.Up ? Items.Count - 1 - step : step;
            var it = Items[k];

            var snap = it.Snap;
            snap.Pos = new Vector2(AlignLeft(grp, it.Style.Bar.Width), y + Above[k]);
            y += Height[k] + grp.Spacing;

            BarRenderer.Draw(graphics, snap, tex);
            DataTextRenderer.Draw(graphics, snap, s.Global.NameAbbrevThreshold, s.Global.TextOutline);
            if (it.Buffs != null)
                BuffIconRenderer.Draw(graphics, snap, it.Buffs, it.Debuffs, tex, s.Global.TextOutline);
        }
    }

    private static bool Overlaps(Element e, RectangleF box)
    {
        if (e == null || !e.IsVisible) return false;
        var r = e.GetClientRectCache;
        if (r.Width <= 0f || r.Height <= 0f) return false;
        return r.Left < box.Right && r.Right > box.Left && r.Top < box.Bottom && r.Bottom > box.Top;
    }

    private static float AlignLeft(UnitFrameGroup grp, float width) => grp.HAlign switch
    {
        FrameHAlign.Center => grp.Pos.X - width / 2f,
        FrameHAlign.Right => grp.Pos.X - width,
        _ => grp.Pos.X,
    };
}
