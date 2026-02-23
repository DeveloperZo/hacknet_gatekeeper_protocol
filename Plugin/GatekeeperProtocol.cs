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
        // HeaderH — px below Bounds.Y where the char matrix starts.
        // Tune in-game: gp_drawtest header <n>
        // CharW/CharH are auto-measured from GuiData.tinyfont at draw time.
        public static int HeaderH = 28;
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
        private readonly bool _testMode;

        // Character matrix — vanilla SSHcrack-style visual
        private static readonly Random _rng      = new Random();
        private static readonly string HEX_CHARS = "0123456789ABCDEF";
        private char[,]  _grid;
        private float[,] _threshold; // per-cell crack threshold (0-1), set once at grid init
        private float    _drawTimer; // accumulates delta-time for flicker cadence

        // displayName  — what shows in the RAM panel (e.g. "SSHcrack_v2")
        // port         — V2: vanilla protocol label ("ssh"), V3: PF port name ("ssh_v3")
        // portNum      — V2: vanilla port number (22),       V3: PF port number (20022)
        // tier < 3     → vanilla port API  (int overloads)
        // tier >= 3    → Pathfinder PF API (string overloads)
        protected GPCrackBase(Rectangle location, OS os, string[] args,
                              string port, int portNum, int tier,
                              string displayName, string keyFile = null)
            : base(location, os, args)
        {
            portName       = port;
            portNumber     = portNum;
            this.tier      = tier;
            keyFileName    = keyFile;
            ramCost        = tier >= 3 ? 120 : 80;
            IdentifierName = displayName;
            _testMode      = args != null && Array.IndexOf(args, "--test") >= 0;

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

            // Header offset is still tunable via gp_drawtest / DrawTestParams.HeaderH.
            int HEADER_H = DrawTestParams.HeaderH;

            // Measure character dimensions directly from the font — fixes overlap caused by
            // any hardcoded step not matching GuiData.tinyfont's internal spacing metrics.
            // MeasureString returns the exact advance width/height for the glyph, so each
            // character gets its natural cell and nothing piles up.
            var  charSize = GuiData.tinyfont.MeasureString("A");
            int  CHAR_W   = Math.Max(1, (int)charSize.X);
            int  CHAR_H   = Math.Max(1, (int)charSize.Y);

            int contentX = Bounds.X + 4;
            int contentY = Bounds.Y + HEADER_H;
            int contentW = Bounds.Width  - 8;
            int contentH = Bounds.Height - HEADER_H - 4;

            int cols = Math.Max(1, contentW / CHAR_W);
            int rows = Math.Max(1, contentH / CHAR_H);

            EnsureGrid(cols, rows);

            // Flicker uncracked chars ~12 times per second for the matrix feel.
            _drawTimer += t;
            if (_drawTimer >= 0.08f)
            {
                _drawTimer = 0f;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        if (_threshold[r, c] > pct)
                            _grid[r, c] = HEX_CHARS[_rng.Next(HEX_CHARS.Length)];
            }

            Color uncracked = new Color(180, 0, 0); // red — same as vanilla SSHcrack
            Color cracked   = TIER_BAR[ti];          // orange (V2) or cyan (V3)

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Color cell = _threshold[r, c] <= pct ? cracked : uncracked;
                    GuiData.spriteBatch.DrawString(
                        GuiData.tinyfont,
                        _grid[r, c].ToString(),
                        new Vector2(contentX + c * CHAR_W, contentY + r * CHAR_H),
                        cell);
                }
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
            : base(l, os, args, "ssh", 22, 2, "SSHcrack_v2") { }
    }

    public class GPFTPCrackV2 : GPCrackBase
    {
        public GPFTPCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ftp", 21, 2, "FTPBounce_v2") { }
    }

    public class GPWebCrackV2 : GPCrackBase
    {
        public GPWebCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "web", 80, 2, "WebServerWorm_v2") { }
    }

    // V3 — hardened-node crackers. Target custom PF ports (20022/20021/20080),
    //       run with cyan color. Require a key file obtained from a relay node.
    public class GPSSHCrackV3 : GPCrackBase
    {
        public GPSSHCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ssh_v3", 20022, 3, "SSHcrack_v3", "ssh_v3_key.dat") { }
    }

    public class GPFTPCrackV3 : GPCrackBase
    {
        public GPFTPCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ftp_v3", 20021, 3, "FTPBounce_v3", "ftp_v3_key.dat") { }
    }

    public class GPWebCrackV3 : GPCrackBase
    {
        public GPWebCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "web_v3", 20080, 3, "WebServerWorm_v3", "web_v3_key.dat") { }
    }
}
