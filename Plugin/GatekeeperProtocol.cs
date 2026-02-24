// =============================================================================
// GatekeeperProtocol.cs — M1 Plugin
// Gatekeeper Protocol — Hacknet Mod
//
// M1 scope:
//   - Register 6 custom PFPorts (ssh_v2/ftp_v2/web_v2, ssh_v3/ftp_v3/web_v3)
//   - GPCrackBase (unified) + 6 concrete crackers, tier-based timing + color
//   - gp_debug command
//
// Crack tiers — base solve time (divided by CPU multiplier):
//   V2 (tier 2): 10s | orange bar | no key file
//   V3 (tier 3): 15s | cyan bar   | requires <port>_v3_key.dat in /home
//
// Adding a new tier: add a concrete class inheriting GPCrackBase,
//   pass the next tier number, adjust BASE_SOLVE_TIME array.
//
// Port naming: Pathfinder uses "web" for HTTP (not "http").
// V3 key files: ssh_v3_key.dat, ftp_v3_key.dat, web_v3_key.dat (in player /home)
//
// Hardware flags (read-only in M1, upgraded in M3):
//   CPU — cpu_t2/t3/t4 : crack speed multiplier (1.0x / 1.5x / 2.25x / 3.0x)
//   RAM — ram_t2/t3/t4 : process slot capacity
//   HDD — hdd_t2/t3/t4 : inventory size
//   NIC — nic_t2/t3/t4 : trace time modifier + upload/download speed
// =============================================================================

using BepInEx;
using BepInEx.Hacknet;
using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Pathfinder.Command;
using Pathfinder.Executable;
using Pathfinder.Port;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace GatekeeperProtocol
{
    [BepInPlugin("com.gatekeeper.protocol", "Gatekeeper Protocol", "0.2.0")]
    public class GatekeeperPlugin : HacknetPlugin
    {
        public static GatekeeperPlugin Instance;

        public override bool Load()
        {
            Instance = this;

            // All tiers share the same underlying port numbers (22/21/80).
            // Tier is encoded in the protocol name, not the number.
            PortManager.RegisterPort("ssh_v2", "SSH V2", 22);
            PortManager.RegisterPort("ftp_v2", "FTP V2", 21);
            PortManager.RegisterPort("web_v2", "Web V2", 80);
            PortManager.RegisterPort("ssh_v3", "SSH V3", 22);
            PortManager.RegisterPort("ftp_v3", "FTP V3", 21);
            PortManager.RegisterPort("web_v3", "Web V3", 80);

            Log.LogInfo("[GP] Registered 6 custom ports: ssh_v2/ftp_v2/web_v2 (22/21/80), ssh_v3/ftp_v3/web_v3 (22/21/80)");

            ExecutableManager.RegisterExecutable<GPSSHCrackV2>("#SSH_V2#");
            ExecutableManager.RegisterExecutable<GPFTPCrackV2>("#FTP_V2#");
            ExecutableManager.RegisterExecutable<GPWebCrackV2>("#WEB_V2#");
            ExecutableManager.RegisterExecutable<GPSSHCrackV3>("#SSH_V3#");
            ExecutableManager.RegisterExecutable<GPFTPCrackV3>("#FTP_V3#");
            ExecutableManager.RegisterExecutable<GPWebCrackV3>("#WEB_V3#");

            Log.LogInfo("[GP] Registered 6 executables: SSH/FTP/WEB_V2, SSH/FTP/WEB_V3");

            CommandManager.RegisterCommand("gp_debug",      GpDebugCommand,      addAutocomplete: false);
            CommandManager.RegisterCommand("gp_drawtest",  GpDrawTestCommand,   addAutocomplete: false);
            CommandManager.RegisterCommand("gp_resetports", GpResetPortsCommand, addAutocomplete: false);

            Log.LogInfo("[GP] M1 plugin loaded. Commands: gp_debug, gp_drawtest, gp_resetports");
            return true;
        }

        public override bool Unload()
        {
            Log.LogInfo("[GP] Gatekeeper Protocol unloaded.");
            return true;
        }

        private static void GpDebugCommand(OS os, string[] args)
        {
            os.write("");
            os.write("[GP] ===== GATEKEEPER PROTOCOL DEBUG =====");
            os.write("");
            os.write("CPU  : T" + HardwareState.CpuTier(os) + " (" + HardwareState.CpuMultiplier(os).ToString("F2") + "x)");
            os.write("RAM  : T" + HardwareState.RamTier(os) + " [" + os.totalRam + " MB]");
            os.write("HDD  : T" + HardwareState.HddTier(os) + " [M3]");
            os.write("NIC  : T" + HardwareState.NicTier(os) + " [M3]");
            os.write("CRED : [M3]");
            os.write("");

            try
            {
                var tt = os.traceTracker;
                if (tt.active)
                    os.write("TRACE: ACTIVE - " + (tt.startingTimer - tt.timer).ToString("F1") + "s remaining");
                else
                    os.write("TRACE: inactive");
            }
            catch { os.write("TRACE: [unavailable]"); }

            if (os.connectedComp != null)
            {
                var c = os.connectedComp;
                os.write("");
                os.write("Node : " + c.name + " (" + c.ip + ")");
                os.write("Trace: " + c.traceTime + "s max");
                os.write("");
                os.write("Ports (all):");

                // Vanilla integer ports
                int[] vanillaPorts = new[] { 22, 21, 80 };
                foreach (int pn in vanillaPorts)
                {
                    bool open = c.isPortOpen(pn);
                    os.write("  :" + pn.ToString().PadRight(5)
                        + " [" + (open ? "OPEN  " : "CLOSED") + "]  vanilla");
                }

                // GP PF ports
                bool hasGpPorts = false;
                foreach (var ps in c.GetAllPortStates())
                {
                    var proto = ps.Record.Protocol;
                    if (!proto.EndsWith("_v2") && !proto.EndsWith("_v3")) continue;
                    if (!hasGpPorts) { os.write(""); hasGpPorts = true; }
                    string tier    = proto.EndsWith("_v3") ? "T3" : "T2";
                    int    portNum = proto.StartsWith("ssh") ? 22 : proto.StartsWith("ftp") ? 21 : 80;
                    os.write("  " + (proto + ":" + portNum).PadRight(14)
                        + " [" + (ps.Cracked ? "OPEN  " : "CLOSED") + "]  " + tier);
                }
                if (!hasGpPorts) os.write("  (no GP PF ports on this node)");

                os.write("");
                os.write("gp_resetports       close all ports on this node");
                os.write("gp_resetports <ip>  close all ports on any node");
            }
            else
            {
                os.write("Node : [not connected]");
            }

            os.write("");
            os.write("[GP] ==========================================");
            os.write("");
        }

        // ------------------------------------------------------------------
        // gp_resetports [ip]
        // Closes all vanilla + GP PF ports on the connected node (or <ip>).
        // Use between crack tests to reset port state without reloading.
        // ------------------------------------------------------------------
        private static void GpResetPortsCommand(OS os, string[] args)
        {
            Computer target;
            if (args.Length >= 2)
            {
                target = Programs.getComputer(os, args[1]);
                if (target == null)
                {
                    os.write("[GP] resetports: no node found at '" + args[1] + "'");
                    os.write("[GP] Usage: gp_resetports [ip]");
                    return;
                }
            }
            else if (os.connectedComp != null)
            {
                target = os.connectedComp;
            }
            else
            {
                os.write("[GP] resetports: not connected and no IP given.");
                os.write("[GP] Usage: gp_resetports [ip]");
                return;
            }

            string callerIp = os.thisComputer.ip;
            int closed = 0;

            // Vanilla ports
            foreach (int portNum in new[] { 22, 21, 80 })
            {
                try { target.closePort(portNum, callerIp); closed++; }
                catch (Exception ex)
                {
                    GatekeeperPlugin.Instance?.Log.LogWarning(
                        "[GP] closePort(" + portNum + "): " + ex.Message);
                }
            }

            // GP PF ports — only attempt ports that exist on this node
            foreach (string proto in new[] { "ssh_v2","ftp_v2","web_v2","ssh_v3","ftp_v3","web_v3" })
            {
                bool exists = false;
                foreach (var ps in target.GetAllPortStates())
                    if (ps.Record.Protocol == proto) { exists = true; break; }
                if (!exists) continue;
                try { target.closePort(proto, callerIp); closed++; }
                catch (Exception ex)
                {
                    GatekeeperPlugin.Instance?.Log.LogWarning(
                        "[GP] closePort(" + proto + "): " + ex.Message);
                }
            }

            os.write("[GP] " + target.name + " (" + target.ip + "): "
                + closed + " port(s) reset.");
            os.write("[GP] Run 'probe' or 'gp_debug' to verify.");
            GatekeeperPlugin.Instance?.Log.LogInfo(
                "[GP] gp_resetports: closed " + closed + " ports on " + target.ip);
        }

        private static void GpDrawTestCommand(OS os, string[] args)
        {
            // gp_drawtest header <n> — shift matrix start Y below the IdentifierName label.
            // CharW/CharH are auto-measured from GuiData.tinyfont — not tunable here.
            if (args.Length >= 3 && args[1].ToLower() == "header" && int.TryParse(args[2], out int val))
            {
                DrawTestParams.HeaderH = val;
                os.write("[GP] headerH = " + val + " — updates on next draw frame");
                return;
            }

            os.write("");
            os.write("[GP] === DRAW TEST ===");
            os.write("headerH = " + DrawTestParams.HeaderH
                     + "  (charW/charH auto-measured from tinyfont)");
            os.write("");
            os.write("Spawn a looping test cracker (no node connection needed):");
            os.write("  SSHcrack_v2 --test");
            os.write("  SSHcrack_v3 --test");
            os.write("");
            os.write("Tune header offset live (no rebuild needed):");
            os.write("  gp_drawtest header <n>   shift matrix below label (try 16-40)");
            os.write("");
        }
    }

    // =========================================================================
    // DRAW TEST PARAMS — live-tunable layout constants
    // Adjust in-game with gp_drawtest, then bake into defaults once happy.
    // =========================================================================
    internal static class DrawTestParams
    {
        // All values editable live in BepInEx/plugins/gp_drawtest.cfg — no rebuild needed.
        // Changes apply within 1 second while Hacknet is running.
        public static int    HeaderH     = 28;    // px below module top where matrix starts
        public static int    TargetRows  = 6;     // rows the animation always tries to show
        public static int    RamCostV2   = 256;   // module height ∝ ramCost/playerRam
        public static int    RamCostV3   = 384;
        public static int    AccentH     = 3;     // accent bar height (px) at bottom of header
        public static int    LabelOffset = 11;    // header text baseline: HEADER_H - LabelOffset
        // Draw style per exe family. Valid values: "matrix" | "packets"
        // matrix  — random threshold hex grid (same as vanilla SSHcrack)
        // packets — rows complete top-to-bottom, columns left-to-right (FTP feel)
        public static string SshStyle    = "matrix";
        public static string FtpStyle    = "packets";
        public static string WebStyle    = "matrix";

        private static string   _cfgPath  = null;
        private static DateTime _lastRead = DateTime.MinValue;
        private static float    _acc      = 0f;

        public static void TryHotReload(float dt)
        {
            _acc += dt;
            if (_acc < 1.0f) return;
            _acc = 0f;

            if (_cfgPath == null)
                _cfgPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "gp_drawtest.cfg");

            if (!File.Exists(_cfgPath)) return;
            var modified = File.GetLastWriteTimeUtc(_cfgPath);
            if (modified <= _lastRead) return;
            _lastRead = modified;

            try
            {
                foreach (var raw in File.ReadAllLines(_cfgPath))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("#") || !line.Contains("=")) continue;
                    var kv  = line.Split(new[] { '=' }, 2);
                    var key = kv[0].Trim().ToLowerInvariant();
                    var val = kv[1].Trim();
                    // String values handled first
                    if (key == "sshstyle") { SshStyle = val.ToLowerInvariant(); continue; }
                    if (key == "ftpstyle") { FtpStyle = val.ToLowerInvariant(); continue; }
                    if (key == "webstyle") { WebStyle = val.ToLowerInvariant(); continue; }
                    // Int values
                    if (!int.TryParse(val, out int v)) continue;
                    if (key == "headerh")      HeaderH     = Math.Max(16, v);
                    if (key == "targetrows")   TargetRows  = Math.Max(1,  v);
                    if (key == "ramcostv2")    RamCostV2   = Math.Max(1,  v);
                    if (key == "ramcostv3")    RamCostV3   = Math.Max(1,  v);
                    if (key == "accenth")      AccentH     = Math.Max(1,  v);
                    if (key == "labeloffset")  LabelOffset = Math.Max(1,  v);
                }
                GatekeeperPlugin.Instance?.Log.LogInfo(
                    "[GP] cfg reloaded — headerH=" + HeaderH
                    + " accentH=" + AccentH + " labelOffset=" + LabelOffset
                    + " rows=" + TargetRows
                    + " ramV2=" + RamCostV2 + " ramV3=" + RamCostV3
                    + " ssh=" + SshStyle + " ftp=" + FtpStyle + " web=" + WebStyle);
            }
            catch (Exception ex)
            {
                GatekeeperPlugin.Instance?.Log.LogWarning("[GP] cfg read error: " + ex.Message);
            }
        }
    }

    // =========================================================================
    // HARDWARE STATE
    // =========================================================================
    public static class HardwareState
    {
        public static int CpuTier(OS os)
        {
            if (os.Flags.HasFlag("cpu_t4")) return 4;
            if (os.Flags.HasFlag("cpu_t3")) return 3;
            if (os.Flags.HasFlag("cpu_t2")) return 2;
            return 1;
        }
        public static float CpuMultiplier(OS os)
        {
            if (os.Flags.HasFlag("cpu_t4")) return 3.0f;
            if (os.Flags.HasFlag("cpu_t3")) return 2.25f;
            if (os.Flags.HasFlag("cpu_t2")) return 1.5f;
            return 1.0f;
        }
        public static int RamTier(OS os)
        {
            if (os.Flags.HasFlag("ram_t4")) return 4;
            if (os.Flags.HasFlag("ram_t3")) return 3;
            if (os.Flags.HasFlag("ram_t2")) return 2;
            return 1;
        }
        public static int HddTier(OS os)
        {
            if (os.Flags.HasFlag("hdd_t4")) return 4;
            if (os.Flags.HasFlag("hdd_t3")) return 3;
            if (os.Flags.HasFlag("hdd_t2")) return 2;
            return 1;
        }
        public static int NicTier(OS os)
        {
            if (os.Flags.HasFlag("nic_t4")) return 4;
            if (os.Flags.HasFlag("nic_t3")) return 3;
            if (os.Flags.HasFlag("nic_t2")) return 2;
            return 1;
        }
    }

    // =========================================================================
    // GP MINI-GAMES  (V3 crackers only — SPACE-bar interaction)
    //
    // Transliterated from the drawtest.html MINIGAMES section.
    // Each mini-game drives Progress 0→1; IsComplete triggers port open.
    //
    //   GPSignalSync      — SSH V3  — oscillating bar, SPACE on center zone (5 syncs)
    //   GPPacketSort      — FTP V3  — falling packets, SPACE to catch target (10 catches)
    //   GPInjectionTiming — Web V3  — scrolling HTTP, SPACE on EXPLOIT band  (10 hits)
    // =========================================================================

    internal abstract class GPMiniGame
    {
        public float Progress    { get; protected set; }
        public bool  IsComplete  => Progress >= 1f;

        protected static readonly Random _rng = new Random();
        protected Rectangle _b;   // module Bounds (set in Init)
        protected int       _hH;  // header height in px (set in Init)

        public abstract void Init(Rectangle bounds, int headerH);
        public abstract void Update(float dt, bool spaceJustPressed);
        public abstract void Draw(Color accent);

        // ── drawing helpers ──────────────────────────────────────────────
        protected void Rect(int x, int y, int w, int h, Color c)
            => GuiData.spriteBatch.Draw(Utils.white, new Rectangle(x, y, w, h), c);

        protected void TextC(string s, int cx, int y, Color c)
        {
            var sz = GuiData.tinyfont.MeasureString(s);
            GuiData.spriteBatch.DrawString(GuiData.tinyfont, s,
                new Vector2(cx - sz.X / 2f, y), c);
        }

        protected void Text(string s, int x, int y, Color c)
            => GuiData.spriteBatch.DrawString(GuiData.tinyfont, s, new Vector2(x, y), c);
    }

    // ────────────────────────────────────────────────────────────────────
    // SSH V3 · Signal Sync
    // Indicator oscillates on a horizontal track.
    // Press SPACE when it aligns with the center zone. 5 syncs = done.
    // ────────────────────────────────────────────────────────────────────
    internal sealed class GPSignalSync : GPMiniGame
    {
        private float _sPhase, _speed, _accel;
        private int   _synced;
        private const int TOTAL_SYNCS = 5;
        private float _cooldown, _flashTimer;
        private bool  _flashGood, _won;

        public override void Init(Rectangle bounds, int headerH)
        {
            _b = bounds; _hH = headerH;
            _sPhase = 0f;
            _speed  = 1.8f + (float)_rng.NextDouble() * 0.8f;
            _accel  = 0f;
            _synced = 0;
            _cooldown = _flashTimer = 0f;
            _won = false;
            Progress = 0f;
        }

        private int BarW   => _b.Width - 32;
        private int ZoneW  => Math.Max(20, BarW / 6);
        private int Travel => BarW / 2 - ZoneW / 2;

        private bool InZone() => Math.Abs((float)Math.Sin(_sPhase) * Travel) < ZoneW / 2;

        public override void Update(float dt, bool space)
        {
            if (_won) return;
            _accel += dt * ((float)_rng.NextDouble() - 0.5f) * 0.4f;
            _accel  = Math.Max(-0.5f, Math.Min(0.5f, _accel));
            _speed  = Math.Max(1.2f, Math.Min(3.5f, _speed + _accel * dt));
            _sPhase += _speed * dt;
            if (_flashTimer > 0) _flashTimer -= dt;
            if (_cooldown   > 0) _cooldown   -= dt;

            if (space && _cooldown <= 0)
            {
                _cooldown = 0.35f;
                if (InZone()) { _synced++; _flashGood = true;  _flashTimer = 0.45f; }
                else          {            _flashGood = false; _flashTimer = 0.30f; }
            }
            Progress = Math.Min(_synced / (float)TOTAL_SYNCS, 1f);
            if (_synced >= TOTAL_SYNCS) _won = true;
        }

        public override void Draw(Color accent)
        {
            int barX  = _b.X + 16;
            int barW  = BarW;
            int barH  = 22;
            int cH    = _b.Height - _hH;
            int barY  = _b.Y + _hH + cH / 2 - barH / 2;
            int zoneW = ZoneW;
            int cx    = barX + barW / 2;
            int travel = Travel;
            int indX  = cx + (int)((float)Math.Sin(_sPhase) * travel);
            bool inZone = InZone();
            bool fl     = _flashTimer > 0;

            // Track background
            Rect(barX, barY, barW, barH, new Color(13, 13, 13));

            // Target zone highlight
            int zoneX = cx - zoneW / 2;
            Color zoneC = fl
                ? (_flashGood ? new Color(0, 50, 20, 80) : new Color(60, 10, 10, 60))
                : new Color(55, 50, 0, 25);
            Rect(zoneX, barY + 1, zoneW, barH - 2, zoneC);

            // Indicator glow
            int glowR = fl ? 12 : inZone ? 10 : 7;
            Color glowC = fl
                ? (_flashGood ? new Color(0, 255, 130, 80)  : new Color(255, 60, 60, 60))
                : (inZone     ? new Color(0, 200, 80, 50)   : new Color(200, 120, 0, 40));
            Rect(indX - glowR, barY + barH / 2 - glowR, glowR * 2, glowR * 2, glowC);

            // Indicator dot
            int dotR  = 5;
            Color dotC = fl
                ? (_flashGood ? new Color(0, 255, 130)   : new Color(255, 60, 60))
                : (inZone     ? new Color(120, 255, 170) : accent);
            Rect(indX - dotR, barY + barH / 2 - dotR, dotR * 2, dotR * 2, dotC);

            // Sync pips
            int pipW  = Math.Max(10, (barW - TOTAL_SYNCS * 3) / TOTAL_SYNCS);
            int pipH  = 7, pipGap = 3;
            int tpW   = TOTAL_SYNCS * (pipW + pipGap) - pipGap;
            int pipX  = _b.X + _b.Width / 2 - tpW / 2;
            int pipY  = barY + barH + 12;
            for (int i = 0; i < TOTAL_SYNCS; i++)
            {
                Color pc = i < _synced ? new Color(0, 200, 100) : new Color(25, 25, 25);
                Rect(pipX + i * (pipW + pipGap), pipY, pipW, pipH, pc);
            }

            // Hint text
            string hint = fl ? (_flashGood ? "SYNC" : "MISS") : "[SPACE]";
            Color hintC = fl
                ? (_flashGood ? new Color(0, 255, 130) : new Color(255, 60, 60))
                : new Color(60, 60, 60);
            TextC(hint, _b.X + _b.Width / 2, barY - 13, hintC);

            if (_won)
            {
                Rect(_b.X, _b.Y + _hH, _b.Width, _b.Height - _hH, new Color(0, 0, 0, 180));
                TextC("PORT OPEN",            _b.X + _b.Width / 2, _b.Y + _b.Height / 2 - 7, accent);
                TextC("handshake established", _b.X + _b.Width / 2, _b.Y + _b.Height / 2 + 5,
                      new Color(0, 200, 100));
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // FTP V3 · Packet Sort
    // Packets fall in a centre column. TARGET shown in header.
    // Press SPACE to catch TARGET packets in the catch zone.
    // Catching a wrong packet = penalty flash. 10 correct catches = done.
    // ────────────────────────────────────────────────────────────────────
    internal sealed class GPPacketSort : GPMiniGame
    {
        private sealed class Packet
        {
            public float  Y;
            public string Label;
            public bool   IsTarget;
            public bool   Dead;
        }

        private static readonly string HEX16 = "0123456789ABCDEF";
        private static char RndH() => HEX16[_rng.Next(16)];

        private List<Packet> _packets;
        private string _target;
        private float  _spawnTimer, _spawnInterval, _speed;
        private int    _caught;
        private const int NEEDED = 10;
        private float  _penaltyFlash, _hitFlash, _cooldown;
        private bool   _won;

        private const int PKT_H   = 12;
        private const int CATCH_T = 14;

        private int ColW      => Math.Min(72, _b.Width - 24);
        private int ColX      => _b.X + _b.Width / 2 - ColW / 2;
        private int SpawnY    => _b.Y + _hH + 14;
        private int CatchTop  => _b.Y + _b.Height - CATCH_T;

        private string RndLabel() => "[" + RndH() + RndH() + "]";

        public override void Init(Rectangle bounds, int headerH)
        {
            _b = bounds; _hH = headerH;
            _target       = RndLabel();
            _packets      = new List<Packet>();
            _spawnTimer   = 0f; _spawnInterval = 0.65f; _speed = 36f;
            _caught       = 0;
            _penaltyFlash = _hitFlash = _cooldown = 0f;
            _won     = false;
            Progress = 0f;
        }

        private Packet SpawnPacket()
        {
            bool   isTarget = _rng.NextDouble() < 0.38;
            string label    = isTarget ? _target : RndLabel();
            if (!isTarget && label == _target) label = RndLabel();
            return new Packet { Y = SpawnY, Label = label, IsTarget = isTarget };
        }

        public override void Update(float dt, bool space)
        {
            if (_won) return;
            if (_penaltyFlash > 0) _penaltyFlash -= dt;
            if (_hitFlash     > 0) _hitFlash     -= dt;
            if (_cooldown     > 0) _cooldown     -= dt;

            _spawnTimer += dt;
            if (_spawnTimer >= _spawnInterval)
            {
                _spawnTimer    = 0f;
                _spawnInterval = Math.Max(0.38f, 0.65f - _caught * 0.015f);
                bool tooClose  = false;
                foreach (var p in _packets)
                    if (!p.Dead && p.Y < SpawnY + PKT_H * 2) { tooClose = true; break; }
                if (!tooClose) _packets.Add(SpawnPacket());
            }

            foreach (var p in _packets)
            {
                p.Y += _speed * dt;
                if (p.Y > _b.Y + _b.Height) p.Dead = true;
            }
            _packets.RemoveAll(p => p.Dead);
            _speed = 36f + _caught * 1.2f;

            if (space && _cooldown <= 0)
            {
                _cooldown = 0.15f;
                Packet best = null;
                foreach (var p in _packets)
                    if (!p.Dead && p.Y + PKT_H >= CatchTop && p.Y <= _b.Y + _b.Height)
                        if (best == null || p.Y > best.Y) best = p;
                if (best != null)
                {
                    best.Dead = true;
                    if (best.IsTarget) { _caught++;   _hitFlash     = 0.25f; }
                    else               {              _penaltyFlash = 0.30f; }
                }
            }

            Progress = Math.Min(_caught / (float)NEEDED, 1f);
            if (_caught >= NEEDED) _won = true;
        }

        public override void Draw(Color accent)
        {
            bool flHit = _hitFlash > 0, flPen = _penaltyFlash > 0;
            int cx = ColX, cw = ColW;

            // Header strip
            Rect(_b.X, _b.Y + _hH, _b.Width, 13, new Color(12, 12, 12));
            Text("TARGET:", _b.X + 4, _b.Y + _hH + 2, new Color(60, 60, 60));
            Text(_target,   _b.X + 52, _b.Y + _hH + 2,
                 flPen ? new Color(255, 60, 60) : accent);
            string countStr = _caught + "/" + NEEDED;
            var csSz = GuiData.tinyfont.MeasureString(countStr);
            Text(countStr, (int)(_b.X + _b.Width - csSz.X - 4), _b.Y + _hH + 2,
                 new Color(50, 50, 50));

            // Column background
            Rect(cx, _b.Y + _hH + 13, cw, _b.Height - _hH - 13, new Color(10, 10, 10));

            // Catch zone
            Color czC = flHit ? new Color(0, 140, 220, 46)
                      : flPen ? new Color(255, 60,  60, 30)
                      :         new Color(0, 140, 220, 15);
            Rect(cx + 1, CatchTop, cw - 2, CATCH_T - 1, czC);

            // Falling packets
            foreach (var p in _packets)
            {
                if (p.Dead) continue;
                int py = (int)p.Y;
                Color bg = p.IsTarget
                    ? new Color(accent.R, accent.G, accent.B, 28)
                    : new Color(12, 12, 12);
                Rect(cx + 2, py, cw - 4, PKT_H, bg);
                bool inZone = py + PKT_H >= CatchTop && py <= _b.Y + _b.Height;
                Color tc = p.IsTarget
                    ? (inZone ? accent : new Color(accent.R, accent.G, accent.B, 160))
                    : new Color(34, 34, 34);
                var lSz = GuiData.tinyfont.MeasureString(p.Label);
                Text(p.Label, (int)(cx + cw / 2 - lSz.X / 2), py + 1, tc);
            }

            // Flash labels
            if (flHit) TextC("CATCH", cx + cw / 2, CatchTop - 11, new Color(0,   200, 255, 180));
            if (flPen) TextC("WRONG", cx + cw / 2, CatchTop - 11, new Color(255,  80,  80, 180));

            // SPACE hint in catch zone
            TextC("[SPACE]", cx + cw / 2, CatchTop + 3,
                  flHit ? accent : flPen ? new Color(255, 60, 60) : new Color(35, 35, 35));

            if (_won)
            {
                Rect(_b.X, _b.Y + _hH, _b.Width, _b.Height - _hH, new Color(0, 0, 0, 190));
                TextC("PORT OPEN",           _b.X + _b.Width / 2, _b.Y + _b.Height / 2 - 7, accent);
                TextC(NEEDED + " pkts routed", _b.X + _b.Width / 2, _b.Y + _b.Height / 2 + 5,
                      new Color(0, 200, 100));
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // Web V3 · Injection Timing
    // HTTP headers scroll upward. A >>> EXPLOIT_PAYLOAD <<< line cycles.
    // Press SPACE when it crosses the yellow injection band.
    // 10 successful injections = done.
    // ────────────────────────────────────────────────────────────────────
    internal sealed class GPInjectionTiming : GPMiniGame
    {
        private static readonly string[] LINES =
        {
            "GET / HTTP/1.1", "Host: target.node", "Connection: keep-alive",
            "Accept: */*",    "X-GP-CRACK: v3",    "X-Port: 80",
            ">>> EXPLOIT_PAYLOAD <<<",
            "Cookie: sess=inject", "Pragma: no-cache", "X-Tier: 3", "Content-Length: 0",
        };
        private const int EXPLOIT_IDX = 6;
        private const int LINE_H      = 11;
        private const int BAND_H      = 14;

        private float _scrollY, _speed;
        private int   _hits, _misses;
        private const int TOTAL_HITS = 10;
        private float _flashTimer, _cooldown;
        private bool  _flashGood, _won;

        public override void Init(Rectangle bounds, int headerH)
        {
            _b = bounds; _hH = headerH;
            _scrollY = 0f; _speed = 38f;
            _hits = _misses = 0;
            _flashTimer = _cooldown = 0f;
            _won = false; Progress = 0f;
        }

        private float ExploitScreenY()
        {
            int total  = LINES.Length * LINE_H;
            int startY = _b.Y + _hH + 16;
            float scroll = _scrollY % total;
            return startY + ((EXPLOIT_IDX * LINE_H - scroll + total * 2) % total);
        }

        private int BandY()
        {
            int startY   = _b.Y + _hH + 16;
            int contentH = _b.Height - _hH - 16;
            return startY + (int)(contentH * 0.55f);
        }

        public override void Update(float dt, bool space)
        {
            if (_won) return;
            _scrollY += _speed * dt;
            if (_flashTimer > 0) _flashTimer -= dt;
            if (_cooldown   > 0) _cooldown   -= dt;
            _speed = 38f + _hits * 2f;

            if (space && _cooldown <= 0)
            {
                _cooldown = 0.3f;
                float ey = ExploitScreenY();
                int   by = BandY();
                if (Math.Abs(ey - by) < BAND_H / 2 + LINE_H / 2)
                { _hits++;   _flashGood = true;  _flashTimer = 0.35f; }
                else
                { _misses++; _flashGood = false; _flashTimer = 0.25f; }
            }

            Progress = Math.Min(_hits / (float)TOTAL_HITS, 1f);
            if (_hits >= TOTAL_HITS) _won = true;
        }

        public override void Draw(Color accent)
        {
            bool fl    = _flashTimer > 0;
            int  bandY = BandY();
            int  total = LINES.Length * LINE_H;

            // Injection band
            Color bandC = fl
                ? (_flashGood ? new Color(255, 220, 0, 100) : new Color(255, 60, 60, 40))
                : new Color(255, 220, 0, 20);
            Rect(_b.X, bandY - BAND_H / 2, _b.Width, BAND_H, bandC);

            // Scrolling lines (3 passes for seamless wrap)
            for (int i = 0; i < LINES.Length * 3; i++)
            {
                var   line   = LINES[i % LINES.Length];
                float rawY   = _b.Y + _hH + 16 + (i * LINE_H - _scrollY % total);
                if (rawY < _b.Y + _hH || rawY > _b.Y + _b.Height + LINE_H) continue;
                bool  isExpl = (i % LINES.Length) == EXPLOIT_IDX;
                bool  inBand = isExpl && Math.Abs(rawY - bandY) < BAND_H / 2 + LINE_H / 2;
                Color lineC  = isExpl
                    ? (inBand ? new Color(255, 255, 160)
                              : new Color(accent.R, accent.G, accent.B, 220))
                    : new Color(80, 80, 95, 160);
                Text(line, _b.X + 6, (int)rawY, lineC);
            }

            // Header strip (painted over scrolling lines so they don't bleed)
            Rect(_b.X, _b.Y + _hH, _b.Width, 15, new Color(10, 10, 10));
            Text("HITS " + _hits + "/" + TOTAL_HITS,
                 _b.X + 5, _b.Y + _hH + 2, new Color(60, 60, 60));
            if (_misses > 0)
                Text("MISS " + _misses, _b.X + 64, _b.Y + _hH + 2, new Color(90, 30, 30));
            var sSz = GuiData.tinyfont.MeasureString("[SPACE]");
            Text("[SPACE]", (int)(_b.X + _b.Width - sSz.X - 4), _b.Y + _hH + 2,
                 new Color(50, 50, 50));

            // Hit/miss flash
            if (fl)
                TextC(_flashGood ? "HIT" : "MISS", _b.X + _b.Width / 2, bandY + 2,
                      _flashGood ? new Color(0, 255, 150, 200) : new Color(255, 80, 80, 180));

            if (_won)
            {
                Rect(_b.X, _b.Y + _hH, _b.Width, _b.Height - _hH, new Color(0, 0, 0, 190));
                TextC("PORT OPEN",
                      _b.X + _b.Width / 2, _b.Y + _b.Height / 2 - 7, accent);
                TextC(TOTAL_HITS + " payloads injected",
                      _b.X + _b.Width / 2, _b.Y + _b.Height / 2 + 5, new Color(0, 200, 100));
            }
        }
    }

    // =========================================================================
    // GP CRACK BASE — unified single-inheritance crack executable
    //
    // To add a new tier: create a concrete class, pass the next tier number.
    // BASE_SOLVE_TIME and TIER_* arrays auto-index on tier.
    //
    // Tier 2 (V2): 10s | orange | no key file
    // Tier 3 (V3): 15s | cyan   | requires <port>_v3_key.dat in player /home
    // =========================================================================
    public abstract class GPCrackBase : BaseExecutable
    {
        // Base solve time in seconds per tier (index = tier number).
        private static readonly float[] BASE_SOLVE_TIME = { 0f, 0f, 10.0f, 15.0f };

        // Progress bar fill color per tier.
        private static readonly Color[] TIER_BAR = {
            Color.White, Color.White,
            new Color(200, 120,   0),   // V2 orange
            new Color(  0, 180, 220),   // V3 cyan
        };

        // Label text color per tier.
        private static readonly Color[] TIER_LABEL = {
            Color.White, Color.White,
            new Color(255, 180,  80),   // V2 amber
            new Color( 80, 200, 255),   // V3 sky
        };

        protected string portName;   // mutable: tier-up fallback may escalate (e.g. ssh_v2 -> ssh_v3)
        protected readonly int portNumber;
        protected readonly int    tier;
        protected readonly string keyFileName; // null = no gate
        protected float elapsed;
        protected bool  initialized;

        // Pass --test as an argument (e.g. "SSHcrack_v2 --test") to enter test mode:
        //   - skips all port/target checks so no connected node is needed
        //   - animation loops continuously instead of completing
        //   - combine with gp_drawtest to tune layout constants live
        private readonly bool   _testMode;    // --test: one cycle, then close (no port side-effects)
        private readonly bool   _loopMode;    // --infinity: loop forever (layout tuning)
        private readonly string _crackerFamily; // "ssh" | "ftp" | "web" — selects draw style

        // Character matrix state
        private static readonly Random _rng      = new Random();
        private static readonly string HEX_CHARS = "0123456789ABCDEF";
        private char[,]  _grid;
        private float[,] _threshold;
        private float    _drawTimer;
        private static bool _fontLogged = false; // log real font metrics once per session

        // Mini-game (V3 crackers only — null for V2)
        private GPMiniGame _miniGame;
        private bool       _prevSpace;  // previous-frame SPACE state for edge detection

        // displayName    — what shows in the RAM panel (e.g. "SSHcrack_v2")
        // port           — V2: vanilla protocol ("ssh"),   V3: PF port name ("ssh_v3")
        // portNum        — V2: vanilla port number (22),   V3: PF port number (20022)
        // crackerFamily  — "ssh" | "ftp" | "web" → drives draw style selection from cfg
        // tier == 1      → vanilla port API (int overloads) — reserved for future T1 custom crackers
        // tier >= 2      → Pathfinder PF API (string overloads) — V2 and V3 both use PF protocol names
        protected GPCrackBase(Rectangle location, OS os, string[] args,
                              string port, int portNum, int tier,
                              string displayName, string crackerFamily, string keyFile = null)
            : base(location, os, args)
        {
            portName        = port;
            portNumber      = portNum;
            this.tier       = tier;
            keyFileName     = keyFile;
            _crackerFamily  = crackerFamily;
            ramCost         = tier >= 3 ? DrawTestParams.RamCostV3 : DrawTestParams.RamCostV2; // V2=tier2, V3=tier3
            IdentifierName  = displayName;
            _testMode       = args != null && Array.IndexOf(args, "--test")     >= 0;
            _loopMode       = args != null && Array.IndexOf(args, "--infinity") >= 0;

            if (os.connectedComp != null)
                targetIP = os.connectedComp.ip;

            // V3 crackers get an interactive mini-game instead of a passive timer.
            if (keyFile != null)
            {
                if      (crackerFamily == "ssh") _miniGame = new GPSignalSync();
                else if (crackerFamily == "ftp") _miniGame = new GPPacketSort();
                else                             _miniGame = new GPInjectionTiming();
            }
        }

        public override void Update(float t)
        {
            // base.Update(t) calls ExeModule.Update which throws for BaseExecutable —
            // skip it so our timer always runs.

            if (!initialized)
            {
                initialized = true;

                if (_testMode || _loopMode)
                {
                    GatekeeperPlugin.Instance?.Log.LogInfo(
                        "[GP] " + IdentifierName + (_loopMode ? " --infinity loop" : " --test one-shot"));
                    _miniGame?.Init(Bounds, DrawTestParams.HeaderH);
                    return; // skip all port/target checks
                }

                var target = Programs.getComputer(os, targetIP);
                if (target == null)
                {
                    os.write("[GP] ERROR: No target. Connect to a node first.");
                    needsRemoval = true;
                    return;
                }

                if (tier >= 2)
                {
                    // PF port — existence check by protocol name.
                    // isPortOpen(string) returns false for both "closed" and "not present",
                    // so use GetAllPortStates() to distinguish the two.
                    bool portExists = false;
                    foreach (var ps in target.GetAllPortStates())
                        if (ps.Record.Protocol == portName) { portExists = true; break; }

                    // Tier-up fallback: if this cracker's native port isn't on the node,
                    // try the next tier (e.g. SSHcrack_v2 on a T3 node that only has ssh_v3).
                    // The node's trace timer is the soft gate — hardware buffs are required
                    // to beat trace before solve completes.
                    if (!portExists && tier == 2)
                    {
                        string nextTier = portName.Replace("_v2", "_v3");
                        bool nextExists = false;
                        foreach (var ps in target.GetAllPortStates())
                            if (ps.Record.Protocol == nextTier) { nextExists = true; break; }
                        if (nextExists)
                        {
                            GatekeeperPlugin.Instance?.Log.LogInfo(
                                "[GP] " + IdentifierName + " escalating target: " + portName + " -> " + nextTier);
                            os.write("[GP] " + portName + " absent — attempting " + nextTier + " (buffs recommended).");
                            portName  = nextTier;
                            portExists = true;
                        }
                    }

                    if (!portExists)
                    {
                        os.write("[GP] ERROR: " + portName + " not found on " + targetIP + ".");
                        GatekeeperPlugin.Instance?.Log.LogWarning(
                            "[GP] ABORT: port=" + portName + " missing on " + targetIP);
                        needsRemoval = true;
                        return;
                    }
                    if (target.isPortOpen(portName))
                    {
                        os.write("[GP] " + portName + " already open.");
                        needsRemoval = true;
                        return;
                    }
                }
                else
                {
                    // Vanilla port (tier 1, reserved) — bail if already open.
                    if (target.isPortOpen(portNumber))
                    {
                        os.write("[GP] port " + portNumber + " already open.");
                        needsRemoval = true;
                        return;
                    }
                }

                if (keyFileName != null)
                {
                    var home    = os.thisComputer.files.root.searchForFolder("home");
                    bool hasKey = home != null && home.searchForFile(keyFileName) != null;
                    if (!hasKey)
                    {
                        os.write("[GP] V3 HANDSHAKE FAILED.");
                        os.write("[GP] Key file required: " + keyFileName);
                        os.write("[GP] Obtain the key from a relay node and scp it to /home.");
                        needsRemoval = true;
                        return;
                    }
                }

                try { target.hostileActionTaken(); }
                catch (Exception ex)
                {
                    GatekeeperPlugin.Instance?.Log.LogWarning("[GP] hostileActionTaken: " + ex.Message);
                }

                _miniGame?.Init(Bounds, DrawTestParams.HeaderH);
            }

            // ── Mini-game update path (V3 crackers — replaces passive timer) ──────────
            if (_miniGame != null)
            {
                var  kb         = Keyboard.GetState();
                bool spaceDown  = kb.IsKeyDown(Keys.Space);
                bool spaceJust  = spaceDown && !_prevSpace;
                _prevSpace      = spaceDown;

                _miniGame.Update(t, spaceJust);

                float solveTimeForPct = BASE_SOLVE_TIME[Math.Min(tier, BASE_SOLVE_TIME.Length - 1)];
                elapsed = _miniGame.Progress * solveTimeForPct; // keep elapsed in sync for progress bar

                if (_miniGame.IsComplete)
                {
                    if (_loopMode) { _miniGame.Init(Bounds, DrawTestParams.HeaderH); elapsed = 0f; return; }
                    if (_testMode) { os.write("[GP] " + IdentifierName + " test complete."); needsRemoval = true; return; }

                    GatekeeperPlugin.Instance?.Log.LogInfo(
                        "[GP] MINI-GAME DONE: " + portName + " target=" + targetIP);
                    try
                    {
                        var tgt = Programs.getComputer(os, targetIP);
                        if (tgt != null) tgt.openPort(portName, os.thisComputer.ip);
                    }
                    catch (Exception ex)
                    {
                        GatekeeperPlugin.Instance?.Log.LogWarning("[GP] openPort failed: " + ex.Message);
                    }
                    try
                    {
                        var vt = Programs.getComputer(os, targetIP);
                        bool ok = vt != null && vt.isPortOpen(portName);
                        GatekeeperPlugin.Instance?.Log.LogInfo(
                            "[GP] PORT VERIFY: " + IdentifierName + " isOpen=" + ok);
                    }
                    catch (Exception vex)
                    {
                        GatekeeperPlugin.Instance?.Log.LogWarning("[GP] verify failed: " + vex.Message);
                    }
                    os.write("[GP] " + IdentifierName + " handshake complete.");
                    needsRemoval = true;
                }
                return; // skip passive timer
            }

            float solveTime = BASE_SOLVE_TIME[Math.Min(tier, BASE_SOLVE_TIME.Length - 1)];
            elapsed += t * HardwareState.CpuMultiplier(os);

            // First-tick confirmation — appears once in LogOutput.log when timer starts.
            if (elapsed <= t * 2)
                GatekeeperPlugin.Instance?.Log.LogInfo(
                    "[GP] TIMER STARTED: " + portName + " solveTime=" + solveTime + "s target=" + targetIP);

            if (elapsed >= solveTime)
            {
                if (_loopMode)
                {
                    elapsed = 0f;
                    _grid   = null; // regenerate grid each loop
                    return;
                }
                if (_testMode)
                {
                    os.write("[GP] " + IdentifierName + " test complete.");
                    needsRemoval = true;
                    return;
                }

                GatekeeperPlugin.Instance?.Log.LogInfo(
                    "[GP] TIMER DONE: " + portName + " elapsed=" + elapsed.ToString("F1") + "s");
                // openPort may throw on nodes that don't own this PF port —
                // wrap so needsRemoval is always reached and the exe exits cleanly.
                try
                {
                    var target = Programs.getComputer(os, targetIP);
                    if (target != null)
                    {
                        if (tier >= 2)
                            target.openPort(portName, os.thisComputer.ip);   // PF string API (V2 + V3)
                        else
                            target.openPort(portNumber, os.thisComputer.ip); // vanilla int API (T1)
                    }
                }
                catch (Exception ex)
                {
                    GatekeeperPlugin.Instance?.Log.LogWarning(
                        "[GP] openPort failed: " + ex.Message);
                }

                // Ground-truth confirmation — re-reads port state independently of our write.
                try
                {
                    var verifyTarget = Programs.getComputer(os, targetIP);
                    bool confirmed = verifyTarget != null &&
                                     (tier >= 2 ? verifyTarget.isPortOpen(portName)
                                                : verifyTarget.isPortOpen(portNumber));
                    GatekeeperPlugin.Instance?.Log.LogInfo(
                        "[GP] PORT VERIFY: " + IdentifierName + " isOpen=" + confirmed
                        + (confirmed ? "" : " — openPort may have failed silently"));
                }
                catch (Exception vex)
                {
                    GatekeeperPlugin.Instance?.Log.LogWarning("[GP] verify failed: " + vex.Message);
                }

                os.write("[GP] " + IdentifierName + (tier >= 3 ? " handshake complete." : " breached."));
                needsRemoval = true;
            }
        }

        // Recreate the grid only when dimensions change (module resize or first draw).
        private void EnsureGrid(int cols, int rows)
        {
            if (_grid != null && _grid.GetLength(0) == rows && _grid.GetLength(1) == cols) return;
            _grid      = new char[rows, cols];
            _threshold = new float[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    _grid[r, c]      = HEX_CHARS[_rng.Next(HEX_CHARS.Length)];
                    _threshold[r, c] = (float)_rng.NextDouble(); // random crack order
                }
        }

        public override void Draw(float t)
        {
            // base.Draw(t) renders the module background + IdentifierName.
            base.Draw(t);
            drawOutline();

            int   ti        = Math.Min(tier, TIER_BAR.Length - 1);
            float solveTime = BASE_SOLVE_TIME[Math.Min(tier, BASE_SOLVE_TIME.Length - 1)];
            float pct       = initialized ? Math.Min(elapsed / solveTime, 1.0f) : 0f;

            DrawTestParams.TryHotReload(t);

            int HEADER_H    = DrawTestParams.HeaderH;
            int TARGET_ROWS = DrawTestParams.TargetRows;

            int contentX = Bounds.X + 4;
            int contentY = Bounds.Y + HEADER_H;
            int contentW = Bounds.Width  - 8;
            int contentH = Bounds.Height - HEADER_H - 4;

            // Explicit header — dark bg + tier accent underline + name + target IP.
            // Text is bottom-aligned inside the header (matches HTML drawtest layout):
            //   accent bar  at HEADER_H - 3  (3px tall, sits 3px from bottom of header bg)
            //   text baseline at HEADER_H - 8 (7px above accent, same as HTML h-7 pattern)
            {
                GuiData.spriteBatch.Draw(Utils.white,
                    new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, HEADER_H),
                    new Color(18, 18, 30));
                GuiData.spriteBatch.Draw(Utils.white,
                    new Rectangle(Bounds.X, Bounds.Y + HEADER_H - DrawTestParams.AccentH, Bounds.Width, DrawTestParams.AccentH),
                    TIER_BAR[ti]);
                int labelY = Bounds.Y + HEADER_H - DrawTestParams.LabelOffset;
                GuiData.spriteBatch.DrawString(GuiData.tinyfont, IdentifierName,
                    new Vector2(Bounds.X + 4, labelY), TIER_LABEL[ti]);
                string tgt   = _testMode ? "TEST" : (targetIP ?? "-");
                var    tgtSz = GuiData.tinyfont.MeasureString(tgt);
                GuiData.spriteBatch.DrawString(GuiData.tinyfont, tgt,
                    new Vector2(Bounds.X + Bounds.Width - (int)tgtSz.X - 4, labelY),
                    TIER_BAR[ti] * 0.75f);
            }

            if (contentH <= 0) return; // module too short for content

            // Mini-game draw (V3 crackers — replaces matrix/packets/waveform)
            if (_miniGame != null && initialized)
            {
                _miniGame.Draw(TIER_BAR[ti]);
                return;
            }

            // Measure the font's natural cell size.
            var   fontMeasure = GuiData.tinyfont.MeasureString("A");
            float fontW       = fontMeasure.X;
            float fontH       = fontMeasure.Y;

            // Log real font metrics once so you can calibrate charW/charH in drawtest.html.
            if (!_fontLogged) {
                _fontLogged = true;
                GatekeeperPlugin.Instance?.Log.LogInfo(
                    $"[GP] tinyfont: W={fontW:F2} H={fontH:F2}  — set charW/charH in drawtest.html to match");
            }

            // Auto-scale so exactly TARGET_ROWS fit in the available height.
            // Clamped to [0.45, 1.0]: never enlarge, never go below readable.
            float scale  = Math.Min(1.0f, Math.Max(0.45f, (float)contentH / (TARGET_ROWS * fontH)));
            int   CHAR_W = Math.Max(1, (int)(fontW * scale));
            int   CHAR_H = Math.Max(1, (int)(fontH * scale));

            int rows = Math.Min(TARGET_ROWS, Math.Max(1, contentH / CHAR_H));
            int cols = Math.Max(1, contentW / CHAR_W);

            EnsureGrid(cols, rows);

            // Resolve draw style from cfg for this cracker's family.
            string style = _crackerFamily == "ftp" ? DrawTestParams.FtpStyle
                         : _crackerFamily == "web"  ? DrawTestParams.WebStyle
                         :                            DrawTestParams.SshStyle;

            Color uncracked = new Color(180, 0, 0);
            Color cracked   = TIER_BAR[ti];

            // Flicker at ~12 Hz. The condition that determines which cells flicker
            // depends on the active style so cracked cells stay static in all modes.
            _drawTimer += t;
            if (_drawTimer >= 0.08f)
            {
                _drawTimer = 0f;
                if (style == "packets")
                {
                    int cR = (int)(pct * rows);
                    int cC = (int)((pct * rows - cR) * cols);
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                            if (r > cR || (r == cR && c >= cC))
                                _grid[r, c] = HEX_CHARS[_rng.Next(HEX_CHARS.Length)];
                }
                else if (style == "waveform")
                {
                    // Only flicker cells behind the advancing wave front.
                    for (int r = 0; r < rows; r++)
                    {
                        float rowPhase = (float)r / rows * 0.3f;
                        for (int c = 0; c < cols; c++)
                        {
                            float front = pct + 0.08f * (float)Math.Sin(r * 1.5f + pct * 6.28f) - rowPhase;
                            if ((float)c / cols > front)
                                _grid[r, c] = HEX_CHARS[_rng.Next(HEX_CHARS.Length)];
                        }
                    }
                }
                else // matrix
                {
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                            if (_threshold[r, c] > pct)
                                _grid[r, c] = HEX_CHARS[_rng.Next(HEX_CHARS.Length)];
                }
            }

            if      (style == "packets")  DrawPackets (cols, rows, pct, contentX, contentY, CHAR_W, CHAR_H, scale, cracked, uncracked);
            else if (style == "waveform") DrawWaveform(cols, rows, pct, contentX, contentY, CHAR_W, CHAR_H, scale, cracked, uncracked);
            else                          DrawMatrix  (cols, rows, pct, contentX, contentY, CHAR_W, CHAR_H, scale, cracked, uncracked);
        }

        private void DrawMatrix(int cols, int rows, float pct,
                                int cX, int cY, int cW, int cH, float scale,
                                Color cracked, Color uncracked)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    Color cell = _threshold[r, c] <= pct ? cracked : uncracked;
                    GuiData.spriteBatch.DrawString(
                        GuiData.tinyfont, _grid[r, c].ToString(),
                        new Vector2(cX + c * cW, cY + r * cH), cell,
                        0f, Vector2.Zero, scale,
                        Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
                }
        }

        // Rows complete top-to-bottom; within each row, columns complete left-to-right.
        // Mirrors the packet_rows variant in drawtest-ftp.html.
        private void DrawPackets(int cols, int rows, float pct,
                                 int cX, int cY, int cW, int cH, float scale,
                                 Color cracked, Color uncracked)
        {
            int   crackedRows = (int)(pct * rows);
            int   crackedCols = (int)((pct * rows - crackedRows) * cols);
            Color edge        = Color.White;

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    Color cell;
                    if      (r < crackedRows)                          cell = cracked;
                    else if (r == crackedRows && c <  crackedCols)     cell = cracked;
                    else if (r == crackedRows && c == crackedCols)     cell = edge;
                    else                                               cell = uncracked;
                    GuiData.spriteBatch.DrawString(
                        GuiData.tinyfont, _grid[r, c].ToString(),
                        new Vector2(cX + c * cW, cY + r * cH), cell,
                        0f, Vector2.Zero, scale,
                        Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
                }
        }

        // Sine-wave sweep: each row unlocks left-to-right with a vertical sine ripple.
        // The wave front shimmers white as it advances across the grid.
        private void DrawWaveform(int cols, int rows, float pct,
                                  int cX, int cY, int cW, int cH, float scale,
                                  Color cracked, Color uncracked)
        {
            for (int r = 0; r < rows; r++)
            {
                float rowPhase = (float)r / rows * 0.3f;
                for (int c = 0; c < cols; c++)
                {
                    float colPct = (float)c / cols;
                    float front  = pct + 0.08f * (float)Math.Sin(r * 1.5f + pct * 6.28f) - rowPhase;
                    Color cell;
                    if      (colPct <= front - 0.06f) cell = cracked;
                    else if (colPct <= front)          cell = Color.Lerp(cracked, Color.White, (front - colPct) / 0.06f * 0.6f + 0.4f);
                    else                               cell = uncracked;
                    GuiData.spriteBatch.DrawString(
                        GuiData.tinyfont, _grid[r, c].ToString(),
                        new Vector2(cX + c * cW, cY + r * cH), cell,
                        0f, Vector2.Zero, scale,
                        Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
                }
            }
        }
    }

    // =========================================================================
    // CONCRETE CRACKERS — V2 (tier 2, orange, 10s) and V3 (tier 3, cyan, 15s)
    // =========================================================================
    // V2 — upgraded vanilla crackers. Target the same ports as vanilla (22/21/80),
    //       run with orange color. No key file required. Works on any node with the port.
    // V2: protocol name "ssh_v2", same port number 22 as vanilla ssh.
    // V3: protocol name "ssh_v3", same port number 22. Tier encodes protocol, not number.
    public class GPSSHCrackV2 : GPCrackBase
    {
        public GPSSHCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ssh_v2", 22, 2, "SSHcrack_v2", "ssh") { }
    }

    public class GPFTPCrackV2 : GPCrackBase
    {
        public GPFTPCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ftp_v2", 21, 2, "FTPBounce_v2", "ftp") { }
    }

    public class GPWebCrackV2 : GPCrackBase
    {
        public GPWebCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "web_v2", 80, 2, "WebServerWorm_v2", "web") { }
    }

    public class GPSSHCrackV3 : GPCrackBase
    {
        public GPSSHCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ssh_v3", 22, 3, "SSHcrack_v3", "ssh", "ssh_v3_key.dat") { }
    }

    public class GPFTPCrackV3 : GPCrackBase
    {
        public GPFTPCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ftp_v3", 21, 3, "FTPBounce_v3", "ftp", "ftp_v3_key.dat") { }
    }

    public class GPWebCrackV3 : GPCrackBase
    {
        public GPWebCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "web_v3", 80, 3, "WebServerWorm_v3", "web", "web_v3_key.dat") { }
    }
}
