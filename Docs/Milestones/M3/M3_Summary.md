# M3 — Networks + Hardware RPG + Economy

## Scope
Multi-network traversal, network bonuses/debuffs, shop and hardware upgrade system,
income generation from held networks. Loss formula from M2 becomes live — currency
and hardware deductions now actually apply. ZDTK re-enabled and compatibility resolved.

## Networks

### Full Vision — 7 Networks
The complete game ships 7 networks total (player home + 6 hostile).
Networks use generic identifiers for now; flavour names are a later polish pass.

| ID | Label | Notes |
|---|---|---|
| `net_home` | Home | Player's own network — always present |
| `net_01` | Network 01 | M3 slice — hostile |
| `net_02` | Network 02 | M3 slice — hostile |
| `net_03` | Network 03 | Later milestone |
| `net_04` | Network 04 | Later milestone |
| `net_05` | Network 05 | Later milestone |
| `net_06` | Network 06 | Later milestone |

### M3 Slice — 3 Networks
M3 implements the first 3 networks: `net_home` (player) + `net_01` and `net_02` (hostile).
Remaining 4 hostile networks are stubs — listed in `gp_networks` as LOCKED, no nodes.

### Multi-Network Traversal
Player can list all known networks via `gp_networks` command.
Reuses existing map UI — each network renders in a distinct color.
Selecting a network drills into its node map.
Player traverses between networks by cracking border/gateway nodes.

### Network Bonuses and Debuffs
Each network has a profile applied to all member nodes:
- Crack speed modifier (affects how fast player cracks nodes in that network)
- Trace speed modifier (affects how fast trace runs on player in that network)
- Reward multiplier (affects resource drops from nodes in that network)
- Defense difficulty modifier (affects base defense levels of nodes)

M3 example profiles (generic — tuned for balance, not flavour):
- `net_01`: fast crack, fast trace, medium rewards, low defense  *(entry network — accessible)*
- `net_02`: medium crack, medium trace, high rewards, high defense

Network profiles are set at run start with minor random variance per seed.

### Player Home Network
Player's nodes form their own named network (`net_home`).
Income generation: held nodes generate gp_scp passively over time.
Rate = base_rate * nodes_held * network_income_modifier.
Income ticks in Update() and writes to gp_wallet flag.

## Hardware RPG

### Components and Tiers
Four hardware components, three tiers each:

**CPU** (gp_cpu_tier)
- T1: 1.0x crack speed
- T2: 1.5x crack speed
- T3: 2.25x crack speed

**RAM** (gp_ram_tier)
- T1: 1 concurrent process
- T2: 2 concurrent processes
- T3: 3 concurrent processes
- Concurrent process = running a crack executable while another is in progress

**HDD** (gp_hdd_tier)
- T1: base exfil reward
- T2: 1.5x exfil reward
- T3: 2.25x exfil reward

**NIC** (gp_nic_tier)
- T1: base trace resistance (no modifier)
- T2: trace timer runs 20% slower against player
- T3: trace timer runs 40% slower against player

### Resource Drops
Nodes drop resources on crack: gp_scp (currency) and gp_parts (upgrade material).
Drop amounts scale with node defense level and network reward modifier.
HDD tier multiplies final drop amount.

### Shop
gp_shop command — available from player home node or any shop-flagged node.

Menu displays:
- Current hardware tiers and upgrade costs
- Script tier upgrade (T1→T2→T3) — costs gp_parts + gp_scp
- Hardware upgrades per component — costs gp_parts + gp_scp
- Consumables: extra key files, one-use energize tokens, trace jammers

Upgrade writes new tier flag immediately. No cooldown.

## Economy

### Currency: gp_scp
Earned from: node cracks, exfil rewards, passive income from held nodes.
Spent at: shop.
Tracked in flag: gp_wallet.

### Loss Formula — Now Live
Previously stubbed in M2, now applies actual deductions:
  loss = base_loss * (1 - nodes_held/total_nodes) * decay_curve
  currency lost = gp_wallet * loss
  hardware: each component has a chance to drop one tier based on loss magnitude
  floor: hardware cannot drop below T1, wallet cannot go below 10% of current value

### ZDTK Compatibility
ZDTK re-enabled. Locale entries provided for all GP port names and commands.
gp_locale.xml populated with all required keys.
Verified: Extensions menu loads with both GatekeeperProtocol.dll and ZeroDayToolKit.dll active.

## Completion Criteria
- `gp_networks` lists net_home + net_01 + net_02 (active) and net_03–net_06 (LOCKED)
- Each active network renders in a distinct color on the map
- Network drill-down shows correct node map for net_home, net_01, net_02
- Network bonuses/debuffs apply correctly to crack speed, trace speed, rewards
- Player home network (net_home) generates income per tick
- All four hardware components upgrade correctly via shop
- Resource drops scale with HDD tier and network reward modifier
- Loss formula applies real currency and hardware deductions
- Hardware floor prevents zero-out
- ZDTK compatibility confirmed — no Extensions menu crash
