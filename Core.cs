using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileNameplates.Settings;
using ImGuiNET;

namespace ExileNameplates.Core;

public enum BlockAnchor { Above, Below, Left, Right }

public enum Bucket { Normal, Magic, Rare, Unique, Ally, Minion, Npc, Self }

public enum MatchKind { Name, Path }

public enum UnitSlot { Player, Mercenary, AnimatedGuardian, Spectre }

public enum FrameHAlign { Left, Center, Right }
public enum FrameVGrow { Down, Up }

public enum TextToken
{
    Literal,
    Name, Hp, HpMax, HpPct, Life, LifeMax, LifePct, Es, EsMax, EsPct, Mana, ManaMax, ManaPct,
    Dist, Rarity, Type, DotDps, DotLoss, Dps, Xp, XpPct,
}

public sealed class CompiledTemplate
{
    private readonly struct Seg
    {
        public readonly TextToken Token;
        public readonly string Text;
        public Seg(TextToken token, string text) { Token = token; Text = text; }
    }

    private static readonly Dictionary<string, TextToken> Tokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = TextToken.Name,
        ["hp"] = TextToken.Hp, ["hpmax"] = TextToken.HpMax, ["hp%"] = TextToken.HpPct,
        ["life"] = TextToken.Life, ["lifemax"] = TextToken.LifeMax, ["life%"] = TextToken.LifePct,
        ["es"] = TextToken.Es, ["esmax"] = TextToken.EsMax, ["es%"] = TextToken.EsPct,
        ["mana"] = TextToken.Mana, ["manamax"] = TextToken.ManaMax, ["mana%"] = TextToken.ManaPct,
        ["dist"] = TextToken.Dist, ["rarity"] = TextToken.Rarity, ["type"] = TextToken.Type,
        ["dotdps"] = TextToken.DotDps, ["dotloss"] = TextToken.DotLoss, ["dps"] = TextToken.Dps,
        ["xp"] = TextToken.Xp, ["xp%"] = TextToken.XpPct,
    };

    private static readonly StringBuilder Sb = new(64);

    private readonly Seg[] _segs;
    private readonly string _constant;

    public static readonly CompiledTemplate Empty = new(Array.Empty<Seg>(), "");

    private CompiledTemplate(Seg[] segs, string constant) { _segs = segs; _constant = constant; }

    public static CompiledTemplate Parse(string template)
    {
        if (string.IsNullOrEmpty(template)) return Empty;

        var segs = new List<Seg>(4);
        int i = 0, litStart = 0;
        while (i < template.Length)
        {
            if (template[i] == '{')
            {
                var end = template.IndexOf('}', i + 1);
                if (end > i)
                {
                    if (i > litStart) segs.Add(new Seg(TextToken.Literal, template[litStart..i]));
                    var name = template.Substring(i + 1, end - i - 1);
                    segs.Add(Tokens.TryGetValue(name, out var tok) ? new Seg(tok, null) : new Seg(TextToken.Literal, "{" + name + "}"));
                    i = litStart = end + 1;
                    continue;
                }
            }
            i++;
        }
        if (i > litStart) segs.Add(new Seg(TextToken.Literal, template[litStart..]));

        var constant = segs.Count == 0 ? ""
                     : segs.Count == 1 && segs[0].Token == TextToken.Literal ? segs[0].Text
                     : null;
        return new CompiledTemplate(segs.ToArray(), constant);
    }

    public string Render(in Snapshot s, string name)
    {
        if (_constant != null) return _constant;
        if (_segs.Length == 1) return Resolve(_segs[0].Token, s, name);

        Sb.Clear();
        foreach (var seg in _segs)
            Sb.Append(seg.Token == TextToken.Literal ? seg.Text : Resolve(seg.Token, s, name));
        return Sb.ToString();
    }

    private static string Resolve(TextToken t, in Snapshot s, string name) => t switch
    {
        TextToken.Name => name ?? "",
        TextToken.Hp => NumberFmt.Abbrev(s.HpCur + s.EsCur),
        TextToken.HpMax => NumberFmt.Abbrev(s.HpMax + s.EsMax),
        TextToken.HpPct => Pct(s.HpCur + s.EsCur, s.HpMax + s.EsMax),
        TextToken.Life => NumberFmt.Abbrev(s.HpCur),
        TextToken.LifeMax => NumberFmt.Abbrev(s.HpMax),
        TextToken.LifePct => Pct(s.HpCur, s.HpMax),
        TextToken.Es => NumberFmt.Abbrev(s.EsCur),
        TextToken.EsMax => NumberFmt.Abbrev(s.EsMax),
        TextToken.EsPct => Pct(s.EsCur, s.EsMax),
        TextToken.Mana => NumberFmt.Abbrev(s.ManaCur),
        TextToken.ManaMax => NumberFmt.Abbrev(s.ManaMax),
        TextToken.ManaPct => Pct(s.ManaCur, s.ManaMax),
        TextToken.Dist => ((int)s.Dist).ToString(CultureInfo.InvariantCulture),
        TextToken.Rarity => s.Rarity ?? "",
        TextToken.Type => s.Type ?? "",
        TextToken.DotDps => NumberFmt.Abbrev(s.DotRate),
        TextToken.DotLoss => NumberFmt.Abbrev(s.DotLoss),
        TextToken.Dps => NumberFmt.Abbrev(s.Dps),
        TextToken.Xp => NumberFmt.Abbrev(s.Xp),
        TextToken.XpPct => s.XpPct <= 0f ? "0" : s.XpPct < 0.01f ? "<0.01" : s.XpPct.ToString("0.##", CultureInfo.InvariantCulture),
        _ => "",
    };

    private static string Pct(float cur, float max)
    {
        if (max <= 0f) return "0";
        var p = (int)MathF.Round(cur / max * 100f);
        return Math.Clamp(p, 0, 100).ToString(CultureInfo.InvariantCulture);
    }
}

public static class DotPredictor
{
    public static (float rate, float loss) Predict(float ratePerMin, List<Buff> buffs, float curHp, DotDurations d)
    {
        var rate = ratePerMin / 60f;
        if (curHp <= 0f) return (rate, 0f);

        float horizon = 0f;
        bool anyDot = false;
        foreach (var buff in buffs)
        {
            if (buff == null) continue;
            var id = buff.BuffDefinition?.Id ?? buff.Name ?? "";
            bool Has(string part) => id.Contains(part, StringComparison.OrdinalIgnoreCase);
            var fallback = Has("poison") ? d.PoisonSeconds
                : Has("ignite") || Has("burning") ? d.IgniteSeconds
                : Has("bleed") || Has("corrupted_blood") ? d.BleedSeconds
                : Has("caustic") ? d.CausticSeconds
                : Has("decay") ? d.DecaySeconds
                : -1f;
            if (fallback < 0f) continue;
            anyDot = true;
            var remaining = float.IsFinite(buff.Timer) && buff.Timer > 0f ? buff.Timer : fallback;
            if (remaining > horizon) horizon = remaining;
        }
        if (!anyDot) horizon = d.FallbackSeconds;

        return (rate, Math.Min(rate * horizon, curHp));
    }
}

public struct DpsMeter
{
    private bool _seen;
    private float _lastEff;
    private double _lastT;
    private float _dps;

    private const double MinDt = 0.1;

    public float Update(float effHp, double now, double tau)
    {
        if (!_seen) { _seen = true; _lastEff = effHp; _lastT = now; return 0f; }
        if (tau < 0.05) tau = 0.05;

        var dt = now - _lastT;
        if (dt < MinDt) return _dps;

        var drop = _lastEff - effHp;
        var rate = drop > 0f ? drop / (float)dt : 0f;
        var alpha = 1.0 - Math.Exp(-dt / tau);
        _dps += (float)(alpha * (rate - _dps));
        if (_dps < 0f) _dps = 0f;

        _lastEff = effHp;
        _lastT = now;
        return _dps;
    }
}

public static class EntityClassifier
{
    public static readonly EntityType[] Types =
        { EntityType.Monster, EntityType.Player, EntityType.Pet, EntityType.Npc, EntityType.Daemon };

    public static Bucket Classify(Entity e, Entity localPlayer)
    {
        if (localPlayer != null && e.Address == localPlayer.Address) return Bucket.Self;

        switch (e.Type)
        {
            case EntityType.Player: return Bucket.Ally;
            case EntityType.Npc: return Bucket.Npc;
            case EntityType.Pet: return Bucket.Minion;
            case EntityType.Daemon:
            case EntityType.Monster:
                if (!e.IsHostile) return Bucket.Minion;
                return e.Rarity switch
                {
                    MonsterRarity.Magic => Bucket.Magic,
                    MonsterRarity.Rare => Bucket.Rare,
                    MonsterRarity.Unique => Bucket.Unique,
                    _ => Bucket.Normal,
                };
            default: return Bucket.Normal;
        }
    }
}

public enum IconStyle { IconOnly, IconBadge, IconOverBadge }

public static class NameAbbreviator
{
    private static readonly Dictionary<string, string> Cache = new();
    private static int _cacheThreshold = -1;

    public static string Abbreviate(string name, int threshold)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (threshold > 0 && name.Length <= threshold) return name;

        if (_cacheThreshold != threshold) { Cache.Clear(); _cacheThreshold = threshold; }
        if (Cache.TryGetValue(name, out var cached)) return cached;

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var abbrev = words.Length <= 1
            ? name
            : string.Join(" ", words[..^1].Select(w => char.ToUpper(w[0]) + ".")) + " " + words[^1];
        Cache[name] = abbrev;
        return abbrev;
    }
}

public sealed class Nameplate
{
    public readonly Entity Entity;
    public readonly string PathKey;
    public readonly string FallbackName;
    public Life Life;
    public Render Render;
    public Stats Stats;

    public Bucket Bucket;
    public NameplateStyle Resolved;
    public Snapshot Snap;

    public string RarityName = "";
    public string TypeName = "";

    public int Xp;
    public float XpPct;
    public int XpForLevel = -1;

    public DpsMeter Dps;

    private NameplateStyle _eff;

    public readonly List<ResolvedStatus> Buffs = new();
    public readonly List<ResolvedStatus> Debuffs = new();

    private int _version = -1;
    private bool _lastHostile;
    private EntityType _lastType = (EntityType)(-1);
    private MonsterRarity _lastRarity = (MonsterRarity)(-1);

    public bool HasResolved => Resolved != null;
    public bool CachedHostile => _lastHostile;
    public EntityType CachedType => _lastType;
    public MonsterRarity CachedRarity => _lastRarity;

    public Nameplate(Entity entity)
    {
        Entity = entity;
        PathKey = PathUtil.KeyOf(entity);
        FallbackName = PathKey[(PathKey.LastIndexOf('/') + 1)..];
    }

    public void Resolve(ExileNameplatesSettings s, Entity localPlayer)
    {
        var hostile = Entity.IsHostile;
        var type = Entity.Type;
        var rarity = Entity.Rarity;
        if (_version == s.SettingsVersion && _lastHostile == hostile
            && _lastType == type && _lastRarity == rarity && Resolved != null)
            return;
        _version = s.SettingsVersion;
        _lastHostile = hostile;
        _lastType = type;
        _lastRarity = rarity;

        Bucket = EntityClassifier.Classify(Entity, localPlayer);
        RarityName = type == EntityType.Monster ? rarity.ToString() : "";
        TypeName = Bucket.ToString();
        var basePreset = s.Buckets.For(Bucket);

        var hit = s.Overrides.Find(o => o.IsMatch(Render?.Name, Entity.Path));
        if (hit != null)
        {
            Resolved = basePreset.Clone();
            hit.Style.ApplyTo(Resolved);
        }
        else
        {
            Resolved = basePreset;
        }
    }

    public NameplateStyle Effective(NameplateStyle from)
    {
        _eff ??= new NameplateStyle();
        if (!ReferenceEquals(_eff, from)) _eff.CopyFrom(from);
        return _eff;
    }
}

public static class NumberFmt
{
    public static string Abbrev(float v)
    {
        var n = v < 0f ? 0f : v;
        if (n >= 1_000_000f) return Trim(n / 1_000_000f) + "m";
        if (n >= 1_000f) return Trim(n / 1_000f) + "k";
        return ((int)n).ToString(CultureInfo.InvariantCulture);
    }

    private static string Trim(float x)
    {
        var s = x.ToString("0.0", CultureInfo.InvariantCulture);
        return s.EndsWith(".0") ? s[..^2] : s;
    }
}

public static class PathUtil
{
    public static string KeyOf(Entity e)
    {
        var path = e.Path ?? e.Metadata ?? "";
        var at = path.LastIndexOf('@');
        return at >= 0 ? path[..at] : path;
    }
}

public struct ResolvedStatus
{
    public StatusIcon Icon;
    public IconStyle Style;
    public Vector4 Tint;
    public Vector4 BadgeTint;
    public float Scale;
    public int Stacks;
    public float Remaining;

    private const int MaxSaneStacks = 200;

    public static void AddOrMerge(List<ResolvedStatus> list, Dictionary<(string, uint), int> index,
                                  Buff buff, string key, int maxIcons, ResolvedStatus look)
    {
        var raw = (int)Math.Max(buff.BuffStacks, buff.BuffCharges);
        var stacks = raw > 1 && raw <= MaxSaneStacks ? raw : 1;
        var remaining = float.IsFinite(buff.Timer) ? Math.Max(0f, buff.Timer) : 0f;

        var dedupe = (key, buff.SourceEntityId);
        if (index.TryGetValue(dedupe, out var at))
        {
            var prev = list[at];
            prev.Stacks += stacks;
            if (remaining > prev.Remaining) prev.Remaining = remaining;
            list[at] = prev;
            return;
        }

        if (list.Count >= maxIcons) return;
        index[dedupe] = list.Count;
        look.Stacks = stacks;
        look.Remaining = remaining;
        list.Add(look);
    }
}

public struct Snapshot
{
    public Vector2 Pos;
    public float HpCur, HpMax, EsCur, EsMax, ManaCur, ManaMax;
    public float DotRate;
    public float DotLoss;
    public float Dps;
    public int Xp;
    public float XpPct;
    public IFrameStyle Style;
    public string Name;
    public float Dist;
    public string Rarity;
    public string Type;
}

public enum StatusCategory { Buff, Debuff }

public enum StatusIcon
{
    Ignite          = 0,
    Burning         = 1,
    Chilled         = 2,
    Frozen          = 3,
    Shocked         = 4,
    Electrocuted    = 5,
    Frostbite       = 6,
    Scorched        = 7,

    Bleeding        = 8,
    Hemorrhage      = 9,
    CorruptedBlood  = 10,
    Poisoned        = 11,
    Caustic         = 12,
    Decay           = 13,
    Plagued         = 14,
    Withered        = 15,

    Stunned         = 16,
    Immobilized     = 17,
    Bound           = 18,
    Blinded         = 19,
    Taunted         = 20,
    Maimed          = 21,
    Hindered        = 22,
    Crushed         = 23,

    Exposed         = 24,
    Vulnerable      = 25,
    Cursed          = 26,
    Doomed          = 27,
    Marked          = 28,
    Enfeebled       = 29,
    Hexed           = 30,
    Silenced        = 31,

    Enraged         = 32,
    Berserk         = 33,
    Frenzy          = 34,
    AngerAura       = 35,
    UnholyMight     = 36,
    Impale          = 37,
    Adrenaline      = 38,
    CriticalMark    = 39,

    Fortify         = 40,
    Endurance       = 41,
    MoltenShell     = 42,
    EnergyShield    = 43,
    Block           = 44,
    Regeneration    = 45,
    Haste           = 46,
    Onslaught       = 47,

    FrenzyCharge      = 48,
    EnduranceCharge   = 49,
    PowerCharge       = 50,
    HatredAura        = 51,
    WrathAura         = 52,
    DeterminationAura = 53,
    GraceAura         = 54,
    ClarityAura       = 55,

    Armour          = 56,
    Evasion         = 57,
    Barrier         = 58,
    Immunity        = 59,
    Ward            = 60,
    Deflect         = 61,
    LifeFlask       = 62,
    Recoup          = 63,

    Phasing         = 64,
    Tailwind        = 65,
    Sprint          = 66,
    Slowed          = 67,
    Leech           = 68,
    Blink           = 69,
    Knockback       = 70,
    Temporal        = 71,

    Petrified       = 72,
    Webbed          = 73,
    Pinned          = 74,
    Disarmed        = 75,
    Smothered       = 76,
    Confused        = 77,
    Charmed         = 78,
    Possessed       = 79,

    BurningGround       = 80,
    ChilledGround       = 81,
    ShockedGround       = 82,
    CausticGround       = 83,
    ConsecratedGround   = 84,
    DesecratedGround    = 85,
    BloodGround         = 86,
    FungalGround        = 87,

    Totem           = 88,
    Banner          = 89,
    Brand           = 90,
    Trap            = 91,
    Mine            = 92,
    Portal          = 93,
    Beacon          = 94,
    Shrine          = 95,
}

public static class StatusAtlas
{
    private const int AtlasWidth  = 2048;
    private const int AtlasHeight = 3072;
    private const int CellSize    = 256;
    private const int Columns     = 8;

    public static (Vector2 Uv0, Vector2 Uv1) GetUVPair(StatusIcon icon)
    {
        int i = (int)icon;
        var x = (i % Columns) * CellSize;
        var y = (i / Columns) * CellSize;
        var uv0 = new Vector2((float)x / AtlasWidth, (float)y / AtlasHeight);
        var uv1 = new Vector2((float)(x + CellSize) / AtlasWidth, (float)(y + CellSize) / AtlasHeight);
        return (uv0, uv1);
    }
}

public static class StatusSearch
{
    public static bool Matches(string label, string id, string query)
    {
        if (string.IsNullOrEmpty(query)) return true;
        return (label != null && label.Contains(query, StringComparison.OrdinalIgnoreCase))
            || (id != null && id.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}

public class StylePatch
{
    public bool? Show { get; set; }
    public Vector4? HealthColor { get; set; }
    public Vector4? EnergyShieldColor { get; set; }
    public Vector4? BackgroundColor { get; set; }
    public Vector4? BorderColor { get; set; }
    public Vector4? AboveColor { get; set; }
    public Vector4? OnBarColor { get; set; }
    public Vector4? BelowColor { get; set; }
    public float? FrameThickness { get; set; }
    public float? FrameCornerSize { get; set; }
    public float? BarOpacity { get; set; }
    public bool? HealthEnabled { get; set; }
    public bool? EnergyShieldEnabled { get; set; }
    public bool? ManaEnabled { get; set; }

    public bool HasAny =>
        Show.HasValue || HealthColor.HasValue || EnergyShieldColor.HasValue ||
        BackgroundColor.HasValue || BorderColor.HasValue ||
        AboveColor.HasValue || OnBarColor.HasValue || BelowColor.HasValue ||
        FrameThickness.HasValue || FrameCornerSize.HasValue || BarOpacity.HasValue ||
        HealthEnabled.HasValue || EnergyShieldEnabled.HasValue || ManaEnabled.HasValue;

    public void ApplyTo(NameplateStyle target)
    {
        if (Show.HasValue) target.Show = Show.Value;
        var bar = target.Bar;
        if (HealthColor.HasValue) bar.Health.Color = HealthColor.Value;
        if (EnergyShieldColor.HasValue) bar.EnergyShield.Color = EnergyShieldColor.Value;
        if (BackgroundColor.HasValue) bar.Background.Color = BackgroundColor.Value;
        if (BorderColor.HasValue) bar.Frame.Color = BorderColor.Value;
        if (AboveColor.HasValue) target.Above.Color = AboveColor.Value;
        if (OnBarColor.HasValue) target.OnBar.Color = OnBarColor.Value;
        if (BelowColor.HasValue) target.Below.Color = BelowColor.Value;
        if (FrameThickness.HasValue) bar.Frame.Thickness = FrameThickness.Value;
        if (FrameCornerSize.HasValue) bar.Frame.CornerSize = FrameCornerSize.Value;
        if (BarOpacity.HasValue) bar.Opacity = BarOpacity.Value;
        if (HealthEnabled.HasValue) bar.Health.Enabled = HealthEnabled.Value;
        if (EnergyShieldEnabled.HasValue) bar.EnergyShield.Enabled = EnergyShieldEnabled.Value;
        if (ManaEnabled.HasValue) bar.Mana.Enabled = ManaEnabled.Value;
    }

    public void FillFrom(StylePatch o)
    {
        Show ??= o.Show;
        HealthColor ??= o.HealthColor;
        EnergyShieldColor ??= o.EnergyShieldColor;
        BackgroundColor ??= o.BackgroundColor;
        BorderColor ??= o.BorderColor;
        AboveColor ??= o.AboveColor;
        OnBarColor ??= o.OnBarColor;
        BelowColor ??= o.BelowColor;
        FrameThickness ??= o.FrameThickness;
        FrameCornerSize ??= o.FrameCornerSize;
        BarOpacity ??= o.BarOpacity;
        HealthEnabled ??= o.HealthEnabled;
        EnergyShieldEnabled ??= o.EnergyShieldEnabled;
        ManaEnabled ??= o.ManaEnabled;
    }

    public void Reset()
    {
        Show = null;
        HealthColor = EnergyShieldColor = BackgroundColor = BorderColor = null;
        AboveColor = OnBarColor = BelowColor = null;
        FrameThickness = FrameCornerSize = BarOpacity = null;
        HealthEnabled = EnergyShieldEnabled = ManaEnabled = null;
    }
}

public struct FrameDraw
{
    public UnitSlot Slot;
    public UnitFrameStyle Style;
    public Snapshot Snap;
    public List<ResolvedStatus> Buffs;
    public List<ResolvedStatus> Debuffs;
}

public sealed class UnitFrameEngine
{
    public static readonly UnitSlot[] Slots = Enum.GetValues<UnitSlot>();
    public static readonly string[] SlotNames = { "Player", "Mercenary", "Animated Guardian", "Spectre" };

    private const string MercPrefix = "Metadata/Monsters/Mercenaries/";
    private const string GuardianPrefix = "Metadata/Monsters/AnimatedItem/AnimatedArmour";
    private const string SpectrePrefix = "Metadata/Monsters/LeagueAzmeri/SpecialCorpses/";
    private static readonly Comparison<Entity> ById = (a, b) => a.Id.CompareTo(b.Id);

    public readonly List<FrameDraw> Draws = new();

    private Entity _mercenary;
    private Entity _guardian;
    private readonly List<Entity> _spectres = new();
    private readonly List<List<ResolvedStatus>> _buffPool = new();
    private readonly List<List<ResolvedStatus>> _debuffPool = new();
    private readonly Dictionary<string, (FrameStatus Frame, ConfiguredStatus Cfg)>[] _whitelist;
    private readonly Dictionary<(string, uint), int> _index = new();
    private int _version = -1;

    public UnitFrameEngine()
    {
        _whitelist = new Dictionary<string, (FrameStatus, ConfiguredStatus)>[Slots.Length];
        for (int i = 0; i < _whitelist.Length; i++) _whitelist[i] = new();
    }

    public void Reset() => Draws.Clear();

    public void Tick(ExileNameplatesSettings s, GameController gc)
    {
        if (_version != s.SettingsVersion)
        {
            _version = s.SettingsVersion;
            foreach (var slot in Slots)
            {
                var wl = _whitelist[(int)slot];
                wl.Clear();
                foreach (var fs in s.Frames.For(slot).Statuses)
                {
                    var cs = string.IsNullOrEmpty(fs.Key) ? null : s.Statuses.Find(c => c.Key == fs.Key);
                    if (cs != null) wl[fs.Key] = (fs, cs);
                }
            }
        }

        Draws.Clear();
        FindUnits(gc, s.Frames.Spectre.Enabled ? s.Frames.MaxSpectreFrames : 0);

        Add(s, UnitSlot.Player, gc.Player);
        Add(s, UnitSlot.Mercenary, _mercenary);
        Add(s, UnitSlot.AnimatedGuardian, _guardian);
        foreach (var sp in _spectres)
            Add(s, UnitSlot.Spectre, sp);
    }

    private void FindUnits(GameController gc, int maxSpectres)
    {
        _mercenary = null;
        _guardian = null;
        _spectres.Clear();
        if (!gc.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Monster, out var list))
            return;

        float bestMerc = float.MaxValue, bestGuardian = float.MaxValue;

        foreach (var e in list)
        {
            if (e == null || !e.IsValid || !e.IsAlive || e.IsHostile) continue;
            var path = e.Path ?? e.Metadata ?? "";

            if (path.StartsWith(MercPrefix, StringComparison.Ordinal))
            {
                var d = e.DistancePlayer;
                if (d < bestMerc) { bestMerc = d; _mercenary = e; }
            }
            else if (path.StartsWith(GuardianPrefix, StringComparison.Ordinal))
            {
                var d = e.DistancePlayer;
                if (d < bestGuardian) { bestGuardian = d; _guardian = e; }
            }
            else if (path.StartsWith(SpectrePrefix, StringComparison.Ordinal))
            {
                _spectres.Add(e);
            }
        }

        _spectres.Sort(ById);
        if (_spectres.Count > maxSpectres)
            _spectres.RemoveRange(maxSpectres, _spectres.Count - maxSpectres);
    }

    private void Add(ExileNameplatesSettings s, UnitSlot slot, Entity e)
    {
        var style = s.Frames.For(slot);
        if (!style.Enabled) return;
        if (e == null || !e.IsValid || !e.IsAlive) return;

        e.TryGetComponent<Life>(out var life);
        e.TryGetComponent<Render>(out var render);

        var at = Draws.Count;
        var buffs = Rent(_buffPool, at);
        var debuffs = Rent(_debuffPool, at);

        var wl = _whitelist[(int)slot];
        var all = wl.Count > 0 && (style.Buffs.Enabled || style.Debuffs.Enabled) ? e.Buffs : null;
        if (all != null)
        {
            _index.Clear();
            foreach (var buff in all)
            {
                if (buff == null || buff.IsInvisible) continue;
                var key = buff.BuffDefinition?.Id ?? buff.Name;
                if (key == null || !wl.TryGetValue(key, out var m)) continue;

                var isBuff = (m.Frame.Category ?? m.Cfg.Category) == StatusCategory.Buff;
                var block = isBuff ? style.Buffs : style.Debuffs;
                if (!block.Enabled) continue;

                ResolvedStatus.AddOrMerge(isBuff ? buffs : debuffs, _index, buff, key, block.MaxIcons, new ResolvedStatus
                {
                    Icon = m.Frame.Icon ?? m.Cfg.Icon,
                    Style = m.Cfg.Style,
                    Tint = m.Frame.Tint ?? m.Cfg.Tint,
                    BadgeTint = m.Cfg.BadgeTint,
                    Scale = m.Frame.Scale ?? m.Cfg.Scale,
                });
            }
        }

        Draws.Add(new FrameDraw
        {
            Slot = slot,
            Style = style,
            Buffs = buffs,
            Debuffs = debuffs,
            Snap = new Snapshot
            {
                HpCur = life?.CurHP ?? 0, HpMax = life?.MaxHP ?? 0,
                EsCur = life?.CurES ?? 0, EsMax = life?.MaxES ?? 0,
                ManaCur = life?.CurMana ?? 0, ManaMax = life?.MaxMana ?? 0,
                Style = style,
                Name = render?.Name ?? SlotNames[(int)slot],
                Dist = e.DistancePlayer,
                Rarity = "",
                Type = SlotNames[(int)slot],
            },
        });
    }

    private static List<ResolvedStatus> Rent(List<List<ResolvedStatus>> pool, int index)
    {
        while (pool.Count <= index) pool.Add(new List<ResolvedStatus>());
        var l = pool[index];
        l.Clear();
        return l;
    }

    public int CountFor(UnitSlot slot)
    {
        var n = 0;
        foreach (var d in Draws) if (d.Slot == slot) n++;
        return n;
    }

    public string FirstNameFor(UnitSlot slot)
    {
        foreach (var d in Draws) if (d.Slot == slot) return d.Snap.Name;
        return null;
    }
}

public static class XpCalculator
{
    private static readonly int[] BaseXp =
    {
        20,20,25,38,51,67,88,113,141,173,211,250,297,347,402,461,526,596,668,748,
        831,921,1013,1113,1216,1325,1437,1560,1685,1812,1950,2089,2235,2386,2543,2705,2873,3045,3227,3407,
        3597,3796,3993,4203,4415,4634,4854,5086,5318,5561,5808,6064,6324,6585,6859,7134,7413,7703,7996,8265,
        8525,8781,9032,9283,9532,9773,10014,10255,10487,10720,10949,11170,11388,11601,11804,12004,12192,12377,12549,12712,
        12863,13006,13132,13250,13352,13440,13512,13569,13609,13633,13639,13627,13598,13549,13483,13396,13293,13170,13029,0,
    };

    private static readonly long[] CumulativeXp =
    {
        0,525,1760,3781,7184,12186,19324,29377,43181,61693,85990,117506,157384,207736,269997,346462,439268,551295,685171,843709,
        1030734,1249629,1504995,1800847,2142652,2535122,2984677,3496798,4080655,4742836,5490247,6334393,7283446,8348398,9541110,10874351,12361842,14018289,15859432,17905634,
        20171471,22679999,25456123,28517857,31897771,35621447,39721017,44225461,49176560,54607467,60565335,67094245,74247659,82075627,90631041,99984974,110197515,121340161,133497202,146749362,
        161191120,176922628,194049893,212684946,232956711,255001620,278952403,304972236,333233648,363906163,397194041,433312945,472476370,514937180,560961898,610815862,664824416,723298169,786612664,855129128,
        929261318,1009443795,1096169525,1189918242,1291270350,1400795257,1519130326,1646943474,1784977296,1934009687,2094900291,2268549086,2455921256,2658074992,2876116901,3111280300,3364828162,3638186694,3932818530,4250334444,
    };

    private const string NoXpPrefix = "Metadata/Monsters/LeagueBestiary/";

    public static int RawXp(string path, int expMultiplier, int playerLevel)
    {
        if (string.IsNullOrEmpty(path) || expMultiplier <= 0) return 0;
        if (path.StartsWith(NoXpPrefix, StringComparison.Ordinal)) return 0;

        var at = path.LastIndexOf('@');
        if (at < 0 || !int.TryParse(path.AsSpan(at + 1), out var lvl) || lvl < 1) return 0;
        if (lvl > 100) lvl = 100;

        var raw = (int)MathF.Floor(BaseXp[lvl - 1] * expMultiplier / 100f);
        return (int)(raw * LevelPenalty(playerLevel, lvl));
    }

    public static float XpPercent(int rawXp, int playerLevel)
    {
        if (rawXp <= 0 || playerLevel < 1 || playerLevel >= 100) return 0f;
        var need = CumulativeXp[playerLevel] - CumulativeXp[playerLevel - 1];
        return need <= 0 ? 0f : rawXp / (float)need * 100f;
    }

    private static double LevelPenalty(int playerLevel, int monsterLevel)
    {
        var safeZone = 3 + playerLevel / 16;
        var diff = Math.Abs(playerLevel - monsterLevel) - safeZone;
        if (diff <= 0) return 1.0;
        var mult = Math.Pow((playerLevel + 5) / (double)(playerLevel + 5 + Math.Pow(diff, 2.5)), 1.5);
        return Math.Max(mult, 0.01);
    }

    static XpCalculator()
    {
        Debug.Assert(RawXp("Metadata/Monsters/SkeletonSoldier/SkeletonSoldierRangedTornadoShot@34", 110, 34) == 2624);
        Debug.Assert(RawXp("Metadata/Monsters/SandLeaper/SandLeaper@34", 75, 34) == 1789);
        Debug.Assert(RawXp("Metadata/Monsters/LeagueBestiary/SandLeaperBestiaryMinion@34", 150, 34) == 0);
    }
}

public sealed class BuffCatalog
{
    public readonly struct Entry
    {
        public Entry(string id, string label) { Id = id; Label = label; }
        public string Id { get; }
        public string Label { get; }
    }

    private readonly List<Entry> _entries = new();
    public bool Loaded => _entries.Count > 0;

    public void EnsureLoaded(GameController gc)
    {
        if (_entries.Count > 0) return;
        var defs = gc?.Files?.BuffDefinitions;
        if (defs == null) return;
        defs.ReloadIfEmpty();
        foreach (var d in defs.EntriesList)
        {
            if (d == null || string.IsNullOrEmpty(d.Id)) continue;
            _entries.Add(new Entry(d.Id, Humanize(string.IsNullOrEmpty(d.Name) ? d.Id : d.Name)));
        }
    }

    public IEnumerable<Entry> Search(string query)
    {
        foreach (var e in _entries)
            if (StatusSearch.Matches(e.Label, e.Id, query)) yield return e;
    }

    private static string Humanize(string idOrName)
    {
        var s = idOrName;
        var slash = s.LastIndexOf('/');
        if (slash >= 0 && slash < s.Length - 1) s = s[(slash + 1)..];

        var sb = new StringBuilder(s.Length + 8);
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '_' || c == '-')
            {
                if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                continue;
            }
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1]) && s[i - 1] != ' ')
                sb.Append(' ');
            sb.Append(c);
        }

        var words = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
            words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i][1..].ToLower() : "");
        return string.Join(" ", words);
    }
}

public static class DraggableOverlay
{
    private static string _dragId;
    private static Vector2 _dragOffset;

    private static readonly Vector4 HoverColor = new Vector4(220, 120, 220, 255) / 255f;
    private static readonly Vector4 IdleColor = new Vector4(120, 120, 220, 255) / 255f;

    public static void Drag(string id, ref Vector2 pos, Vector2 size)
    {
        var mouse = ImGui.GetMousePos();
        bool hovered = mouse.X >= pos.X && mouse.X <= pos.X + size.X
                    && mouse.Y >= pos.Y && mouse.Y <= pos.Y + size.Y;

        if (_dragId == null && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragId = id;
            _dragOffset = mouse - pos;
        }

        if (_dragId == id)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left)) pos = mouse - _dragOffset;
            else _dragId = null;
        }

        if (hovered || _dragId == id)
        {
            ImGui.SetNextWindowPos(pos);
            ImGui.SetNextWindowSize(size);
            ImGui.SetNextWindowBgAlpha(0f);
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollWithMouse;
            ImGui.Begin($"##endrag_{id}", flags);
            ImGui.End();
        }

        ImGui.GetForegroundDrawList().AddRect(pos, pos + size, ImGui.GetColorU32(hovered ? HoverColor : IdleColor), 0f, ImDrawFlags.None, 1.5f);
    }
}

public sealed class FrameTimer
{
    private readonly double[] _buf = new double[60];
    private int _i;
    private int _count;

    public void Sample(double ms)
    {
        _buf[_i] = ms;
        _i = (_i + 1) % _buf.Length;
        if (_count < _buf.Length) _count++;
    }

    public double AvgMs
    {
        get
        {
            if (_count == 0) return 0;
            double sum = 0;
            for (int k = 0; k < _count; k++) sum += _buf[k];
            return sum / _count;
        }
    }

    public double Fps => AvgMs > 0 ? 1000.0 / AvgMs : 0;
}

public static class PerfOverlay
{
    public static void Draw(Profiler prof, FrameTimer frame, int considered, int drawn, int culled, ref Vector2 pos, bool moveMode)
    {
        if (moveMode)
            DraggableOverlay.Drag("perf", ref pos, new Vector2(280, 60 + prof.Stats.Count * 16));

        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowBgAlpha(0.75f);
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoFocusOnAppearing | (moveMode ? 0 : ImGuiWindowFlags.NoInputs);
        if (ImGui.Begin("##np_perf", flags))
        {
            ImGui.Text($"frame {frame.AvgMs:F2} ms ({frame.Fps:F0} fps)");
            ImGui.Text($"entities  considered {considered}  drawn {drawn}  culled {culled}");
            ImGui.Separator();
            foreach (var kv in prof.Stats.OrderBy(k => k.Key))
                ImGui.Text($"{kv.Key,-16} {kv.Value.LastMs,6:F2} {kv.Value.AvgMs,6:F2} {kv.Value.MaxMs,6:F2} {kv.Value.Calls,7}");
        }
        ImGui.End();
    }
}

public sealed class Profiler
{
    public sealed class Stat
    {
        public long Calls;
        public double LastMs;
        public double TotalMs;
        public double MaxMs;
        public double AvgMs => Calls > 0 ? TotalMs / Calls : 0;
    }

    private readonly Dictionary<string, Stat> _stats = new();
    private readonly Dictionary<string, long> _active = new();

    public bool Enabled { get; set; }
    public IReadOnlyDictionary<string, Stat> Stats => _stats;

    public void Begin(string name)
    {
        if (!Enabled) return;
        _active[name] = Stopwatch.GetTimestamp();
    }

    public void End(string name)
    {
        if (!Enabled || !_active.Remove(name, out var start)) return;
        var ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        if (!_stats.TryGetValue(name, out var st))
            _stats[name] = st = new Stat();
        st.Calls++;
        st.LastMs = ms;
        st.TotalMs += ms;
        if (ms > st.MaxMs) st.MaxMs = ms;
    }

    public void Reset() { _stats.Clear(); _active.Clear(); }
}

public sealed class TextureCache
{
    private readonly Dictionary<string, nint> _ids = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> FillNames { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> TrackNames { get; private set; } = Array.Empty<string>();

    public void Load(Graphics graphics, string textureDir)
    {
        _ids.Clear();
        foreach (var path in Directory.EnumerateFiles(textureDir, "*.png"))
        {
            var key = Path.GetFileNameWithoutExtension(path);
            if (graphics.InitImage(key, path))
                _ids[key] = graphics.GetTextureId(key);
        }
        FillNames = _ids.Keys.Where(n => n.StartsWith("fill_", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n).ToList();
        TrackNames = _ids.Keys.Where(n => n.StartsWith("bg_", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n).ToList();
    }

    public nint Get(string name) => name != null && _ids.TryGetValue(name, out var id) ? id : 0;
}
