# M1 — Tiered Systems

## Scope
Core port and executable tier system. Foundation everything else builds on.
Energize mechanic is designed here but implemented in M2 alongside node reconfig.

## Deliverables

### Tiered Ports
Six custom ports registered via Pathfinder PortManager:
- ssh_v2 (10022), ftp_v2 (10021), web_v2 (10080)
- ssh_v3 (20022), ftp_v3 (20021), web_v3 (20080)

Vanilla ports (ssh/ftp/web) are T1 targets — cracked with standard Hacknet tools.
V2 ports — require GP V2 crack executables. Base crack time: 10s × CPU multiplier.
V3 ports — require GP V3 crack executables AND the matching key file in player /home.

### Crack Executables
Six executables registered via Pathfinder:
- GPSSHv2, GPFTPv2, GPWebv2  (V2 tier — no gate, 10s base)
- GPSSHv3, GPFTPv3, GPWebv3  (V3 tier — key file gate, 10s base)

Each script executable represents its own tier. Running a V2 cracker means the
player has V2-capable cracking software. No global script tier flag.

CPU multiplier applies to all GP crackers: effective time = 10s / CpuMult.

### Hardware Flags
Flags written to save state (upgraded in M3):
- CPU — gp_cpu_t2/t3/t4  : crack speed multiplier (1.0x / 1.5x / 2.25x / 3.0x)
- RAM — gp_ram_t2/t3/t4  : process slot capacity (more/bigger exes running at once)
- HDD — gp_hdd_t2/t3/t4  : inventory size (larger scripts require more HDD space)
- NIC — gp_nic_t2/t3/t4  : trace time increase + faster upload/download

### Debug Commands
- gp_debug: prints CPU/RAM/HDD/NIC tier, RAM usage, trace state, node GP port status

### Test Nodes
- gp_test_node  : all 9 ports (3 vanilla + 3 V2 + 3 V3), proxy 50s, firewall GATEKEEPER, trace 120s
- gp_relay_alpha: vanilla ports only, holds 3 V3 key files

M1 testing: StartingActions pre-seeds V3 key files in player /home so all crack
paths are immediately testable without doing the scp workflow.

## Energize — Design (Implementation in M2)
After a layer or port is hacked, defenses that reset (via admin) drop one level.
Energized state prevents the level drop — defense resets to same level, energize is consumed.
One energized item per node.
Flag schema: gp_energized_<nodeID> = "proxy|firewall|ssh|ftp|web|..." or empty.

## Completion Criteria
- Extensions menu loads without crash
- GP extension starts, gp_test_node and gp_relay_alpha visible on map
- gp_debug output is correct (CPU/RAM/HDD/NIC tiers, trace state, port list)
- All six GP executables run and crack the correct ports
- V3 executables reject when key file is missing
- Crack timing scales with gp_cpu_t2/t3/t4 flags
- gp_test_node crackable end-to-end, porthack succeeds after 3 vanilla ports
