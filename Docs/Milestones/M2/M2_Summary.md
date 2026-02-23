# M2 — Gatekeeper Attack

## Scope
Gatekeeper simulation engine, player home network, breach/planning/defense loop,
bluescreen resolution. Loss formula is live but currency/hardware impact is stubbed
(actual deduction happens in M3 when economy exists).

## The Flow

### 1. Breach Warning (Planning Phase)
Triggered by a scripted event or network milestone.
Gatekeeper is "in transit" — player has a fixed window (TBD, ~90s) before simulation starts.

Player receives:
- Full node list via gp_plan: node ID, current defense profile, estimated gatekeeper
  crack time at current config, node value score
- Ability to designate last-stand node (gp_setstand <nodeID>)
- Ability to set entry point — which node the gatekeeper enters from (gp_setentry <nodeID>)

During breach warning player can:
- Connect to own nodes and run reconfig commands (see Node Reconfig below)
- Run gp_scan on the gatekeeper node to get its speed profile

### 2. Gatekeeper Simulation
Runs in Update() hook as a state machine.
Gatekeeper traverses nodes in priority order starting from entry node.

Per-node crack progress advances each tick based on:
- Gatekeeper speed profile vs node defense flags (not Computer object values)
- Proxy: gatekeeper is slow — high resistance multiplier
- Firewall: gatekeeper is fast — low resistance multiplier
- SSH ports: gatekeeper is slow
- FTP/Web ports: moderate speed

When crack progress hits 1.0 on a node:
- Node is flagged as captured (gp_node_<id>_captured = true)
- If player is connected to that node: bluescreen triggered, mid-tier loss
- Gatekeeper moves to next node in traversal order

### 3. Player Counterplay — Node Reconfig
Player connects to own nodes (same crack mechanic, fast since player has admin).
Once connected, reconfig commands available:

**Downgrade/Remove:**
- gp_remove proxy / firewall / <porttype>
- Strips the defense entirely. Fast. Used to remove easy gatekeeper targets.

**Upgrade:**
- gp_upgrade proxy / firewall / <porttype>
- Increases defense level by 1 (up to tier max). Takes time proportional to tier.

**Energize:**
- gp_energize proxy / firewall / <porttype>
- Marks item as energized (flag: gp_energized_<nodeID> = <item>).
- One energized item per node — previous energized item loses state.
- Energized item: when defense resets after gatekeeper pass, level is preserved,
  energize is consumed.
- Non-energized item: when defense resets, drops one level.

**Executable Lock:**
If gatekeeper is currently cracking a node (gp_node_<id>_gk_active = true):
- Player can connect and observe (sees live proxy drain, port flips, firewall solve)
- All executables are locked: "ACCESS DENIED — hostile presence detected"
- Passive commands only: ls, cat, gp_plan, monitor

### 4. Monitoring
gp_monitor command (run from any node) prints live table:
- Node ID, defense profile, gatekeeper progress %, captured status
- Refreshes every 3 seconds
- Player can run this while connected elsewhere

### 5. Resolution — Bluescreen
Three trigger conditions:

**Player on captured node:**
Bluescreen immediately on node capture. Mid-tier loss.
Loss = base_loss * (1 - nodes_held/total_nodes) * decay_curve
Currency/hardware deduction stubbed — loss % calculated and printed, not applied.

**All nodes captured:**
Bluescreen. High loss. Fixed floor applied (cannot be zeroed).
Loss % calculated and printed, not applied.

**Player trace elapsed (last-stand node):**
Upload completes. Bluescreen. Low loss scaled by % nodes still held.
Loss % calculated and printed, not applied.

Loss formula (live, impact deferred to M3):
  base_loss * (1 - nodes_held/total_nodes) * decay_curve
  Hardware levels off at a floor — no zero-out mechanic.

## Flag Schema

### Node Defense Flags (per node)
- gp_node_<id>_proxy_level: 0-3 (0 = none)
- gp_node_<id>_firewall_level: 0-3
- gp_node_<id>_ssh_level: 0-3
- gp_node_<id>_ftp_level: 0-3
- gp_node_<id>_web_level: 0-3
- gp_node_<id>_energized: proxy|firewall|ssh|ftp|web|empty

### Gatekeeper State Flags
- gp_gk_active: true/false
- gp_gk_node_current: nodeID currently being cracked
- gp_node_<id>_gk_progress: 0.0-1.0 crack progress float
- gp_node_<id>_captured: true/false

### Network State Flags
- gp_stand_node: designated last-stand node ID
- gp_entry_node: designated gatekeeper entry node ID
- gp_trace_remaining: float seconds on upload trace timer
- gp_nodes_total: int
- gp_nodes_held: int (recalculated each tick)

### Gatekeeper Speed Profile (set at run start, read by gp_scan)
- gp_gk_proxy_resist: float (high = slow on proxies)
- gp_gk_firewall_resist: float (low = fast on firewalls)
- gp_gk_ssh_resist: float (high = slow on SSH)
- gp_gk_ftp_resist: float
- gp_gk_web_resist: float

## gp_scan Output (Example)
```
GATEKEEPER PROFILE — SCAN COMPLETE

Proxy resistance:    HIGH    [estimated crack time: 4x base]
Firewall resistance: LOW     [estimated crack time: 0.5x base]
SSH resistance:      HIGH    [estimated crack time: 3x base]
FTP resistance:      MEDIUM  [estimated crack time: 1x base]
Web resistance:      MEDIUM  [estimated crack time: 1x base]

Recommendation: strip firewalls, stack proxies and SSH ports on high-value nodes.
```

## Completion Criteria
- Breach warning triggers, gp_plan prints correct node list with estimates
- gp_scan returns gatekeeper profile
- Gatekeeper simulation advances crack progress per tick correctly
- Player can connect to gatekeeper-active node and observe without running executables
- Node reconfig commands work: remove, upgrade, energize
- Energize flag consumed correctly on reset, level preserved
- Non-energized defense drops one level on reset
- gp_monitor prints live state
- All three bluescreen conditions trigger correctly
- Loss % calculated and printed correctly (not applied to currency/hardware)
