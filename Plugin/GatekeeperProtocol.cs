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
using Pathfinder.Command;
using Pathfinder.Executable;
using Pathfinder.Port;
using System;
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

            PortManager.RegisterPort("ssh_v2", "SSH V2", 10022);
            PortManager.RegisterPort("ftp_v2", "FTP V2", 10021);
            PortManager.RegisterPort("web_v2", "Web V2", 10080);
            PortManager.RegisterPort("ssh_v3", "SSH V3", 20022);
            PortManager.RegisterPort("ftp_v3", "FTP V3", 20021);
            PortManager.RegisterPort("web_v3", "Web V3", 20080);

            Log.LogInfo("[GP] Registered 6 custom ports: ssh_v2/ftp_v2/web_v2, ssh_v3/ftp_v3/web_v3");

            ExecutableManager.RegisterExecutable<GPSSHCrackV2>("#SSH_V2#");
            ExecutableManager.RegisterExecutable<GPFTPCrackV2>("#FTP_V2#");
            ExecutableManager.RegisterExecutable<GPWebCrackV2>("#WEB_V2#");
            ExecutableManager.RegisterExecutable<GPSSHCrackV3>("#SSH_V3#");
            ExecutableManager.RegisterExecutable<GPFTPCrackV3>("#FTP_V3#");
            ExecutableManager.RegisterExecutable<GPWebCrackV3>("#WEB_V3#");

            Log.LogInfo("[GP] Registered 6 executables: SSH/FTP/WEB_V2, SSH/FTP/WEB_V3");

            CommandManager.RegisterCommand("gp_debug",    GpDebugCommand,    addAutocomplete: false);
            CommandManager.RegisterCommand("gp_drawtest", GpDrawTestCommand, addAutocomplete: false);

            Log.LogInfo("[GP] M1 plugin loaded. Commands: gp_debug, gp_drawtest");
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
                os.write("");
                os.write("Node : " + os.connectedComp.name + " (" + os.connectedComp.ip + ")");
                os.write("Trace: " + os.connectedComp.traceTime + "s max");
                bool hasGpPorts = false;
                foreach (var port in os.connectedComp.GetAllPortStates())
                {
                    if (port.Record.Protocol.EndsWith("_v2") || port.Record.Protocol.EndsWith("_v3"))
                    {
                        if (!hasGpPorts) { os.write("Ports:"); hasGpPorts = true; }
                        os.write("  " + port.Record.Protocol.PadRight(10) + " [" + (port.Cracked ? "OPEN" : "CLOSED") + "]");
                    }
                }
                if (!hasGpPorts) os.write("Ports: none on this node");
            }
            else
            {
                os.write("Node : [not connected]");
            }

            os.write("");
            os.write("[GP] ==========================================");
            os.write("");
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
        public static int    HeaderH    = 28;     // px below module top where matrix starts
        public static int    TargetRows = 6;      // rows the animation always tries to show
        public static int    RamCostV2  = 256;    // module height ∝ ramCost/playerRam
        public static int    RamCostV3  = 384;
        // Draw style per exe family. Valid values: "matrix" | "packets"
        // matrix  — random threshold hex grid (same as vanilla SSHcrack)
        // packets — rows complete top-to-bottom, columns left-to-right (FTP feel)
        public static string SshStyle   = "matrix";
        public static string FtpStyle   = "packets";
        public static string WebStyle   = "matrix";

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
                    if (key == "headerh")    HeaderH    = v;
                    if (key == "targetrows") TargetRows = Math.Max(1, v);
                    if (key == "ramcostv2")  RamCostV2  = Math.Max(1, v);
                    if (key == "ramcostv3")  RamCostV3  = Math.Max(1, v);
                }
                GatekeeperPlugin.Instance?.Log.LogInfo(
                    "[GP] cfg reloaded — headerH=" + HeaderH
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

        protected readonly string portName;
        protected readonly int    portNumber;
        protected readonly int    tier;
        protected readonly string keyFileName; // null = no gate
        protected float elapsed;
        protected bool  initialized;

        // Pass --test as an argument (e.g. "SSHcrack_v2 --test") to enter test mode:
        //   - skips all port/target checks so no connected node is needed
        //   - animation loops continuously instead of completing
        //   - combine with gp_drawtest to tune layout constants live
        private readonly bool   _testMode;
        private readonly string _crackerFamily; // "ssh" | "ftp" | "web" — selects draw style

        // Character matrix state
        private static readonly Random _rng      = new Random();
        private static readonly string HEX_CHARS = "0123456789ABCDEF";
        private char[,]  _grid;
        private float[,] _threshold;
        private float    _drawTimer;

        // displayName    — what shows in the RAM panel (e.g. "SSHcrack_v2")
        // port           — V2: vanilla protocol ("ssh"),   V3: PF port name ("ssh_v3")
        // portNum        — V2: vanilla port number (22),   V3: PF port number (20022)
        // crackerFamily  — "ssh" | "ftp" | "web" → drives draw style selection from cfg
        // tier < 3       → vanilla port API (int overloads)
        // tier >= 3      → Pathfinder PF API (string overloads)
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
            ramCost         = tier >= 3 ? DrawTestParams.RamCostV3 : DrawTestParams.RamCostV2;
            IdentifierName  = displayName;
            _testMode       = args != null && Array.IndexOf(args, "--test") >= 0;

            if (os.connectedComp != null)
                targetIP = os.connectedComp.ip;
        }

        public override void Update(float t)
        {
            // base.Update(t) calls ExeModule.Update which throws for BaseExecutable —
            // skip it so our timer always runs.

            if (!initialized)
            {
                initialized = true;

                if (_testMode)
                {
                    GatekeeperPlugin.Instance?.Log.LogInfo("[GP] DRAW TEST: " + IdentifierName + " looping");
                    return; // skip all port/target checks
                }

                var target = Programs.getComputer(os, targetIP);
                if (target == null)
                {
                    os.write("[GP] ERROR: No target. Connect to a node first.");
                    needsRemoval = true;
                    return;
                }

                if (tier >= 3)
                {
                    // PF port — existence check by name: isPortOpen(string) returns false for both
                    // "closed" and "not present", so use GetAllPortStates() to distinguish.
                    bool portExists = false;
                    foreach (var ps in target.GetAllPortStates())
                        if (ps.Record.Protocol == portName) { portExists = true; break; }
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
                    // Vanilla port — match vanilla SSHcrack behaviour exactly:
                    // no existence pre-check, just bail if already open.
                    // computer.ports is not a reliable existence oracle; openPort(int) is a
                    // silent no-op when the port isn't configured, same as vanilla.
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
            }

            float solveTime = BASE_SOLVE_TIME[Math.Min(tier, BASE_SOLVE_TIME.Length - 1)];
            elapsed += t * HardwareState.CpuMultiplier(os);

            // First-tick confirmation — appears once in LogOutput.log when timer starts.
            if (elapsed <= t * 2)
                GatekeeperPlugin.Instance?.Log.LogInfo(
                    "[GP] TIMER STARTED: " + portName + " solveTime=" + solveTime + "s target=" + targetIP);

            if (elapsed >= solveTime)
            {
                if (_testMode)
                {
                    elapsed = 0f;
                    _grid   = null; // regenerate grid each loop for visual variety
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
                        if (tier >= 3)
                            target.openPort(portName, os.thisComputer.ip);   // Pathfinder string API
                        else
                            target.openPort(portNumber, os.thisComputer.ip); // vanilla int API
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
                                     (tier >= 3 ? verifyTarget.isPortOpen(portName)
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
                    new Rectangle(Bounds.X, Bounds.Y + HEADER_H - 3, Bounds.Width, 3),
                    TIER_BAR[ti]);
                int labelY = Bounds.Y + HEADER_H - 11;
                GuiData.spriteBatch.DrawString(GuiData.tinyfont, IdentifierName,
                    new Vector2(Bounds.X + 4, labelY), TIER_LABEL[ti]);
                string tgt   = _testMode ? "TEST" : (targetIP ?? "-");
                var    tgtSz = GuiData.tinyfont.MeasureString(tgt);
                GuiData.spriteBatch.DrawString(GuiData.tinyfont, tgt,
                    new Vector2(Bounds.X + Bounds.Width - (int)tgtSz.X - 4, labelY),
                    TIER_BAR[ti] * 0.75f);
            }

            if (contentH <= 0) return; // module too short for content

            // Measure the font's natural cell size.
            var   fontMeasure = GuiData.tinyfont.MeasureString("A");
            float fontW       = fontMeasure.X;
            float fontH       = fontMeasure.Y;

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
                else // matrix
                {
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                            if (_threshold[r, c] > pct)
                                _grid[r, c] = HEX_CHARS[_rng.Next(HEX_CHARS.Length)];
                }
            }

            if (style == "packets")
                DrawPackets(cols, rows, pct, contentX, contentY, CHAR_W, CHAR_H, scale, cracked, uncracked);
            else
                DrawMatrix(cols, rows, pct, contentX, contentY, CHAR_W, CHAR_H, scale, cracked, uncracked);
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
    }

    // =========================================================================
    // CONCRETE CRACKERS — V2 (tier 2, orange, 10s) and V3 (tier 3, cyan, 15s)
    // =========================================================================
    // V2 — upgraded vanilla crackers. Target the same ports as vanilla (22/21/80),
    //       run with orange color. No key file required. Works on any node with the port.
    public class GPSSHCrackV2 : GPCrackBase
    {
        public GPSSHCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ssh", 22, 2, "SSHcrack_v2", "ssh") { }
    }

    public class GPFTPCrackV2 : GPCrackBase
    {
        public GPFTPCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ftp", 21, 2, "FTPBounce_v2", "ftp") { }
    }

    public class GPWebCrackV2 : GPCrackBase
    {
        public GPWebCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "web", 80, 2, "WebServerWorm_v2", "web") { }
    }

    public class GPSSHCrackV3 : GPCrackBase
    {
        public GPSSHCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ssh_v3", 20022, 3, "SSHcrack_v3", "ssh", "ssh_v3_key.dat") { }
    }

    public class GPFTPCrackV3 : GPCrackBase
    {
        public GPFTPCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ftp_v3", 20021, 3, "FTPBounce_v3", "ftp", "ftp_v3_key.dat") { }
    }

    public class GPWebCrackV3 : GPCrackBase
    {
        public GPWebCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "web_v3", 20080, 3, "WebServerWorm_v3", "web", "web_v3_key.dat") { }
    }
}
