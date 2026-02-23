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

            CommandManager.RegisterCommand("gp_debug", GpDebugCommand, addAutocomplete: false);

            Log.LogInfo("[GP] M1 plugin loaded. Commands: gp_debug");
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

        protected GPCrackBase(Rectangle location, OS os, string[] args,
                              string port, int portNum, int tier, string keyFile = null)
            : base(location, os, args)
        {
            portName    = port;
            portNumber  = portNum;
            this.tier   = tier;
            keyFileName = keyFile;
            ramCost     = tier >= 3 ? 120 : 80;

            IdentifierName = port
                .Replace("ssh_v2", "SSHcrack_v2")
                .Replace("ftp_v2", "FTPBounce_v2")
                .Replace("web_v2", "WebServerWorm_v2")
                .Replace("ssh_v3", "SSHcrack_v3")
                .Replace("ftp_v3", "FTPBounce_v3")
                .Replace("web_v3", "WebServerWorm_v3");

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

                var target = Programs.getComputer(os, targetIP);
                if (target == null)
                {
                    os.write("[GP] ERROR: No target. Connect to a node first.");
                    needsRemoval = true;
                    return;
                }

                if (target.isPortOpen(portName))
                {
                    os.write("[GP] " + portName + " already open.");
                    needsRemoval = true;
                    return;
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

                try
                {
                    if (!os.traceTracker.active)
                        target.hostileActionTaken();
                }
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
                GatekeeperPlugin.Instance?.Log.LogInfo(
                    "[GP] TIMER DONE: " + portName + " elapsed=" + elapsed.ToString("F1") + "s");
                // openPort may throw on nodes that don't own this PF port —
                // wrap so needsRemoval is always reached and the exe exits cleanly.
                try
                {
                    var target = Programs.getComputer(os, targetIP);
                    if (target != null)
                        target.openPort(portNumber, os.thisComputer.ip);
                }
                catch (Exception ex)
                {
                    GatekeeperPlugin.Instance?.Log.LogWarning(
                        "[GP] openPort(" + portNumber + ") failed: " + ex.Message);
                }
                os.write("[GP] " + portName + (tier >= 3 ? " handshake complete." : " breached."));
                needsRemoval = true;
            }
        }

        public override void Draw(float t)
        {
            // base.Draw(t) renders the module background + IdentifierName — same as vanilla crackers.
            // base.Update(t) is NOT called (it throws for BaseExecutable); Draw is safe.
            base.Draw(t);
            drawTarget();
            drawOutline();

            int   ti        = Math.Min(tier, TIER_BAR.Length - 1);
            float solveTime = BASE_SOLVE_TIME[Math.Min(tier, BASE_SOLVE_TIME.Length - 1)];
            float pct       = initialized ? Math.Min(elapsed / solveTime, 1.0f) : 0f;

            // Bottom-anchored bar — adapts to actual module height regardless of value.
            int barH = 8;
            int barX = Bounds.X + 2;
            int barW = Bounds.Width - 4;
            int barY = Bounds.Y + Bounds.Height - barH - 2;

            Hacknet.Gui.RenderedRectangle.doRectangle(barX, barY, barW, barH, new Color(20, 20, 20));
            Hacknet.Gui.RenderedRectangle.doRectangle(barX, barY, (int)(barW * pct), barH, TIER_BAR[ti]);
        }
    }

    // =========================================================================
    // CONCRETE CRACKERS — V2 (tier 2, orange, 10s) and V3 (tier 3, cyan, 15s)
    // =========================================================================
    public class GPSSHCrackV2 : GPCrackBase
    {
        public GPSSHCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ssh_v2", 10022, 2) { }
    }

    public class GPFTPCrackV2 : GPCrackBase
    {
        public GPFTPCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ftp_v2", 10021, 2) { }
    }

    public class GPWebCrackV2 : GPCrackBase
    {
        public GPWebCrackV2(Rectangle l, OS os, string[] args)
            : base(l, os, args, "web_v2", 10080, 2) { }
    }

    public class GPSSHCrackV3 : GPCrackBase
    {
        public GPSSHCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ssh_v3", 20022, 3, "ssh_v3_key.dat") { }
    }

    public class GPFTPCrackV3 : GPCrackBase
    {
        public GPFTPCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "ftp_v3", 20021, 3, "ftp_v3_key.dat") { }
    }

    public class GPWebCrackV3 : GPCrackBase
    {
        public GPWebCrackV3(Rectangle l, OS os, string[] args)
            : base(l, os, args, "web_v3", 20080, 3, "web_v3_key.dat") { }
    }
}
