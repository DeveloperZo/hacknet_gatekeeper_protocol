# M1 — Tiered Systems

## Status: IN PROGRESS

### Completed
- 6 custom ports registered via PortManager (ssh_v2/ftp_v2/web_v2, ssh_v3/ftp_v3/web_v3)
- 6 executables registered via ExecutableManager (#GP_SSH_V2# through #GP_WEB_V3#)
- GPCrackBaseV2 and GPCrackBaseV3 with CPU multiplier applied
- V3 key file gate (requires <portBase>_v3_key.dat in player /home)
- Hardware flag schema defined (gp_cpu/ram/hdd/nic_t2/t3/t4)
- gp_debug command: prints hardware tiers, trace state, GP port status on connected node
- Plugin loads cleanly, all 6 ports and executables confirmed in BepInEx log
- Extension menu no longer crashes (ZDTK locale conflict resolved — GP must load before ZDTK in plugin order)
- gp_debug confirmed working in-game, node info correct

### In Progress
- Executable naming updated to SSHcrack_v2 / FTPBounce_v2 / WebServerWorm_v2 convention
- IdentifierName in GPCrackBaseV2 and GPCrackBaseV3 constructors needs updating in Cursor to match

### Pending Verification
- SSHcrack_v2 / FTPBounce_v2 / WebServerWorm_v2 execute correctly (not "No Command")
- SSHcrack_v3 / FTPBounce_v3 / WebServerWorm_v3 execute correctly
- V3 key file gate rejects missing key, accepts present key
- Crack progress bar renders
- Port opens on target node after crack completes
- porthack succeeds after all 9 ports open
- CPU tier flag changes affect crack speed

## File Layout

### Plugin
- Source: managed in Cursor
- DLL: D:\SteamLibrary\steamapps\common\Hacknet\BepInEx\plugins\GatekeeperProtocol.dll

### Extension
- ExtensionInfo.xml: StartingMission, StartingVisibleNodes, HasIntroStartup all correct
- Nodes/PlayerComp.xml: vanilla + V2 + V3 executables as <file> tags (token substitution fires here)
- Nodes/gp_test_node.xml: <ports> for vanilla + <PFPorts replace="false"> for V2/V3, portsForCrack val="9"
- Missions/RunStart.xml: SENTINEL mission, goal getAdminElsewhere on gp_test_node
- Actions/StartingActions.xml: empty (executables moved to PlayerComp.xml)

## Known Gotchas

### AddAsset Does Not Substitute Tokens
AddAsset FileContents="#GP_SSH_V2#" writes the literal string to the file.
Pathfinder token substitution only fires during Computer node XML loading.
All GP executables must be <file path="bin"> tags in PlayerComp.xml.

### PFPorts Format
replace="false" preserves vanilla ports. replace="true" would wipe them.
Port entries must be space-separated on one or more lines: protocol:portNum:Display_Name

### IdentifierName Must Match Filename
The IdentifierName set in the executable constructor is what Hacknet uses to find
the binary when the player types the filename. It must match the .exe filename
without the extension. Current required values:
  GPCrackBaseV2: SSHcrack_v2 / FTPBounce_v2 / WebServerWorm_v2
  GPCrackBaseV3: SSHcrack_v3 / FTPBounce_v3 / WebServerWorm_v3

## Energize — Design (Implementation in M2)
After a layer or port is hacked, defenses that reset (via admin) drop one level.
Energized state prevents the level drop — defense resets to same level, energize consumed.
One energized item per node.
Flag schema: gp_energized_<nodeID> = "proxy|firewall|ssh|ftp|web|..." or empty.

## M1 Completion Criteria
- [ ] SSHcrack_v2/FTPBounce_v2/WebServerWorm_v2 run and crack V2 ports
- [ ] SSHcrack_v3/FTPBounce_v3/WebServerWorm_v3 run and crack V3 ports
- [ ] V3 rejects missing key file with clear message
- [ ] Crack progress bar renders for both V2 and V3
- [ ] gp_test_node fully crackable end-to-end, porthack succeeds
- [ ] CPU tier flag (gp_cpu_t2) visibly changes crack speed
- [ ] SENTINEL mission loads, completion registers on porthack
