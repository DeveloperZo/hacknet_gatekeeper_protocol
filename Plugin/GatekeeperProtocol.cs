// =============================================================================
// GatekeeperProtocol.cs — M1 Plugin
// Gatekeeper Protocol — Hacknet Mod
//
// M1 scope:
//   - Register 6 custom PFPorts (ssh_v2/ftp_v2/web_v2, ssh_v3/ftp_v3/web_v3)
//   - GPCrackBaseV2 + 3 concrete crackers (10s base, CPU mult)
//   - GPCrackBaseV3 + 3 concrete crackers (key file gate, 10s base, CPU mult)
//   - gp_debug command
//
// Port naming: Pathfinder uses "web" for HTTP (not "http").
// V3 key files: ssh_v3_key.dat, ftp_v3_key.dat, web_v3_key.dat (in player /home)
//
// Hardware flags (read-only in M1, upgraded in M3):
//   CPU — gp_cpu_t2/t3/t4 : crack speed multiplier (1.0x / 1.5x / 2.25x / 3.0x)
//   RAM — gp_ram_t2/t3/t4 : process slot capacity
//   HDD — gp_hdd_t2/t3/t4 : inventory size (limits large script storage)
//   NIC — gp_nic_t2/t3/t4 : trace time modifier + upload/download speed
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

            // --- Port registrations ---
            // V2 tier: GP crack exe required, 10s base crack time
            PortManager.RegisterPort("ssh_v2", "SSH V2", 10022);
            PortManager.RegisterPort("ftp_v2", "FTP V2", 10021);
            PortManager.RegisterPort("web_v2", "Web V2", 10080);

            // V3 tier: key file required, 10s base crack time
            PortManager.RegisterPort("ssh_v3", "SSH V3", 20022);
            PortManager.RegisterPort("ftp_v3", "FTP V3", 20021);
            PortManager.RegisterPort("web_v3", "Web V3", 20080);

            Log.LogInfo("[GP] Registered 6 custom ports: ssh_v2/ftp_v2/web_v2, ssh_v3/ftp_v3/web_v3");

            // --- Executables ---
            // Must use ExecutableManager.RegisterExecutable explicitly in v5.x.
            // The [Pathfinder.Meta.Load.Executable] attribute is not scanned — omit it.
            // The xmlName matches FileContents="..." in StartingActions.xml.
            // Pathfinder's TextReplaceEvent swaps the XML token for binary exe data at load time.
            ExecutableManager.RegisterExecutable<GPSSHCrackV2>("#GP_SSH_V2#");
            ExecutableManager.RegisterExecutable<GPFTPCrackV2>("#GP_FTP_V2#");
            ExecutableManager.RegisterExecutable<GPWebCrackV2>("#GP_WEB_V2#");
            ExecutableManager.RegisterExecutable<GPSSHCrackV3>("#GP_SSH_V3#");
            ExecutableManager.RegisterExecutable<GPFTPCrackV3>("#GP_FTP_V3#");
            ExecutableManager.RegisterExecutable<GPWebCrackV3>("#GP_WEB_V3#");

            Log.LogInfo("[GP] Registered 6 executables: GP_SSH/FTP/WEB_V2, GP_SSH/FTP/WEB_V3");

            // --- Commands ---
            CommandManager.RegisterCommand("gp_debug", GpDebugCommand, addAutocomplete: false);

            Log.LogInfo("[GP] M1 plugin loaded. Commands: gp_debug");
            return true;
        }

        public override bool Unload()
        {
            Log.LogInfo("[GP] Gatekeeper Protocol unloaded.");
            return true;
        }

        // =====================================================================
        // gp_debug — diagnostic printout
        // =====================================================================
        private static void GpDebugCommand(OS os, string[] args)
        {
            os.write("");
            os.write("  [GP] ===== GATEKEEPER PROTOCOL DEBUG =====");
            os.write("");

            // CPU tier
            int cpuTier = HardwareState.CpuTier(os);
            float cpuMult = HardwareState.CpuMultiplier(os);
            os.write("  CPU  : T" + cpuTier + " (" + cpuMult.ToString("F2") + "x)");

            // RAM tier
            int ramTier = HardwareState.RamTier(os);
            os.write("  RAM  : T" + ramTier + " [" + os.totalRam + " MB]");

            // HDD tier
            int hddTier = HardwareState.HddTier(os);
            os.write("  HDD  : T" + hddTier + " [M3]");

            // NIC tier
            int nicTier = HardwareState.NicTier(os);
            os.write("  NIC  : T" + nicTier + " [M3]");

            // Credits
            os.write("  CRED : [M3]");

            // Live trace state
            os.write("");
            try
            {
                var tt = os.traceTracker;
                if (tt.active)
                {
                    float remaining = tt.startingTimer - tt.timer;
                    os.write("  TRACE: ACTIVE - " + remaining.ToString("F1") + "s remaining");
                }
                else
                {
                    os.write("  TRACE: inactive");
                }
            }
            catch (Exception)
            {
                os.write("  TRACE: [field names unverified]");
            }

            // Connected node info
            if (os.connectedComp != null)
            {
                os.write("");
                os.write("  Node : " + os.connectedComp.name + " (" + os.connectedComp.ip + ")");
                os.write("  Trace: " + os.connectedComp.traceTime + "s max");

                bool hasGpPorts = false;
                foreach (var port in os.connectedComp.GetAllPortStates())
                {
                    if (port.Record.Protocol.EndsWith("_v2") || port.Record.Protocol.EndsWith("_v3"))
                    {
                        if (!hasGpPorts) { os.write("  Ports:"); hasGpPorts = true; }
                        string state = port.Cracked ? "OPEN" : "CLOSED";
                        os.write("    " + port.Record.Protocol.PadRight(10) + " [" + state + "]");
                    }
                }
                if (!hasGpPorts) os.write("  Ports: none on this node");
            }
            else
            {
                os.write("  Node : [not connected]");
            }

            os.write("");
            os.write("  [GP] ==========================================");
            os.write("");
        }
    }

    // =========================================================================
    // HARDWARE STATE
    //
    // CPU  — crack speed multiplier, applied to all GP crack executables
    // RAM  — process slot capacity (M3: gating which exes can run simultaneously)
    // HDD  — inventory size (M3: larger scripts need more HDD space)
    // NIC  — trace time modifier + transfer speed (M3: NIC T2+ needed for v3 nodes)
    //
    // All tiers use additive flag pattern: gp_<hw>_t2 means "at least T2".
    // Flags cannot be removed — tier is always the highest flag present.
    // =========================================================================
    public static class HardwareState
    {
        public static int CpuTier(OS os)
        {
            if (os.Flags.HasFlag("gp_cpu_t4")) return 4;
            if (os.Flags.HasFlag("gp_cpu_t3")) return 3;
            if (os.Flags.HasFlag("gp_cpu_t2")) return 2;
            return 1;
        }

        public static float CpuMultiplier(OS os)
        {
            if (os.Flags.HasFlag("gp_cpu_t4")) return 3.0f;
            if (os.Flags.HasFlag("gp_cpu_t3")) return 2.25f;
            if (os.Flags.HasFlag("gp_cpu_t2")) return 1.5f;
            return 1.0f;
        }

        public static int RamTier(OS os)
        {
            if (os.Flags.HasFlag("gp_ram_t4")) return 4;
            if (os.Flags.HasFlag("gp_ram_t3")) return 3;
            if (os.Flags.HasFlag("gp_ram_t2")) return 2;
            return 1;
        }

        public static int HddTier(OS os)
        {
            if (os.Flags.HasFlag("gp_hdd_t4")) return 4;
            if (os.Flags.HasFlag("gp_hdd_t3")) return 3;
            if (os.Flags.HasFlag("gp_hdd_t2")) return 2;
            return 1;
        }

        public static int NicTier(OS os)
        {
            if (os.Flags.HasFlag("gp_nic_t4")) return 4;
            if (os.Flags.HasFlag("gp_nic_t3")) return 3;
            if (os.Flags.HasFlag("gp_nic_t2")) return 2;
            return 1;
        }
    }

    // =========================================================================
    // GP V2 CRACK BASE
    //
    // Running this exe means the player has V2-capable cracking software.
    // Base solve time: 10s. CPU multiplier applied (higher CPU = faster solve).
    // No key file required. No tier gate.
    //
    // At CPU T1 (1.0x): 10s
    // At CPU T2 (1.5x): 6.7s
    // At CPU T3 (2.25x): 4.4s
    // =========================================================================
    public abstract class GPCrackBaseV2 : BaseExecutable
    {
        protected string portName;      // e.g. "ssh_v2"
        protected int    portNumber;    // e.g. 10022 — passed to Computer.openPort(int, string)
        protected float  elapsed;
        protected bool   initialized;

        private const float BASE_SOLVE_TIME = 10.0f;

        protected GPCrackBaseV2(Rectangle location, OS os, string[] args, string port, int portNum)
            : base(location, os, args)
        {
            portName   = port;
            portNumber = portNum;
            ramCost    = 80;
            IdentifierName = port.Replace("ssh_v2", "SSHcrack_v2")
                                  .Replace("ftp_v2", "FTPBounce_v2")
                                  .Replace("web_v2", "WebServerWorm_v2");
        }

        public override void Update(float t)
        {
            base.Update(t);

            if (!initialized)
            {
                initialized = true;
                var target = Programs.getComputer(os, targetIP);
                if (target == null)
                {
                    os.write("[GP] ERROR: Target not found.");
                    needsRemoval = true;
                    return;
                }

                if (target.isPortOpen(portName))
                {
                    os.write("[GP] " + portName + " already open.");
                    needsRemoval = true;
                    return;
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

            elapsed += t * HardwareState.CpuMultiplier(os);

            if (elapsed >= BASE_SOLVE_TIME)
            {
                var target = Programs.getComputer(os, targetIP);
                if (target != null)
                    target.openPort(portNumber, os.thisComputer.ip);
                os.write("[GP] " + portName + " breached.");
                needsRemoval = true;
            }
        }

        public override void Draw(float t)
        {
            base.Draw(t);
            drawTarget();
            drawOutline();

            if (!initialized) return;

            float pct  = Math.Min(elapsed / BASE_SOLVE_TIME, 1.0f);
            int barX   = Bounds.X + 10;
            int barY   = Bounds.Y + 30;
            int barW   = Bounds.Width - 20;
            int barH   = 16;

            Hacknet.Gui.RenderedRectangle.doRectangle(barX, barY, barW, barH, new Color(20, 20, 20));
            Hacknet.Gui.RenderedRectangle.doRectangle(barX, barY, (int)(barW * pct), barH, new Color(200, 120, 0));

            Hacknet.Gui.TextItem.doLabel(new Vector2(barX, barY - 20),
                portName.ToUpper() + " - " + (int)(pct * 100) + "%", new Color(255, 180, 80));
            Hacknet.Gui.TextItem.doLabel(new Vector2(barX, barY + barH + 4),
                "V2 BREACH", new Color(120, 120, 120));
        }
    }

    // =========================================================================
    // GP V3 CRACK BASE
    //
    // Key gate: requires <portBase>_v3_key.dat in player /home.
    //   portBase = "ssh" for "ssh_v3", "ftp" for "ftp_v3", etc.
    //
    // Base solve time: 10s. CPU multiplier applied.
    // V3 ports will appear on nodes with shorter trace times in M2+ — the
    // challenge comes from node difficulty, not this executable's gate.
    // =========================================================================
    public abstract class GPCrackBaseV3 : BaseExecutable
    {
        protected string portName;      // e.g. "ssh_v3"
        protected int    portNumber;    // e.g. 20022
        protected string portBase;      // e.g. "ssh"
        protected float  elapsed;
        protected bool   initialized;
        protected bool   failed;

        private const float BASE_SOLVE_TIME = 10.0f;

        protected GPCrackBaseV3(Rectangle location, OS os, string[] args, string port, int portNum, string pBase)
            : base(location, os, args)
        {
            portName   = port;
            portNumber = portNum;
            portBase   = pBase;
            ramCost    = 120;
            IdentifierName = port.Replace("ssh_v3", "SSHcrack_v3")
                                  .Replace("ftp_v3", "FTPBounce_v3")
                                  .Replace("web_v3", "WebServerWorm_v3");
        }

        public override void Update(float t)
        {
            base.Update(t);

            if (!initialized)
            {
                initialized = true;
                var target = Programs.getComputer(os, targetIP);
                if (target == null)
                {
                    os.write("[GP] ERROR: Target not found.");
                    needsRemoval = true;
                    return;
                }

                if (target.isPortOpen(portName))
                {
                    os.write("[GP] " + portName + " already open.");
                    needsRemoval = true;
                    return;
                }

                // KEY FILE GATE — requires <portBase>_v3_key.dat in /home
                string keyFileName = portBase + "_v3_key.dat";
                var home = os.thisComputer.files.root.searchForFolder("home");
                bool hasKey = home != null && home.searchForFile(keyFileName) != null;

                if (!hasKey)
                {
                    os.write("[GP] V3 HANDSHAKE FAILED.");
                    os.write("[GP] Key file required: " + keyFileName);
                    os.write("[GP] scp the key from a relay node to your /home.");
                    failed = true;
                    needsRemoval = true;
                    return;
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

            if (failed) return;

            elapsed += t * HardwareState.CpuMultiplier(os);

            if (elapsed >= BASE_SOLVE_TIME)
            {
                var target = Programs.getComputer(os, targetIP);
                if (target != null)
                    target.openPort(portNumber, os.thisComputer.ip);
                os.write("[GP] " + portName + " handshake complete.");
                needsRemoval = true;
            }
        }

        public override void Draw(float t)
        {
            base.Draw(t);
            drawTarget();
            drawOutline();

            if (!initialized || failed) return;

            float pct  = Math.Min(elapsed / BASE_SOLVE_TIME, 1.0f);
            int barX   = Bounds.X + 10;
            int barY   = Bounds.Y + 30;
            int barW   = Bounds.Width - 20;
            int barH   = 16;

            Hacknet.Gui.RenderedRectangle.doRectangle(barX, barY, barW, barH, new Color(20, 20, 20));
            Hacknet.Gui.RenderedRectangle.doRectangle(barX, barY, (int)(barW * pct), barH, new Color(0, 180, 220));

            Hacknet.Gui.TextItem.doLabel(new Vector2(barX, barY - 20),
                portName.ToUpper() + " - " + (int)(pct * 100) + "%", new Color(80, 200, 255));
            Hacknet.Gui.TextItem.doLabel(new Vector2(barX, barY + barH + 4),
                "V3 BREACH", new Color(100, 120, 140));
        }
    }

    // =========================================================================
    // CONCRETE V2 CRACKERS
    // =========================================================================

    public class GPSSHCrackV2 : GPCrackBaseV2
    {
        public GPSSHCrackV2(Rectangle location, OS os, string[] args)
            : base(location, os, args, "ssh_v2", 10022) { }
    }

    public class GPFTPCrackV2 : GPCrackBaseV2
    {
        public GPFTPCrackV2(Rectangle location, OS os, string[] args)
            : base(location, os, args, "ftp_v2", 10021) { }
    }

    public class GPWebCrackV2 : GPCrackBaseV2
    {
        public GPWebCrackV2(Rectangle location, OS os, string[] args)
            : base(location, os, args, "web_v2", 10080) { }
    }

    // =========================================================================
    // CONCRETE V3 CRACKERS
    // =========================================================================

    public class GPSSHCrackV3 : GPCrackBaseV3
    {
        public GPSSHCrackV3(Rectangle location, OS os, string[] args)
            : base(location, os, args, "ssh_v3", 20022, "ssh") { }
    }

    public class GPFTPCrackV3 : GPCrackBaseV3
    {
        public GPFTPCrackV3(Rectangle location, OS os, string[] args)
            : base(location, os, args, "ftp_v3", 20021, "ftp") { }
    }

    public class GPWebCrackV3 : GPCrackBaseV3
    {
        public GPWebCrackV3(Rectangle location, OS os, string[] args)
            : base(location, os, args, "web_v3", 20080, "web") { }
    }
}
