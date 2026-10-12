using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileNameplates.Core;
using ExileNameplates.Rendering;
using ExileNameplates.Settings;
using RectangleF = SharpDX.RectangleF;
using Vector2N = System.Numerics.Vector2;

namespace ExileNameplates;

public class ExileNameplates : BaseSettingsPlugin<ExileNameplatesSettings>
{
    public readonly Profiler Prof = new();
    public readonly FrameTimer Frame = new();
    public readonly TextureCache Tex = new();
    public readonly List<Nameplate> FrameDraws = new();
    public readonly BuffCatalog Catalog = new();
    public readonly UnitFrameEngine Frames = new();

    private readonly Stopwatch _frameSw = new();
    public int Considered, Drawn, Culled;
    private bool _perfShown;
    private bool _moveMode;

    private readonly Dictionary<string, ConfiguredStatus> _statusByKey = new();
    private readonly HashSet<string> _present = new();
    private readonly Dictionary<(string, uint), int> _statusIndex = new();
    private readonly HashSet<string> _ignored = new();
    private readonly StylePatch _statusPatch = new();
    private int _statusVersion = -1;
    private bool _anyStateEnabled;

    public override bool Initialise()
    {
        Tex.Load(Graphics, Path.Combine(DirectoryFullName, "textures"));
        return true;
    }

    public override Job Tick()
    {
        _frameSw.Restart();
        FrameDraws.Clear();
        Considered = Drawn = Culled = 0;
        Prof.Enabled = Settings.Perf.ProfilerEnabled;

        if (_statusVersion != Settings.SettingsVersion)
        {
            _statusVersion = Settings.SettingsVersion;
            _statusByKey.Clear();
            foreach (var cs in Settings.Statuses)
                if (!string.IsNullOrEmpty(cs.Key)) _statusByKey[cs.Key] = cs;

            _anyStateEnabled = Settings.States.Hidden.Enabled
                            || Settings.States.Invuln.Enabled
                            || Settings.States.Cullable.Enabled;

            _ignored.Clear();
            foreach (var ip in Settings.IgnoredPaths)
                if (!string.IsNullOrEmpty(ip)) _ignored.Add(ip);
        }

        var ingame = GameController.IngameState;
        if (!Settings.Enable || ingame.Camera == null || ingame.IngameUi.FullscreenPanels.Any(x => x.IsVisible))
        {
            Frames.Reset();
            return null;
        }

        Prof.Begin("Tick.UnitFrames");
        Frames.Tick(Settings, GameController);
        Prof.End("Tick.UnitFrames");

        var area = GameController.Area.CurrentArea;
        if (area != null && (area.IsTown && !Settings.Global.ShowInTown || area.IsHideout && !Settings.Global.ShowInHideout))
            return null;

        var player = GameController.Player;
        var win = GameController.Window.GetWindowRectangle();
        var playerLevel = player?.GetComponent<Player>()?.Level ?? 1;
        Prof.Begin("Tick.Walk");
        foreach (var type in EntityClassifier.Types)
        {
            if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(type, out var list))
                continue;
            foreach (var e in list)
                ProcessEntity(e, player, win, playerLevel);
        }
        Prof.End("Tick.Walk");
        return null;
    }

    private void ProcessEntity(Entity e, Entity player, RectangleF win, int playerLevel)
    {
        Considered++;
        var np = e.GetHudComponent<Nameplate>();
        if (np == null)
        {
            np = new Nameplate(e);
            e.SetHudComponent(np);
        }
        e.TryGetComponent<Life>(out np.Life);
        e.TryGetComponent<Render>(out np.Render);
        e.TryGetComponent<Stats>(out np.Stats);
        if (!e.IsAlive) { Culled++; return; }

        if (e.Type == EntityType.Daemon && !e.IsTargetable) { Culled++; return; }

        if (Settings.Global.HideUntargetable && !e.IsTargetable) { Culled++; return; }

        if (_ignored.Contains(np.PathKey)) { Culled++; return; }

        var dist = e.DistancePlayer;
        if (dist > Settings.Global.MaxDistance) { Culled++; return; }

        np.Resolve(Settings, player);
        if (!np.Resolved.Show) { Culled++; return; }

        Prof.Begin("Tick.Position");
        var world = e.PosNum;
        if (np.Render != null) world.Z -= 2f * np.Render.BoundsNum.Z;
        var screen = GameController.IngameState.Camera.WorldToScreen(world);
        Prof.End("Tick.Position");

        var m = Settings.Global.OffScreenMargin;
        if (screen.X < win.Left - m || screen.X > win.Right + m ||
            screen.Y < win.Top - m || screen.Y > win.Bottom + m) { Culled++; return; }

        var life = np.Life;
        var isMonster = e.Type == EntityType.Monster;
        if (isMonster && np.XpForLevel != playerLevel)
        {
            np.XpForLevel = playerLevel;
            var variety = GameController.Files.MonsterVarieties.TranslateFromMetadata(e.Metadata);
            np.Xp = variety != null
                ? XpCalculator.RawXp(e.Path, variety.ExperienceMultiplier, playerLevel)
                : 0;
            np.XpPct = XpCalculator.XpPercent(np.Xp, playerLevel);
        }
        np.Snap = new Snapshot
        {
            Pos = new Vector2N(screen.X - np.Resolved.Bar.Width / 2f,
                               screen.Y + np.Resolved.Bar.YOffset),
            HpCur = life?.CurHP ?? 0, HpMax = life?.MaxHP ?? 0,
            EsCur = life?.CurES ?? 0, EsMax = life?.MaxES ?? 0,
            ManaCur = life?.CurMana ?? 0, ManaMax = life?.MaxMana ?? 0,
            Style = np.Resolved,
            Name = np.Render?.Name ?? np.FallbackName,
            Dist = dist,
            Rarity = np.RarityName,
            Type = np.TypeName,
            Xp = isMonster ? np.Xp : 0,
            XpPct = isMonster ? np.XpPct : 0f,
        };
        np.Snap.Dps = np.Dps.Update(np.Snap.HpCur + np.Snap.EsCur,
            Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, Settings.Global.DpsWindow);
        np.Buffs.Clear();
        np.Debuffs.Clear();
        _present.Clear();
        _statusIndex.Clear();
        var rs = np.Resolved;

        float dotPerMin = 0f;
        var sd = np.Stats?.StatDictionary;
        if (sd != null
            && sd.TryGetValue(GameStat.IsCurrentlyTakingDamageOverTime, out var dotFlag) && dotFlag == 1
            && sd.TryGetValue(GameStat.TotalDamageTakenPerMinuteToLife, out var perMin) && perMin > 0)
            dotPerMin = perMin;

        var buffs = dotPerMin > 0f || _statusByKey.Count > 0 ? e.Buffs : null;
        if (buffs != null && _statusByKey.Count > 0)
        {
            foreach (var buff in buffs)
            {
                if (buff == null || buff.IsInvisible) continue;
                var key = buff.BuffDefinition?.Id ?? buff.Name;
                if (key == null || !_statusByKey.TryGetValue(key, out var cfg)) continue;
                _present.Add(key);

                var isBuff = cfg.Category == StatusCategory.Buff;
                var block = isBuff ? rs.Buffs : rs.Debuffs;
                if (!block.Enabled) continue;

                ResolvedStatus.AddOrMerge(isBuff ? np.Buffs : np.Debuffs, _statusIndex, buff, key, block.MaxIcons, new ResolvedStatus
                {
                    Icon = cfg.Icon, Style = cfg.Style, Tint = cfg.Tint, BadgeTint = cfg.BadgeTint, Scale = cfg.Scale,
                });
            }
        }

        if (buffs != null && dotPerMin > 0f)
            (np.Snap.DotRate, np.Snap.DotLoss) = DotPredictor.Predict(dotPerMin, buffs, life?.CurHP ?? 0f, Settings.Global.DotDurations);

        var curStyle = np.Resolved;

        if (_present.Count > 0)
        {
            _statusPatch.Reset();
            foreach (var cs in Settings.Statuses)
                if (_present.Contains(cs.Key)) _statusPatch.FillFrom(cs.Patch);
            if (_statusPatch.HasAny)
            {
                curStyle = np.Effective(curStyle);
                _statusPatch.ApplyTo(curStyle);
            }
        }

        if (_anyStateEnabled)
        {
            var states = Settings.States;
            bool hidden;
            try { hidden = e.IsHidden; } catch { hidden = false; }
            var invuln = sd != null && sd.TryGetValue(GameStat.CannotBeDamaged, out var cbd) && cbd != 0;
            StateStyle st = states.Hidden.Enabled && hidden ? states.Hidden
                : states.Invuln.Enabled && invuln ? states.Invuln
                : states.Cullable.Enabled && !invuln && states.Cullable.IsCullable(np.Bucket, life?.HPPercentage ?? 1f) ? states.Cullable
                : null;
            if (st != null)
            {
                if (st.Suppress) { Culled++; return; }
                if (st.Patch.HasAny)
                {
                    curStyle = np.Effective(curStyle);
                    st.Patch.ApplyTo(curStyle);
                }
            }
        }

        np.Snap.Style = curStyle;
        FrameDraws.Add(np);
        Drawn++;
    }

    public override void DrawSettings() => SettingsPanel.Draw(this);

    public override void Render()
    {
        if (!Settings.Enable) { _frameSw.Stop(); return; }
        if (Settings.TogglePerfOverlay.PressedOnce()) _perfShown = !_perfShown;
        if (Settings.ToggleMoveMode.PressedOnce()) _moveMode = !_moveMode;
        var g = Settings.Global;
        Prof.Begin("Render.Bars");
        foreach (var np in FrameDraws)
            BarRenderer.Draw(Graphics, np.Snap, Tex);
        Prof.End("Render.Bars");
        Prof.Begin("Render.Text");
        foreach (var np in FrameDraws)
            DataTextRenderer.Draw(Graphics, np.Snap, g.NameAbbrevThreshold, g.TextOutline);
        Prof.End("Render.Text");
        Prof.Begin("Render.Buffs");
        foreach (var np in FrameDraws)
            BuffIconRenderer.Draw(Graphics, np.Snap, np.Buffs, np.Debuffs, Tex, g.TextOutline);
        Prof.End("Render.Buffs");
        Prof.Begin("Render.UnitFrames");
        UnitFrameRenderer.Draw(Graphics, GameController, Settings, Frames, Tex, _moveMode);
        Prof.End("Render.UnitFrames");
        PreviewOverlay.Draw(Graphics, Settings, Tex, _moveMode);

        if (g.ShowDebug)
            DebugOverlay.Draw(Graphics, GameController, Settings);

        _frameSw.Stop();
        Frame.Sample(_frameSw.Elapsed.TotalMilliseconds);

        if (_perfShown)
        {
            var pos = Settings.Perf.OverlayPos;
            PerfOverlay.Draw(Prof, Frame, Considered, Drawn, Culled, ref pos, _moveMode);
            Settings.Perf.OverlayPos = pos;
        }
    }
}
