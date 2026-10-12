using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ExileCore;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileNameplates.Core;
using ImGuiNET;
using Newtonsoft.Json;

namespace ExileNameplates.Settings;

public class ExileNameplatesSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(true);

    public HotkeyNodeV2 ToggleMoveMode { get; set; } = new(Keys.None);
    public HotkeyNodeV2 TogglePerfOverlay { get; set; } = new(Keys.F11);

    public GlobalSettings Global { get; set; } = new();
    public Buckets Buckets { get; set; } = new();
    public UnitFrames Frames { get; set; } = new();
    public List<StyleOverride> Overrides { get; set; } = new();
    public PerfSettings Perf { get; set; } = new();

    public List<ConfiguredStatus> Statuses { get; set; } = new()
    {
        new() { Key = "cataclysm_sigil", Label = "Armageddon Brand", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Adrenaline, Tint = new(1f, 0f, 0f, 1f), Style = IconStyle.IconOverBadge, Scale = 1.996f },
        new() { Key = "curse_fire_weakness", Label = "Flammability", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Ignite, Tint = new(0.99526066f, 0.62262744f, 0f, 1f), Style = IconStyle.IconOverBadge, Scale = 1f },
        new() { Key = "bleeding", Label = "Bleeding", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Bleeding, Tint = new(0.90047395f, 0.042676505f, 0.042676505f, 1f), Style = IconStyle.IconOnly, Scale = 1f },
        new() { Key = "ignited", Label = "Ignited", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Ignite, Tint = new(1f, 0.54028434f, 0f, 1f), Style = IconStyle.IconOnly, Scale = 1f },
        new() { Key = "frozen", Label = "Frozen", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Frozen, Tint = new(0f, 0.7582941f, 1f, 1f), Style = IconStyle.IconOnly, Scale = 1f },
        new() { Key = "shocked", Label = "Shocked", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Shocked, Tint = new(1f, 0.9383886f, 0f, 1f), Style = IconStyle.IconOnly, Scale = 1f },
        new() { Key = "chilled", Label = "Chilled", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Chilled, Tint = new(0.15044588f, 0.7443444f, 0.9336493f, 1f), Style = IconStyle.IconOnly, Scale = 1f },
        new() { Key = "poison", Label = "Poisoned", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Poisoned, Tint = new(0.16845895f, 0.97630334f, 0.11104873f, 1f), Style = IconStyle.IconOnly, Scale = 1f },
        new() { Key = "scorched_earth_degen_explosion", Label = "Burning", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Ignite, Tint = new(1f, 0f, 0f, 1f), Style = IconStyle.IconOnly, Scale = 1f },
        new() { Key = "curse_newpunishment", Label = "Punishment", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Vulnerable, Tint = new(0.86255926f, 0.8592075f, 0.15534241f, 1f), Style = IconStyle.IconOverBadge, Scale = 1.5f },
        new() { Key = "fire_beam_damage_stacks", Label = "Scorching Ray", Category = StatusCategory.Debuff,
                Icon = StatusIcon.Scorched, Tint = new(1f, 0.4493392f, 0f, 1f), Style = IconStyle.IconOverBadge, Scale = 2f },
        new() { Key = "flame_link_source", Label = "Flame Link", Category = StatusCategory.Buff,
                Icon = StatusIcon.Burning, Tint = new(1f, 0.63436127f, 0.63436127f, 1f), Style = IconStyle.IconBadge, Scale = 1f },
        new() { Key = "flame_link_target", Label = "Flame Link", Category = StatusCategory.Buff,
                Icon = StatusIcon.Burning, Tint = new(1f, 0.57268727f, 0.57268727f, 1f), Style = IconStyle.IconBadge, Scale = 1f },
        new() { Key = "cover_in_ash", Label = "Covered In Ash", Category = StatusCategory.Debuff,
                Icon = StatusIcon.BurningGround, Tint = new(1f, 0.55506605f, 0f, 1f), Style = IconStyle.IconOverBadge, Scale = 1.218f },
    };

    public StatesSettings States { get; set; } = new();
    public List<string> IgnoredPaths { get; set; } = new();

    public int SettingsVersion { get; set; } = 0;
}

public class GlobalSettings
{
    public float DrawDistanceLimit { get; set; } = 130f;
    public float DetailDistance { get; set; } = 80f;
    public bool ShowInTown { get; set; } = false;
    public bool ShowInHideout { get; set; } = false;
    public float OffScreenMargin { get; set; } = 80f;
    public bool PerformanceMode { get; set; } = false;
    public int NameAbbrevThreshold { get; set; } = 10;
    public bool TextOutline { get; set; } = true;
    public bool HideUntargetable { get; set; } = true;
    public bool ShowDebug { get; set; } = false;
    public DotDurations DotDurations { get; set; } = new();
    public float DpsWindow { get; set; } = 1.0f;

    [JsonIgnore] public float MaxDistance => PerformanceMode ? DetailDistance : DrawDistanceLimit;
}

public class DotDurations
{
    public float PoisonSeconds { get; set; } = 2f;
    public float IgniteSeconds { get; set; } = 4f;
    public float BleedSeconds { get; set; } = 5f;
    public float CausticSeconds { get; set; } = 4f;
    public float DecaySeconds { get; set; } = 8f;
    public float FallbackSeconds { get; set; } = 3f;
}

public class Buckets
{
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Gray = new(0.8f, 0.8f, 0.8f, 1f);

    private static NameplateStyle Plate(float w, float h, Vector4 nameColor, float nameScale, string below,
                                        bool show = true, bool onBar = true, bool buffStacks = false,
                                        bool debuffStacks = true, float debuffIconSize = 24f) => new()
    {
        Show = show,
        Bar = new BarStyle
        {
            Width = w, Height = h,
            Health = new VitalLayer(true, new Vector4(0.85f, 0.24f, 0.20f, 1f), "fill_bevel"),
            EnergyShield = new VitalLayer(true, new Vector4(0.37f, 0.82f, 0.90f, 1f), "fill_gloss"),
            Mana = new VitalLayer(false, new Vector4(0.30f, 0.45f, 0.85f, 1f), "fill_flat"),
            DotOverlay = new DotOverlayStyle { FillTexture = "fill_gradient" },
        },
        Above = new("{name}", nameColor, nameScale) { Enabled = true },
        OnBar = new("{hp%}%", White, 0.9f) { Enabled = onBar, Align = FontAlign.Right },
        Below = new(below, Gray, 1f) { Enabled = below != "", Align = FontAlign.Right },
        Buffs = new() { Enabled = true, Anchor = BlockAnchor.Above, IconSize = 24f, ShowStacks = buffStacks },
        Debuffs = new() { Enabled = true, Anchor = BlockAnchor.Below, IconSize = debuffIconSize, MaxIcons = 5, ShowStacks = debuffStacks },
    };

    public NameplateStyle Normal { get; set; } = Plate(100, 12, White, 1f, "", onBar: false);
    public NameplateStyle Magic { get; set; } = Plate(120, 14, new(0.535545f, 0.6896297f, 1f, 1f), 1f, "{xp%}%");
    public NameplateStyle Rare { get; set; } = Plate(140, 15, new(1f, 0.8815166f, 0f, 1f), 1.2f, "{dps}", buffStacks: true);
    public NameplateStyle Unique { get; set; } = Plate(160, 16, new(1f, 0.5971564f, 0f, 1f), 1.5f, "{dps}", debuffIconSize: 24.613f);
    public NameplateStyle Ally { get; set; } = Plate(160, 16, White, 1.5f, "{xp%}%", debuffStacks: false);
    public NameplateStyle Minion { get; set; } = Plate(160, 16, White, 1.5f, "{xp%}%", show: false, debuffStacks: false);
    public NameplateStyle Npc { get; set; } = Plate(160, 16, White, 1.5f, "{xp%}%", show: false, debuffStacks: false);
    public NameplateStyle Self { get; set; } = Plate(160, 16, White, 1.5f, "{xp%}%", show: false, debuffStacks: false);

    public NameplateStyle For(Bucket b) => b switch
    {
        Bucket.Normal => Normal,
        Bucket.Magic => Magic,
        Bucket.Rare => Rare,
        Bucket.Unique => Unique,
        Bucket.Ally => Ally,
        Bucket.Minion => Minion,
        Bucket.Npc => Npc,
        _ => Self,
    };
}

public class NameplateStyle : IFrameStyle
{
    public bool Show { get; set; } = true;
    public BarStyle Bar { get; set; } = new();
    public Vector2 PreviewPos { get; set; } = new(600, 400);
    public bool PreviewEnabled { get; set; } = false;
    public DataTextSlot Above { get; set; } = new("{name}", new Vector4(1, 1, 1, 1), 1f);
    public DataTextSlot OnBar { get; set; } = new("{hp%}", new Vector4(1, 1, 1, 1), 1f);
    public DataTextSlot Below { get; set; } = new("{dist}m", new Vector4(0.8f, 0.8f, 0.8f, 1f), 0.9f);
    public BuffBlock Buffs { get; set; } = new() { Anchor = BlockAnchor.Above };
    public BuffBlock Debuffs { get; set; } = new() { Anchor = BlockAnchor.Below };

    public NameplateStyle Clone() => new()
    {
        Show = Show,
        Bar = Bar.Clone(),
        PreviewPos = PreviewPos,
        PreviewEnabled = PreviewEnabled,
        Above = Above.Clone(),
        OnBar = OnBar.Clone(),
        Below = Below.Clone(),
        Buffs = Buffs.Clone(),
        Debuffs = Debuffs.Clone(),
    };

    public void CopyFrom(NameplateStyle s)
    {
        Show = s.Show;
        Bar.CopyFrom(s.Bar);
        PreviewPos = s.PreviewPos;
        PreviewEnabled = s.PreviewEnabled;
        Above.CopyFrom(s.Above);
        OnBar.CopyFrom(s.OnBar);
        Below.CopyFrom(s.Below);
        Buffs.CopyFrom(s.Buffs);
        Debuffs.CopyFrom(s.Debuffs);
    }
}

public class BackgroundStyle
{
    public string TrackTexture { get; set; } = "bg_track";
    public Vector4 Color { get; set; } = new(0, 0, 0, 0.6f);
    public float Opacity { get; set; } = 1f;

    public BackgroundStyle Clone() => new() { TrackTexture = TrackTexture, Color = Color, Opacity = Opacity };

    public void CopyFrom(BackgroundStyle s)
    { TrackTexture = s.TrackTexture; Color = s.Color; Opacity = s.Opacity; }
}

public class FrameStyle
{
    public Vector4 Color { get; set; } = new(0, 0, 0, 1);
    public float CornerSize { get; set; } = 4f;
    public float Thickness { get; set; } = 2f;

    public FrameStyle Clone() => new() { Color = Color, CornerSize = CornerSize, Thickness = Thickness };

    public void CopyFrom(FrameStyle s)
    { Color = s.Color; CornerSize = s.CornerSize; Thickness = s.Thickness; }
}

public class BarStyle
{
    public float Width { get; set; } = 100f;
    public float Height { get; set; } = 14f;
    public float YOffset { get; set; } = 0f;
    public float Opacity { get; set; } = 1f;

    public VitalLayer Health { get; set; } = new(true, new Vector4(0.85f, 0.24f, 0.20f, 1f), "fill_flat");
    public VitalLayer EnergyShield { get; set; } = new(true, new Vector4(0.37f, 0.82f, 0.90f, 1f), "fill_flat");
    public VitalLayer Mana { get; set; } = new(false, new Vector4(0.30f, 0.45f, 0.85f, 1f), "fill_flat");
    public DotOverlayStyle DotOverlay { get; set; } = new();

    public BackgroundStyle Background { get; set; } = new();
    public FrameStyle Frame { get; set; } = new();

    public BarStyle Clone() => new()
    {
        Width = Width, Height = Height, YOffset = YOffset, Opacity = Opacity,
        Health = Health.Clone(),
        EnergyShield = EnergyShield.Clone(),
        Mana = Mana.Clone(),
        DotOverlay = DotOverlay.Clone(),
        Background = Background.Clone(),
        Frame = Frame.Clone(),
    };

    public void CopyFrom(BarStyle s)
    {
        Width = s.Width; Height = s.Height; YOffset = s.YOffset; Opacity = s.Opacity;
        Health.CopyFrom(s.Health);
        EnergyShield.CopyFrom(s.EnergyShield);
        Mana.CopyFrom(s.Mana);
        DotOverlay.CopyFrom(s.DotOverlay);
        Background.CopyFrom(s.Background);
        Frame.CopyFrom(s.Frame);
    }
}

public class VitalLayer
{
    public bool Enabled { get; set; } = true;
    public Vector4 Color { get; set; } = new(1, 1, 1, 1);
    public string FillTexture { get; set; } = "fill_flat";

    public VitalLayer() { }
    public VitalLayer(bool enabled, Vector4 color, string fill)
    { Enabled = enabled; Color = color; FillTexture = fill; }

    public VitalLayer Clone() => new(Enabled, Color, FillTexture);

    public void CopyFrom(VitalLayer s)
    { Enabled = s.Enabled; Color = s.Color; FillTexture = s.FillTexture; }
}

public class DotOverlayStyle
{
    public bool Enabled { get; set; } = true;
    public Vector4 Color { get; set; } = new(0f, 0f, 0f, 0.5f);
    public string FillTexture { get; set; } = "fill_flat";

    public DotOverlayStyle Clone() => new() { Enabled = Enabled, Color = Color, FillTexture = FillTexture };

    public void CopyFrom(DotOverlayStyle s)
    { Enabled = s.Enabled; Color = s.Color; FillTexture = s.FillTexture; }
}

public class DataTextSlot
{
    public bool Enabled { get; set; } = false;
    public string Template { get; set; } = "";
    public FontAlign Align { get; set; } = FontAlign.Center;
    public Vector4 Color { get; set; } = new(1, 1, 1, 1);
    public float Scale { get; set; } = 1f;
    public Vector2 Offset { get; set; } = new(0, 0);

    public DataTextSlot() { }
    public DataTextSlot(string template, Vector4 color, float scale)
    { Template = template; Color = color; Scale = scale; }

    public DataTextSlot Clone() => new()
    {
        Enabled = Enabled, Template = Template, Align = Align,
        Color = Color, Scale = Scale, Offset = Offset,
    };

    public void CopyFrom(DataTextSlot s)
    {
        Enabled = s.Enabled; Template = s.Template; Align = s.Align;
        Color = s.Color; Scale = s.Scale; Offset = s.Offset;
    }

    [JsonIgnore] private CompiledTemplate _compiled;
    [JsonIgnore] private string _compiledSrc;

    public CompiledTemplate Compiled()
    {
        if (_compiled == null || _compiledSrc != Template)
        {
            _compiledSrc = Template;
            _compiled = CompiledTemplate.Parse(Template);
        }
        return _compiled;
    }
}

public class BuffBlock
{
    public bool Enabled { get; set; } = false;
    public BlockAnchor Anchor { get; set; } = BlockAnchor.Above;
    public float IconSize { get; set; } = 18f;
    public float Spacing { get; set; } = 2f;
    public int Columns { get; set; } = 6;
    public int MaxIcons { get; set; } = 12;
    public bool ShowStacks { get; set; } = true;
    public bool ShowDuration { get; set; } = false;

    public BuffBlock Clone() => new()
    {
        Enabled = Enabled, Anchor = Anchor, IconSize = IconSize, Spacing = Spacing,
        Columns = Columns, MaxIcons = MaxIcons, ShowStacks = ShowStacks, ShowDuration = ShowDuration,
    };

    public void CopyFrom(BuffBlock s)
    {
        Enabled = s.Enabled; Anchor = s.Anchor; IconSize = s.IconSize; Spacing = s.Spacing;
        Columns = s.Columns; MaxIcons = s.MaxIcons; ShowStacks = s.ShowStacks; ShowDuration = s.ShowDuration;
    }
}

public interface IFrameStyle
{
    BarStyle Bar { get; }
    DataTextSlot Above { get; }
    DataTextSlot OnBar { get; }
    DataTextSlot Below { get; }
    BuffBlock Buffs { get; }
    BuffBlock Debuffs { get; }
}

public class ConfiguredStatus
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public StatusCategory Category { get; set; } = StatusCategory.Debuff;
    public StatusIcon Icon { get; set; }
    public Vector4 Tint { get; set; } = new(1, 1, 1, 1);
    public IconStyle Style { get; set; } = IconStyle.IconBadge;
    public Vector4 BadgeTint { get; set; } = new(0.1f, 0.1f, 0.1f, 1f);
    public float Scale { get; set; } = 1f;

    public StylePatch Patch { get; set; } = new();
}

public class StyleOverride
{
    public string Name { get; set; } = "New override";
    public bool Enabled { get; set; } = true;
    public MatchKind Match { get; set; } = MatchKind.Path;
    public string Pattern { get; set; } = "";
    public StylePatch Style { get; set; } = new();

    private Regex _rx;
    private string _rxSrc;

    private Regex Rx
    {
        get
        {
            if (_rxSrc != Pattern)
            {
                _rxSrc = Pattern;
                try { _rx = string.IsNullOrEmpty(Pattern) ? null : new Regex(Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase); }
                catch { _rx = null; }
            }
            return _rx;
        }
    }

    [JsonIgnore] public bool IsInvalid => !string.IsNullOrEmpty(Pattern) && Rx == null;

    public bool IsMatch(string entityName, string entityPath)
    {
        if (!Enabled || Rx == null) return false;
        var target = Match == MatchKind.Name ? entityName : entityPath;
        return target != null && Rx.IsMatch(target);
    }
}

public class StatesSettings
{
    public StateStyle Invuln { get; set; } = new() { Enabled = true };
    public StateStyle Hidden { get; set; } = new() { Enabled = true, Suppress = true };
    public CullableStateStyle Cullable { get; set; } = new();
}

public class StateStyle
{
    public bool Enabled { get; set; } = false;
    public bool Suppress { get; set; } = false;
    public StylePatch Patch { get; set; } = new();
}

public sealed class CullableStateStyle : StateStyle
{
    public float Multiplier { get; set; } = 1f;
    public float Normal { get; set; } = 0.30f;
    public float Magic { get; set; } = 0.20f;
    public float Rare { get; set; } = 0.10f;
    public float Unique { get; set; } = 0.05f;

    public bool IsCullable(Bucket bucket, float hpPercent)
    {
        var baseT = bucket switch
        {
            Bucket.Normal => Normal,
            Bucket.Magic => Magic,
            Bucket.Rare => Rare,
            Bucket.Unique => Unique,
            _ => 0f,
        };
        if (baseT <= 0f) return false;
        return hpPercent <= baseT * Multiplier;
    }
}

public class PerfSettings
{
    public bool ProfilerEnabled { get; set; } = true;
    public Vector2 OverlayPos { get; set; } = new(20, 200);
}

public class UnitFrames
{
    public UnitFrameGroup Group { get; set; } = new();

    public UnitFrameStyle Player { get; set; } = new();
    public UnitFrameStyle Mercenary { get; set; } = new()
    {
        Buffs = new() { Enabled = true, Anchor = BlockAnchor.Left, IconSize = 28f },
        Debuffs = new() { Enabled = true, Anchor = BlockAnchor.Below },
        Statuses = new() { new() { Key = "flame_link_source" }, new() { Key = "flame_link_target" } },
    };
    public UnitFrameStyle AnimatedGuardian { get; set; } = new()
    {
        Buffs = new() { Enabled = true, Anchor = BlockAnchor.Left, IconSize = 28f },
        Debuffs = new() { Enabled = true, Anchor = BlockAnchor.Above },
        Statuses = new() { new() { Key = "flame_link_target" }, new() { Key = "flame_link_source" } },
    };
    public UnitFrameStyle Spectre { get; set; } = new()
    {
        Buffs = new() { Enabled = true, Anchor = BlockAnchor.Left, IconSize = 28f },
        Debuffs = new() { Enabled = true, Anchor = BlockAnchor.Left },
        Statuses = new() { new() { Key = "flame_link_target" }, new() { Key = "flame_link_source" } },
    };

    public int MaxSpectreFrames { get; set; } = 6;

    public UnitFrameStyle For(UnitSlot s) => s switch
    {
        UnitSlot.Mercenary => Mercenary,
        UnitSlot.AnimatedGuardian => AnimatedGuardian,
        UnitSlot.Spectre => Spectre,
        _ => Player,
    };
}

public class UnitFrameGroup
{
    public Vector2 Pos { get; set; } = new(885, 949);
    public FrameHAlign HAlign { get; set; } = FrameHAlign.Left;
    public FrameVGrow VGrow { get; set; } = FrameVGrow.Down;
    public float Spacing { get; set; } = 6f;

    public bool HideBehindPanels { get; set; } = true;
}

public class FrameStatus
{
    public string Key { get; set; } = "";
    public StatusIcon? Icon { get; set; }
    public Vector4? Tint { get; set; }
    public float? Scale { get; set; }
    public StatusCategory? Category { get; set; }
}

public class UnitFrameStyle : IFrameStyle
{
    public bool Enabled { get; set; } = true;

    public BarStyle Bar { get; set; } = new();
    public DataTextSlot Above { get; set; } = new("{name}", new Vector4(1, 1, 1, 1), 1f) { Enabled = true, Align = FontAlign.Left };
    public DataTextSlot OnBar { get; set; } = new("{hp}/{hpmax}", new Vector4(1, 1, 1, 1), 0.9f) { Enabled = true };
    public DataTextSlot Below { get; set; } = new("{es} ES", new Vector4(0.8f, 0.8f, 0.8f, 1f), 0.85f) { Align = FontAlign.Right };
    public BuffBlock Buffs { get; set; } = new() { Anchor = BlockAnchor.Below, Enabled = true };
    public BuffBlock Debuffs { get; set; } = new() { Anchor = BlockAnchor.Above, Enabled = true };

    public List<FrameStatus> Statuses { get; set; } = new();

    public UnitFrameStyle()
    {
        Bar.Width = 220f;
        Bar.Height = 20f;
        Bar.Mana.Enabled = true;
    }
}

public static class SettingsPanel
{
    public static void Draw(ExileNameplates p)
    {
        var changed = false;
        if (ImGui.BeginTabBar("##np_settings"))
        {
            if (ImGui.BeginTabItem("General"))     { changed |= GeneralTab.Draw(p);     ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Bars"))         { changed |= BarsTab.Draw(p);         ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Unit Frames")) { changed |= UnitFramesTab.Draw(p);  ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Statuses"))    { changed |= StatusesTab.Draw(p);    ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Overrides"))   { changed |= OverridesTab.Draw(p);   ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("States"))      { changed |= StatesTab.Draw(p);      ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Ignore"))      { changed |= IgnoreTab.Draw(p);      ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Performance")) { changed |= PerformanceTab.Draw(p); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
        if (changed) p.Settings.SettingsVersion++;
    }
}

public static class GeneralTab
{
    public static bool Draw(ExileNameplates p)
    {
        var s = p.Settings.Global;
        var changed = false;

        var f = s.DrawDistanceLimit;
        if (StyleControls.SliderFloat("Draw distance", ref f, 20, 300, "Cull nameplates beyond this range")) { s.DrawDistanceLimit = f; changed = true; }

        var d = s.DetailDistance;
        if (StyleControls.SliderFloat("Detail distance", ref d, 10, 200, "Draw distance used instead while Performance mode is on")) { s.DetailDistance = d; changed = true; }

        var m = s.OffScreenMargin;
        if (StyleControls.SliderFloat("Off-screen margin", ref m, 0, 300, "Pixels past screen edge before culling")) { s.OffScreenMargin = m; changed = true; }

        var t = s.ShowInTown;
        if (StyleControls.Toggle("Show in town", ref t)) { s.ShowInTown = t; changed = true; }

        var h = s.ShowInHideout;
        if (StyleControls.Toggle("Show in hideout", ref h)) { s.ShowInHideout = h; changed = true; }

        var pm = s.PerformanceMode;
        if (StyleControls.Toggle("Performance mode", ref pm, "Pull bars and datatexts in to Detail distance")) { s.PerformanceMode = pm; changed = true; }

        var nat = s.NameAbbrevThreshold;
        if (ImGui.SliderInt("Name abbrev threshold", ref nat, 0, 40)) { s.NameAbbrevThreshold = nat; changed = true; }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Abbreviate names longer than this; 0 = always");

        var ol = s.TextOutline;
        if (StyleControls.Toggle("Draw outlined text", ref ol, "1px black outline behind datatext, buff stacks, and durations for readability")) { s.TextOutline = ol; changed = true; }

        var hu = s.HideUntargetable;
        if (StyleControls.Toggle("Hide untargetable", ref hu, "Skip decorative/inactive entities like turrets and dormant objects (IsTargetable = false)")) { s.HideUntargetable = hu; changed = true; }

        var dbg = s.ShowDebug;
        if (StyleControls.Toggle("Debug overlay", ref dbg, "Draw live show/hide determinants above every entity; red = live value diverged from our cached one")) { s.ShowDebug = dbg; changed = true; }

        var dw = s.DpsWindow;
        if (StyleControls.SliderFloat("DPS window", ref dw, 0.25f, 5f, "{dps} smoothing window in seconds; lower = twitchier, higher = steadier")) { s.DpsWindow = dw; changed = true; }

        ImGui.Separator();
        ImGui.Text("DoT durations (s)");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fallback DoT durations used when the game does not expose a buff's remaining time (e.g. poison)");
        var dd = s.DotDurations;
        var dp = dd.PoisonSeconds;
        if (StyleControls.SliderFloat("Poison", ref dp, 0.5f, 20f, null)) { dd.PoisonSeconds = dp; changed = true; }
        var di = dd.IgniteSeconds;
        if (StyleControls.SliderFloat("Ignite", ref di, 0.5f, 20f, null)) { dd.IgniteSeconds = di; changed = true; }
        var db = dd.BleedSeconds;
        if (StyleControls.SliderFloat("Bleed", ref db, 0.5f, 20f, null)) { dd.BleedSeconds = db; changed = true; }
        var dc = dd.CausticSeconds;
        if (StyleControls.SliderFloat("Caustic", ref dc, 0.5f, 20f, null)) { dd.CausticSeconds = dc; changed = true; }
        var de = dd.DecaySeconds;
        if (StyleControls.SliderFloat("Decay", ref de, 0.5f, 20f, null)) { dd.DecaySeconds = de; changed = true; }
        var df = dd.FallbackSeconds;
        if (StyleControls.SliderFloat("Fallback", ref df, 0.5f, 20f, null)) { dd.FallbackSeconds = df; changed = true; }

        return changed;
    }
}

public static class BarsTab
{
    public static bool Draw(ExileNameplates p)
    {
        var changed = false;

        StyleControls.PushTabColors();
        if (ImGui.BeginTabBar("##np_unittype"))
        {
            foreach (Bucket b in Enum.GetValues<Bucket>())
            {
                if (ImGui.BeginTabItem($"{b}##ut_{b}"))
                {
                    changed |= DrawUnitType(p, b);
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
        ImGui.PopStyleColor(3);

        return changed;
    }

    private static bool DrawUnitType(ExileNameplates p, Bucket b)
    {
        var changed = false;
        var style = p.Settings.Buckets.For(b);
        var bar = style.Bar;

        var show = style.Show;
        if (StyleControls.Toggle("Show nameplate", ref show, "Enable nameplates for this unit type")) { style.Show = show; changed = true; }

        ImGui.Separator();
        ImGui.Text("Geometry");

        var w = bar.Width;
        if (StyleControls.SliderFloat("Width", ref w, 20, 300, "Bar width in pixels")) { bar.Width = w; changed = true; }

        var h = bar.Height;
        if (StyleControls.SliderFloat("Height", ref h, 2, 40, "Bar height in pixels")) { bar.Height = h; changed = true; }

        var yo = bar.YOffset;
        if (StyleControls.SliderFloat("Y offset", ref yo, -50, 50, "Vertical nudge from the entity's head")) { bar.YOffset = yo; changed = true; }

        var op = bar.Opacity;
        if (StyleControls.SliderFloat("Opacity", ref op, 0, 1, "Overall bar opacity")) { bar.Opacity = op; changed = true; }

        FrameControls.DrawVitalSection(p, "Health",        bar.Health,       p.Tex.FillNames, ref changed);
        FrameControls.DrawVitalSection(p, "Energy Shield", bar.EnergyShield, p.Tex.FillNames, ref changed);
        FrameControls.DrawVitalSection(p, "Mana",          bar.Mana,         p.Tex.FillNames, ref changed);

        ImGui.Separator();
        ImGui.Text("DoT overlay");
        var dotEn = bar.DotOverlay.Enabled;
        if (StyleControls.Toggle("Enabled##dot_ov", ref dotEn, "Darken the HP predicted lost to damage over time")) { bar.DotOverlay.Enabled = dotEn; changed = true; }
        ImGui.SameLine();
        var dotCol = bar.DotOverlay.Color;
        if (StyleControls.Color("##dot_ov_col", ref dotCol)) { bar.DotOverlay.Color = dotCol; changed = true; }
        ImGui.SameLine();
        var dotFill = bar.DotOverlay.FillTexture;
        if (BarTexturePicker.Draw("dot_ov_fill", p.Tex.FillNames, p.Tex, ref dotFill, dotCol)) { bar.DotOverlay.FillTexture = dotFill; changed = true; }

        FrameControls.DrawBackgroundAndFrame(p, bar, ref changed);

        ImGui.Separator();
        ImGui.Text("Datatexts");
        ImGui.TextDisabled(FrameControls.TokenHint);
        ImGui.TextDisabled(FrameControls.PoolHint);
        FrameControls.DrawDataSlot("Above bar", style.Above, ref changed);
        FrameControls.DrawDataSlot("On bar", style.OnBar, ref changed);
        FrameControls.DrawDataSlot("Below bar", style.Below, ref changed);

        ImGui.Separator();
        ImGui.Text("Status icons");
        FrameControls.DrawBuffBlock("Buffs", style.Buffs, ref changed);
        FrameControls.DrawBuffBlock("Debuffs", style.Debuffs, ref changed);

        ImGui.Separator();
        ImGui.Text("Preview");
        var prevEnabled = style.PreviewEnabled;
        if (StyleControls.Toggle("Show preview bar", ref prevEnabled, "Draw a sample bar on screen for this unit type")) { style.PreviewEnabled = prevEnabled; changed = true; }
        ImGui.TextDisabled("use the move-mode hotkey to drag previews");

        ImGui.Separator();
        if (ImGui.Button("Copy settings to all unit types"))
            ImGui.OpenPopup("##np_applyall_confirm");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Deep-copies this unit type's whole settings (bar, datatexts, buff blocks) except colors to every other unit type");

        if (ImGui.BeginPopupModal("##np_applyall_confirm"))
        {
            ImGui.Text($"Overwrite EVERY unit type's settings with {b}'s (colors kept per-unit)?");
            ImGui.Separator();
            if (ImGui.Button("Apply to all", new Vector2(120, 0)))
            {
                foreach (Bucket other in Enum.GetValues<Bucket>())
                {
                    var target = p.Settings.Buckets.For(other);
                    var health = target.Bar.Health.Color;
                    var es = target.Bar.EnergyShield.Color;
                    var mana = target.Bar.Mana.Color;
                    var dot = target.Bar.DotOverlay.Color;
                    var bg = target.Bar.Background.Color;
                    var frame = target.Bar.Frame.Color;
                    var aboveCol = target.Above.Color;
                    var onBarCol = target.OnBar.Color;
                    var belowCol = target.Below.Color;

                    target.CopyFrom(style);

                    target.Bar.Health.Color = health;
                    target.Bar.EnergyShield.Color = es;
                    target.Bar.Mana.Color = mana;
                    target.Bar.DotOverlay.Color = dot;
                    target.Bar.Background.Color = bg;
                    target.Bar.Frame.Color = frame;
                    target.Above.Color = aboveCol;
                    target.OnBar.Color = onBarCol;
                    target.Below.Color = belowCol;
                }
                changed = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        return changed;
    }
}

public static class UnitFramesTab
{
    private static readonly string[] SlotNames = UnitFrameEngine.SlotNames;
    private static readonly string[] CategoryNames = Enum.GetNames<StatusCategory>();

    private static readonly string[] Filters =
        Array.ConvertAll(new string[UnitFrameEngine.Slots.Length], _ => "");

    private static readonly string[] HAlignNames = Enum.GetNames<FrameHAlign>();
    private static readonly string[] VGrowNames = Enum.GetNames<FrameVGrow>();

    public static bool Draw(ExileNameplates p)
    {
        var changed = false;
        changed |= DrawGroup(p.Settings.Frames.Group);

        StyleControls.PushTabColors();
        if (ImGui.BeginTabBar("##np_unitframes"))
        {
            foreach (var slot in UnitFrameEngine.Slots)
            {
                if (ImGui.BeginTabItem($"{SlotNames[(int)slot]}##uf_{slot}"))
                {
                    ImGui.PushID((int)slot);
                    changed |= DrawSlot(p, slot);
                    ImGui.PopID();
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }
        ImGui.PopStyleColor(3);

        return changed;
    }

    private static bool DrawGroup(UnitFrameGroup grp)
    {
        var changed = false;
        ImGui.Text("Group");

        var pos = grp.Pos;
        if (ImGui.DragFloat2("Position##uf_grp_pos", ref pos)) { grp.Pos = pos; changed = true; }
        ImGui.TextDisabled("use the move-mode hotkey to drag the whole group");

        var ha = (int)grp.HAlign;
        if (ImGui.Combo("Expand horizontal##uf_grp_h", ref ha, HAlignNames, HAlignNames.Length))
        { grp.HAlign = (FrameHAlign)ha; changed = true; }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Which edge of each bar lines up with the anchor X");

        var vg = (int)grp.VGrow;
        if (ImGui.Combo("Expand vertical##uf_grp_v", ref vg, VGrowNames, VGrowNames.Length))
        { grp.VGrow = (FrameVGrow)vg; changed = true; }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Down anchors the top of the stack, Up anchors the bottom");

        var sp = grp.Spacing;
        if (StyleControls.SliderFloat("Spacing##uf_grp_sp", ref sp, 0, 40, "Gap between frames")) { grp.Spacing = sp; changed = true; }

        var hide = grp.HideBehindPanels;
        if (StyleControls.Toggle("Hide behind panels##uf_grp_hide", ref hide,
            "Hide the whole group when it overlaps an open inventory/stash panel or a large panel"))
        { grp.HideBehindPanels = hide; changed = true; }

        ImGui.TextDisabled("frames stack in tab order; hidden ones collapse out");
        ImGui.Separator();
        return changed;
    }

    private static bool DrawSlot(ExileNameplates p, UnitSlot slot)
    {
        var changed = false;
        var style = p.Settings.Frames.For(slot);
        var bar = style.Bar;
        var i = (int)slot;

        var en = style.Enabled;
        if (StyleControls.Toggle("Enabled", ref en, "Draw this unit frame")) { style.Enabled = en; changed = true; }

        if (slot == UnitSlot.Spectre)
        {
            ImGui.TextDisabled("one frame per living spectre, all sharing this config");
            var mx = p.Settings.Frames.MaxSpectreFrames;
            if (ImGui.SliderInt("Max spectre frames", ref mx, 1, 12)) { p.Settings.Frames.MaxSpectreFrames = mx; changed = true; }
        }

        if (style.Enabled)
        {
            var found = p.Frames.CountFor(slot);
            if (found == 1) ImGui.TextDisabled($"found: {p.Frames.FirstNameFor(slot)}");
            else if (found > 1) ImGui.TextDisabled($"found {found}, first: {p.Frames.FirstNameFor(slot)}");
            else ImGui.TextDisabled($"no {SlotNames[i].ToLowerInvariant()} found - frame hidden");
        }

        if (ImGui.CollapsingHeader("Geometry##uf_geo"))
        {
            var w = bar.Width;
            if (StyleControls.SliderFloat("Width", ref w, 40, 500, "Bar width in pixels")) { bar.Width = w; changed = true; }

            var h = bar.Height;
            if (StyleControls.SliderFloat("Height", ref h, 4, 60, "Bar height in pixels")) { bar.Height = h; changed = true; }

            var op = bar.Opacity;
            if (StyleControls.SliderFloat("Opacity", ref op, 0, 1, "Overall bar opacity")) { bar.Opacity = op; changed = true; }
        }

        if (ImGui.CollapsingHeader("Vital layers##uf_vitals"))
        {
            FrameControls.DrawVitalSection(p, "Health",        bar.Health,       p.Tex.FillNames, ref changed);
            FrameControls.DrawVitalSection(p, "Energy Shield", bar.EnergyShield, p.Tex.FillNames, ref changed);
            FrameControls.DrawVitalSection(p, "Mana",          bar.Mana,         p.Tex.FillNames, ref changed);
        }

        if (ImGui.CollapsingHeader("Background & frame##uf_chrome"))
            FrameControls.DrawBackgroundAndFrame(p, bar, ref changed);

        if (ImGui.CollapsingHeader("Datatexts##uf_text"))
        {
            ImGui.TextDisabled(FrameControls.TokenHint);
            ImGui.TextDisabled(FrameControls.PoolHint);
            ImGui.TextDisabled("{rarity} {type} {xp} {dotdps} {dps} stay empty on a unit frame");
            FrameControls.DrawDataSlot("Above bar", style.Above, ref changed);
            FrameControls.DrawDataSlot("On bar", style.OnBar, ref changed);
            FrameControls.DrawDataSlot("Below bar", style.Below, ref changed);
        }

        if (ImGui.CollapsingHeader("Status icon blocks##uf_blocks"))
        {
            FrameControls.DrawBuffBlock("Buffs", style.Buffs, ref changed);
            FrameControls.DrawBuffBlock("Debuffs", style.Debuffs, ref changed);
        }

        if (ImGui.CollapsingHeader("Status whitelist##uf_wl"))
            changed |= DrawWhitelist(p, style, i);

        ImGui.Separator();
        if (ImGui.Button("Copy this frame to the others"))
            ImGui.OpenPopup("##uf_copy_confirm");
        if (ImGui.BeginPopupModal("##uf_copy_confirm"))
        {
            ImGui.Text($"Overwrite every other frame with {SlotNames[i]}'s settings?");
            ImGui.Text("The status whitelist stays per-frame.");
            ImGui.Separator();
            if (ImGui.Button("Copy", new Vector2(120, 0)))
            {
                foreach (var other in UnitFrameEngine.Slots)
                {
                    if (other == slot) continue;
                    var t = p.Settings.Frames.For(other);
                    t.Bar.CopyFrom(bar);
                    t.Above.CopyFrom(style.Above);
                    t.OnBar.CopyFrom(style.OnBar);
                    t.Below.CopyFrom(style.Below);
                    t.Buffs.CopyFrom(style.Buffs);
                    t.Debuffs.CopyFrom(style.Debuffs);
                }
                changed = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0))) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        return changed;
    }

    private static bool DrawWhitelist(ExileNameplates p, UnitFrameStyle style, int slotIndex)
    {
        var changed = false;
        var catalog = p.Settings.Statuses;

        if (catalog.Count == 0)
        {
            ImGui.TextDisabled("no statuses configured - add some on the Statuses tab first");
            return false;
        }

        ImGui.TextDisabled("only checked statuses draw on this frame");
        var filter = Filters[slotIndex];
        if (ImGui.InputText("Search##uf_wl_search", ref filter, 64u)) Filters[slotIndex] = filter;

        if (ImGui.BeginChild("##uf_wl_list", new Vector2(0, 160)))
        {
            for (int idx = 0; idx < catalog.Count; idx++)
            {
                var cs = catalog[idx];
                if (!StatusSearch.Matches(cs.Label, cs.Key, filter)) continue;
                ImGui.PushID(idx);

                var entry = style.Statuses.Find(f => f.Key == cs.Key);
                var on = entry != null;
                if (ImGui.Checkbox($"{cs.Label}##uf_wl_chk", ref on))
                {
                    if (on) style.Statuses.Add(new FrameStatus { Key = cs.Key });
                    else style.Statuses.Remove(entry);
                    changed = true;
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild();

        ImGui.Separator();
        ImGui.Text("Per-status overrides");
        ImGui.TextDisabled("unchecked = inherit from the Statuses tab");

        for (int idx = 0; idx < style.Statuses.Count; idx++)
        {
            var fs = style.Statuses[idx];
            var cfg = p.Settings.Statuses.Find(c => c.Key == fs.Key);
            if (cfg == null) continue;
            ImGui.PushID(idx);

            if (ImGui.TreeNode($"{cfg.Label}##uf_ov"))
            {
                var iconOn = fs.Icon.HasValue;
                if (ImGui.Checkbox("Icon##uf_ov_ic", ref iconOn))
                {
                    fs.Icon = iconOn ? cfg.Icon : (StatusIcon?)null;
                    changed = true;
                }
                if (fs.Icon.HasValue)
                {
                    ImGui.SameLine();
                    var ic = fs.Icon.Value;
                    if (StatusIconPicker.Draw($"uf_{slotIndex}_{idx}", p.Tex, ref ic, fs.Tint ?? cfg.Tint))
                    {
                        fs.Icon = ic;
                        changed = true;
                    }
                }

                fs.Tint = StyleControls.OverrideColor("Tint##uf_ov_t", fs.Tint, ref changed);
                fs.Scale = StyleControls.OverrideFloat("Scale##uf_ov_s", fs.Scale, 0.5f, 3f, ref changed);

                var catOn = fs.Category.HasValue;
                if (ImGui.Checkbox("Category##uf_ov_c", ref catOn))
                {
                    fs.Category = catOn ? cfg.Category : (StatusCategory?)null;
                    changed = true;
                }
                if (fs.Category.HasValue)
                {
                    ImGui.SameLine();
                    var ci = (int)fs.Category.Value;
                    ImGui.SetNextItemWidth(120f);
                    if (ImGui.Combo("##uf_ov_cv", ref ci, CategoryNames, CategoryNames.Length))
                    {
                        fs.Category = (StatusCategory)ci;
                        changed = true;
                    }
                }

                ImGui.TreePop();
            }
            ImGui.PopID();
        }

        return changed;
    }
}

public static class StatusesTab
{
    private static string _masterFilter = "";
    private static string _configFilter = "";

    private static readonly string[] CategoryNames = Enum.GetNames<StatusCategory>();
    private static readonly string[] StyleNames = { "Icon only", "Icon + badge", "Icon over badge" };
    private const int MasterCap = 100;

    public static bool Draw(ExileNameplates p)
    {
        var changed = false;

        ImGui.Text("Buff catalog");
        p.Catalog.EnsureLoaded(p.GameController);
        if (!p.Catalog.Loaded)
            ImGui.TextDisabled("load a game to populate the catalog");

        ImGui.InputText("Search##np_master", ref _masterFilter, 64u);

        int shown = 0, total = 0;
        if (ImGui.BeginChild("##np_master_list", new Vector2(0, 160)))
        {
            foreach (var e in p.Catalog.Search(_masterFilter))
            {
                total++;
                if (shown >= MasterCap) continue;
                shown++;

                if (ImGui.Button($"Add##add_{e.Id}") && !p.Settings.Statuses.Exists(cs => cs.Key == e.Id))
                {
                    p.Settings.Statuses.Add(new ConfiguredStatus { Key = e.Id, Label = e.Label });
                    changed = true;
                }
                ImGui.SameLine();
                ImGui.Text($"{e.Label}  ({e.Id})");
            }
        }
        ImGui.EndChild();

        if (total > shown)
            ImGui.TextDisabled($"+{total - shown} more, refine search");

        ImGui.Separator();

        ImGui.Text("Configured");
        ImGui.InputText("Search##np_config", ref _configFilter, 64u);

        ConfiguredStatus toRemove = null;
        var statuses = p.Settings.Statuses;
        for (int idx = 0; idx < statuses.Count; idx++)
        {
            var cs = statuses[idx];
            if (!StatusSearch.Matches(cs.Label, cs.Key, _configFilter)) continue;
            ImGui.PushID(idx);

            var icon = cs.Icon;
            if (StatusIconPicker.Draw($"icon_{cs.Key}", p.Tex, ref icon, cs.Tint))
            {
                cs.Icon = icon;
                changed = true;
            }
            ImGui.SameLine();
            ImGui.Text(cs.Label);

            var tint = cs.Tint;
            if (StyleControls.Color("Tint##t", ref tint)) { cs.Tint = tint; changed = true; }

            var sidx = (int)cs.Style;
            if (ImGui.Combo("Style##s", ref sidx, StyleNames, StyleNames.Length))
            {
                cs.Style = (IconStyle)sidx;
                changed = true;
            }

            if (cs.Style == IconStyle.IconOverBadge)
            {
                var bt = cs.BadgeTint;
                if (StyleControls.Color("Badge tint##b", ref bt)) { cs.BadgeTint = bt; changed = true; }
            }

            var scale = cs.Scale;
            if (StyleControls.SliderFloat("Scale##sc", ref scale, 0.5f, 3f, null)) { cs.Scale = scale; changed = true; }

            var cidx = (int)cs.Category;
            if (ImGui.Combo("Category##c", ref cidx, CategoryNames, CategoryNames.Length))
            {
                cs.Category = (StatusCategory)cidx;
                changed = true;
            }

            StyleControls.DrawPatch(cs.Patch, ref changed);

            if (ImGui.Button("Remove##rm")) toRemove = cs;
            ImGui.Separator();
            ImGui.PopID();
        }

        if (toRemove != null)
        {
            p.Settings.Statuses.Remove(toRemove);
            changed = true;
        }

        return changed;
    }
}

public static class OverridesTab
{
    private static readonly string[] MatchNames = Enum.GetNames<MatchKind>();

    public static bool Draw(ExileNameplates p)
    {
        var changed = false;
        ImGui.TextWrapped("Regex overrides match an entity name or metadata path and layer a partial style on its unit-type bar. First enabled match wins; order is priority.");

        if (ImGui.Button("Add override"))
        {
            p.Settings.Overrides.Add(new StyleOverride());
            changed = true;
        }
        ImGui.Separator();

        var list = p.Settings.Overrides;
        StyleOverride toRemove = null;
        int moveUp = -1, moveDown = -1;

        for (int i = 0; i < list.Count; i++)
        {
            var ov = list[i];
            ImGui.PushID(i);

            var enabled = ov.Enabled;
            if (ImGui.Checkbox("##en", ref enabled)) { ov.Enabled = enabled; changed = true; }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(160f);
            var name = ov.Name;
            if (ImGui.InputText("##name", ref name, 64u)) { ov.Name = name; changed = true; }
            ImGui.SameLine();
            if (ImGui.ArrowButton("##up", ImGuiDir.Up) && i > 0) moveUp = i;
            ImGui.SameLine();
            if (ImGui.ArrowButton("##down", ImGuiDir.Down) && i < list.Count - 1) moveDown = i;
            ImGui.SameLine();
            if (ImGui.Button("Remove##rm")) toRemove = ov;

            ImGui.SetNextItemWidth(90f);
            var mk = (int)ov.Match;
            if (ImGui.Combo("Match##mk", ref mk, MatchNames, MatchNames.Length)) { ov.Match = (MatchKind)mk; changed = true; }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(220f);
            var pat = ov.Pattern;
            if (ImGui.InputText("Pattern##pat", ref pat, 256u)) { ov.Pattern = pat; changed = true; }
            if (ov.IsInvalid)
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.3f, 1f), "invalid regex");

            StyleControls.DrawPatch(ov.Style, ref changed);

            ImGui.Separator();
            ImGui.PopID();
        }

        if (moveUp > 0)
        {
            (list[moveUp - 1], list[moveUp]) = (list[moveUp], list[moveUp - 1]);
            changed = true;
        }
        if (moveDown >= 0 && moveDown < list.Count - 1)
        {
            (list[moveDown + 1], list[moveDown]) = (list[moveDown], list[moveDown + 1]);
            changed = true;
        }
        if (toRemove != null)
        {
            list.Remove(toRemove);
            changed = true;
        }

        return changed;
    }
}

public static class StatesTab
{
    public static bool Draw(ExileNameplates p)
    {
        var changed = false;
        ImGui.TextWrapped("Combat-state styling for monsters. Applies on top of buff/status overrides. Match order: Hidden > Invulnerable > Cullable; an invulnerable monster is never treated as cullable.");
        ImGui.Separator();

        changed |= DrawBlock("Hidden##st_hidden", p.Settings.States.Hidden);
        ImGui.Separator();
        changed |= DrawBlock("Invulnerable##st_invuln", p.Settings.States.Invuln);
        ImGui.Separator();
        changed |= DrawCullable(p.Settings.States.Cullable);

        return changed;
    }

    private static bool DrawBlock(string label, StateStyle s)
    {
        var changed = false;
        ImGui.PushID(label);

        var enabled = s.Enabled;
        if (StyleControls.Toggle(label, ref enabled)) { s.Enabled = enabled; changed = true; }

        if (s.Enabled)
        {
            var suppress = s.Suppress;
            if (StyleControls.Toggle("Hide nameplate entirely##sup", ref suppress)) { s.Suppress = suppress; changed = true; }

            if (!s.Suppress) StyleControls.DrawPatch(s.Patch, ref changed);
        }

        ImGui.PopID();
        return changed;
    }

    private static bool DrawCullable(CullableStateStyle s)
    {
        var changed = false;
        ImGui.PushID("st_cullable");

        var enabled = s.Enabled;
        if (StyleControls.Toggle("Cullable (low HP)##cull", ref enabled)) { s.Enabled = enabled; changed = true; }

        if (s.Enabled)
        {
            var suppress = s.Suppress;
            if (StyleControls.Toggle("Hide nameplate entirely##csup", ref suppress)) { s.Suppress = suppress; changed = true; }

            ImGui.SetNextItemWidth(160f);
            var mult = s.Multiplier;
            if (StyleControls.SliderFloat("Threshold multiplier##cmul", ref mult, 0.1f, 3f)) { s.Multiplier = mult; changed = true; }

            if (ImGui.TreeNode("Base thresholds (HP fraction)##cth"))
            {
                changed |= Threshold("Normal##cn", () => s.Normal, v => s.Normal = v);
                changed |= Threshold("Magic##cm", () => s.Magic, v => s.Magic = v);
                changed |= Threshold("Rare##cr", () => s.Rare, v => s.Rare = v);
                changed |= Threshold("Unique##cu", () => s.Unique, v => s.Unique = v);
                ImGui.TreePop();
            }

            if (!s.Suppress) StyleControls.DrawPatch(s.Patch, ref changed);
        }

        ImGui.PopID();
        return changed;
    }

    private static bool Threshold(string label, Func<float> get, Action<float> set)
    {
        ImGui.SetNextItemWidth(140f);
        var v = get();
        if (ImGui.SliderFloat(label, ref v, 0f, 1f)) { set(v); return true; }
        return false;
    }
}

public static class IgnoreTab
{
    private static string _add = "";

    public static bool Draw(ExileNameplates p)
    {
        var changed = false;
        var list = p.Settings.IgnoredPaths;

        ImGui.TextWrapped("Entities whose metadata path matches an entry never get a nameplate. Matched on the path with the @id suffix stripped. Add live with the [+ ignore] button on the debug overlay.");
        ImGui.Separator();

        ImGui.SetNextItemWidth(360f);
        ImGui.InputText("##addpath", ref _add, 256u);
        ImGui.SameLine();
        if (ImGui.Button("Add") && !string.IsNullOrWhiteSpace(_add))
        {
            var v = _add.Trim();
            if (!list.Contains(v)) { list.Add(v); changed = true; }
            _add = "";
        }
        ImGui.Separator();

        if (list.Count == 0)
            ImGui.TextDisabled("(none)");

        int removeAt = -1;
        for (int i = 0; i < list.Count; i++)
        {
            ImGui.PushID(i);
            if (ImGui.Button("Remove")) removeAt = i;
            ImGui.SameLine();
            ImGui.TextUnformatted(list[i]);
            ImGui.PopID();
        }
        if (removeAt >= 0) { list.RemoveAt(removeAt); changed = true; }

        return changed;
    }
}

public static class PerformanceTab
{
    public static bool Draw(ExileNameplates p)
    {
        var changed = false;

        var enabled = p.Settings.Perf.ProfilerEnabled;
        if (StyleControls.Toggle("Profiler enabled", ref enabled, "Collect per-section timing; slight overhead")) { p.Settings.Perf.ProfilerEnabled = enabled; changed = true; }

        ImGui.Spacing();
        ImGui.Text("Toggle perf overlay"); ImGui.SameLine();
        p.Settings.TogglePerfOverlay.DrawPickerButton("##np_perf_hk");

        ImGui.Text("Toggle move mode"); ImGui.SameLine();
        p.Settings.ToggleMoveMode.DrawPickerButton("##np_move_hk");

        ImGui.Spacing();
        if (ImGui.Button("Reset profiler")) p.Prof.Reset();

        return changed;
    }
}

public static class StyleControls
{
    private static readonly Vector4 TabCol = new(0.16f, 0.20f, 0.28f, 1f);
    private static readonly Vector4 TabHover = new(0.26f, 0.34f, 0.46f, 1f);
    private static readonly Vector4 TabActive = new(0.20f, 0.40f, 0.60f, 1f);

    public static void PushTabColors()
    {
        ImGui.PushStyleColor(ImGuiCol.Tab, TabCol);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, TabHover);
        ImGui.PushStyleColor(ImGuiCol.TabActive, TabActive);
    }

    public static void DrawPatch(StylePatch patch, ref bool changed)
    {
        if (!ImGui.TreeNode("Style patch##patch")) return;
        patch.Show = OverrideBool("Show", patch.Show, ref changed);
        patch.HealthColor = OverrideColor("HP bar##ph", patch.HealthColor, ref changed);
        patch.EnergyShieldColor = OverrideColor("ES##pe", patch.EnergyShieldColor, ref changed);
        patch.BackgroundColor = OverrideColor("Background##pbg", patch.BackgroundColor, ref changed);
        patch.BorderColor = OverrideColor("Border##pbr", patch.BorderColor, ref changed);
        patch.AboveColor = OverrideColor("Above text##pab", patch.AboveColor, ref changed);
        patch.OnBarColor = OverrideColor("On-bar text##pob", patch.OnBarColor, ref changed);
        patch.BelowColor = OverrideColor("Below text##pbe", patch.BelowColor, ref changed);
        patch.FrameThickness = OverrideFloat("Frame thickness##pft", patch.FrameThickness, 0f, 8f, ref changed);
        patch.FrameCornerSize = OverrideFloat("Frame corner##pfc", patch.FrameCornerSize, 0f, 16f, ref changed);
        patch.BarOpacity = OverrideFloat("Bar opacity##pbo", patch.BarOpacity, 0f, 1f, ref changed);
        patch.HealthEnabled = OverrideBool("HP vital", patch.HealthEnabled, ref changed);
        patch.EnergyShieldEnabled = OverrideBool("ES vital", patch.EnergyShieldEnabled, ref changed);
        patch.ManaEnabled = OverrideBool("Mana vital", patch.ManaEnabled, ref changed);
        ImGui.TreePop();
    }

    public static bool Toggle(string label, ref bool v, string tip = null)
    {
        var changed = ImGui.Checkbox(label, ref v);
        Tip(tip);
        return changed;
    }

    public static bool SliderFloat(string label, ref float v, float min, float max, string tip = null)
    {
        var changed = ImGui.SliderFloat(label, ref v, min, max);
        Tip(tip);
        return changed;
    }

    public static bool Color(string label, ref Vector4 v, string tip = null)
    {
        var changed = ImGui.ColorEdit4(label, ref v,
            ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.NoInputs);
        Tip(tip);
        return changed;
    }

    public static Vector4? OverrideColor(string label, Vector4? cur, ref bool changed)
    {
        var on = cur.HasValue;
        if (ImGui.Checkbox(label, ref on))
        {
            cur = on ? (cur ?? new Vector4(1, 1, 1, 1)) : null;
            changed = true;
        }
        if (cur.HasValue)
        {
            ImGui.SameLine();
            var v = cur.Value;
            if (Color($"{label}##c", ref v)) { cur = v; changed = true; }
        }
        return cur;
    }

    public static float? OverrideFloat(string label, float? cur, float min, float max, ref bool changed)
    {
        var on = cur.HasValue;
        if (ImGui.Checkbox(label, ref on))
        {
            cur = on ? (cur ?? min) : (float?)null;
            changed = true;
        }
        if (cur.HasValue)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120f);
            var v = cur.Value;
            if (ImGui.SliderFloat($"{label}##f", ref v, min, max)) { cur = v; changed = true; }
        }
        return cur;
    }

    public static bool? OverrideBool(string label, bool? cur, ref bool changed)
    {
        var on = cur.HasValue;
        if (ImGui.Checkbox($"set {label}", ref on))
        {
            cur = on ? (cur ?? false) : (bool?)null;
            changed = true;
        }
        if (cur.HasValue)
        {
            ImGui.SameLine();
            var v = cur.Value;
            if (ImGui.Checkbox($"{label}##b", ref v)) { cur = v; changed = true; }
        }
        return cur;
    }

    private static void Tip(string tip)
    {
        if (tip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
    }
}

public static class FrameControls
{
    private static readonly string[] AlignNames = { "Left", "Center", "Right" };
    private static readonly string[] AnchorNames = Enum.GetNames<BlockAnchor>();

    public const string TokenHint =
        "tokens: {name} {hp} {hpmax} {hp%} {life} {lifemax} {life%} {es} {esmax} {es%}\n"
        + "{mana} {manamax} {mana%} {dist} {rarity} {type} {dotdps} {dotloss} {dps} {xp} {xp%}";

    public const string PoolHint = "{hp} is life+ES combined; {life} is life alone";

    public static void DrawVitalSection(ExileNameplates p, string label, VitalLayer layer,
                                        IReadOnlyList<string> fillNames, ref bool changed)
    {
        ImGui.Separator();
        ImGui.Text(label);

        var en = layer.Enabled;
        if (StyleControls.Toggle($"Enabled##{label}", ref en)) { layer.Enabled = en; changed = true; }

        ImGui.SameLine();
        var col = layer.Color;
        if (StyleControls.Color($"##col_{label}", ref col)) { layer.Color = col; changed = true; }
        ImGui.SameLine();

        var fill = layer.FillTexture;
        if (BarTexturePicker.Draw($"fill_{label}", fillNames, p.Tex, ref fill, col)) { layer.FillTexture = fill; changed = true; }
    }

    public static void DrawDataSlot(string label, DataTextSlot slot, ref bool changed)
    {
        ImGui.Separator();
        ImGui.Text(label);

        var en = slot.Enabled;
        if (StyleControls.Toggle($"Enabled##dt_{label}", ref en)) { slot.Enabled = en; changed = true; }

        var tmpl = slot.Template;
        if (ImGui.InputText($"Template##dt_{label}", ref tmpl, 128u)) { slot.Template = tmpl; changed = true; }

        var ai = (int)slot.Align;
        if (ImGui.Combo($"Align##dt_{label}", ref ai, AlignNames, AlignNames.Length)) { slot.Align = (FontAlign)ai; changed = true; }

        var col = slot.Color;
        if (StyleControls.Color($"Color##dt_{label}", ref col)) { slot.Color = col; changed = true; }

        var sc = slot.Scale;
        if (StyleControls.SliderFloat($"Scale##dt_{label}", ref sc, 0.5f, 2f, null)) { slot.Scale = sc; changed = true; }

        var off = slot.Offset;
        if (ImGui.DragFloat2($"Offset##dt_{label}", ref off)) { slot.Offset = off; changed = true; }
    }

    public static void DrawBuffBlock(string label, BuffBlock blk, ref bool changed)
    {
        ImGui.Separator();
        ImGui.Text(label);

        var en = blk.Enabled;
        if (StyleControls.Toggle($"Enabled##bb_{label}", ref en)) { blk.Enabled = en; changed = true; }

        var ai = (int)blk.Anchor;
        if (ImGui.Combo($"Anchor##bb_{label}", ref ai, AnchorNames, AnchorNames.Length)) { blk.Anchor = (BlockAnchor)ai; changed = true; }

        var sz = blk.IconSize;
        if (StyleControls.SliderFloat($"Icon size##bb_{label}", ref sz, 8, 48, null)) { blk.IconSize = sz; changed = true; }

        var sp = blk.Spacing;
        if (StyleControls.SliderFloat($"Spacing##bb_{label}", ref sp, 0, 12, null)) { blk.Spacing = sp; changed = true; }

        var co = blk.Columns;
        if (ImGui.SliderInt($"Columns##bb_{label}", ref co, 1, 12)) { blk.Columns = co; changed = true; }

        var mx = blk.MaxIcons;
        if (ImGui.SliderInt($"Max icons##bb_{label}", ref mx, 1, 24)) { blk.MaxIcons = mx; changed = true; }

        var ss = blk.ShowStacks;
        if (StyleControls.Toggle($"Show stacks##bb_{label}", ref ss)) { blk.ShowStacks = ss; changed = true; }

        var sd = blk.ShowDuration;
        if (StyleControls.Toggle($"Show duration##bb_{label}", ref sd)) { blk.ShowDuration = sd; changed = true; }
    }

    public static void DrawBackgroundAndFrame(ExileNameplates p, BarStyle bar, ref bool changed)
    {
        ImGui.Separator();
        ImGui.Text("Background");
        var bgColor = bar.Background.Color;
        if (StyleControls.Color("BG color##bg_col", ref bgColor)) { bar.Background.Color = bgColor; changed = true; }
        ImGui.SameLine();
        var bgTex = bar.Background.TrackTexture;
        if (BarTexturePicker.Draw("bg_tex", p.Tex.TrackNames, p.Tex, ref bgTex, bgColor)) { bar.Background.TrackTexture = bgTex; changed = true; }

        var bgOp = bar.Background.Opacity;
        if (StyleControls.SliderFloat("BG opacity", ref bgOp, 0, 1, null)) { bar.Background.Opacity = bgOp; changed = true; }

        ImGui.Separator();
        ImGui.Text("Frame");
        var frColor = bar.Frame.Color;
        if (StyleControls.Color("Frame color##fr_col", ref frColor)) { bar.Frame.Color = frColor; changed = true; }

        var frThick = bar.Frame.Thickness;
        if (StyleControls.SliderFloat("Thickness", ref frThick, 0, 6, "Border thickness in px (0 = no border)")) { bar.Frame.Thickness = frThick; changed = true; }

        var frRound = bar.Frame.CornerSize;
        if (StyleControls.SliderFloat("Rounding", ref frRound, 0, 16, "Corner rounding radius")) { bar.Frame.CornerSize = frRound; changed = true; }
    }
}

public static class StatusIconPicker
{
    private static readonly StatusIcon[] All = Enum.GetValues<StatusIcon>();

    private static string _filter = "";

    public static bool Draw(string id, TextureCache tex, ref StatusIcon selected, Vector4 tint)
    {
        var atlas = tex.Get("Icons_Status_Gem");
        var popupId = $"##iconpick_{id}";
        var changed = false;

        var (cu0, cu1) = StatusAtlas.GetUVPair(selected);
        if (atlas != IntPtr.Zero)
        {
            if (ImGui.ImageButton($"##icbtn_{id}", atlas, new Vector2(24, 24), cu0, cu1, Vector4.Zero, tint))
                ImGui.OpenPopup(popupId);
        }
        else if (ImGui.Button($"{(int)selected}##{id}"))
        {
            ImGui.OpenPopup(popupId);
        }

        if (ImGui.BeginPopup(popupId))
        {
            const int cols = 12;

            if (ImGui.IsWindowAppearing()) { _filter = ""; ImGui.SetKeyboardFocusHere(); }
            ImGui.SetNextItemWidth(cols * 42f);
            ImGui.InputText($"##icfilter_{id}", ref _filter, 32u);

            const float iconPx = 40f;
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.15f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1, 1, 1, 0.30f));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(1, 1));

            int col = 0;
            for (int i = 0; i < All.Length; i++)
            {
                var icon = All[i];
                var name = icon.ToString();
                if (!StatusSearch.Matches(name, name, _filter)) continue;

                var (u0, u1) = StatusAtlas.GetUVPair(icon);
                bool clicked = atlas != IntPtr.Zero
                    ? ImGui.ImageButton($"##icp_{id}_{i}", atlas, new Vector2(iconPx, iconPx), u0, u1, Vector4.Zero, Vector4.One)
                    : ImGui.Button($"{i}##icp_{id}_{i}", new Vector2(iconPx, iconPx));
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(name);
                if (clicked) { selected = icon; changed = true; ImGui.CloseCurrentPopup(); }

                col++;
                if (col % cols != 0) ImGui.SameLine();
            }

            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
            ImGui.EndPopup();
        }

        return changed;
    }
}

public static class BarTexturePicker
{
    private static string _filter = "";

    private static readonly Vector2 SwatchSize = new(80, 16);

    public static bool Draw(string id, IReadOnlyList<string> names, TextureCache tex, ref string selected, Vector4 tint)
    {
        var popupId = $"##barpick_popup_{id}";
        var changed = false;
        var curId = selected != null ? tex.Get(selected) : 0;

        if (curId != IntPtr.Zero)
        {
            if (ImGui.ImageButton($"##btn_{id}", curId, SwatchSize, Vector2.Zero, Vector2.One, Vector4.Zero, tint))
                ImGui.OpenPopup(popupId);
        }
        else
        {
            if (ImGui.Button($"{selected ?? "(none)"}##{id}"))
                ImGui.OpenPopup(popupId);
        }

        if (ImGui.BeginPopup(popupId))
        {
            ImGui.InputText($"##filter_{id}", ref _filter, 64);
            ImGui.Separator();

            var cols = 3;
            var col = 0;
            foreach (var name in names)
            {
                if (!string.IsNullOrEmpty(_filter) && !name.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var isCur = name == selected;
                var texId = tex.Get(name);

                if (isCur) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.8f, 1f));

                bool clicked;
                if (texId != IntPtr.Zero)
                    clicked = ImGui.ImageButton($"##pick_{id}_{name}", texId, SwatchSize, Vector2.Zero, Vector2.One, Vector4.Zero, tint);
                else
                    clicked = ImGui.Button($"{name}##pick_{id}_{name}");

                if (isCur) ImGui.PopStyleColor();

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(name);

                if (clicked)
                {
                    selected = name;
                    changed = true;
                    _filter = "";
                    ImGui.CloseCurrentPopup();
                }

                col++;
                if (col < cols)
                    ImGui.SameLine();
                else
                    col = 0;
            }

            ImGui.EndPopup();
        }

        return changed;
    }
}
