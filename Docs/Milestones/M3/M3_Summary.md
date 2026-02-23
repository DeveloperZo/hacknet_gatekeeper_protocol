# M3 — Networks + Hardware RPG + Economy

## Scope
Multi-network traversal, network bonuses/debuffs, shop and hardware upgrade system,
income generation from held networks. Loss formula from M2 becomes live — currency
and hardware deductions now actually apply. ZDTK re-enabled and compatibility resolved.

## Networks

### Multi-Network Traversal
Three named networks: h4ck, th3, p14n3t.
Plus player's home network.

Player can list all known networks via gp_networks command.
Reuses existing map UI — each network renders in a distinct color.
Selecting a network drills into its node map.
Player traverses between networks by cracking border/gateway nodes.

### Network Bonuses and Debuffs
Each network has a profile applied to all member nodes:
- Crack speed modifier (affects how fast player cracks nodes in that network)
- Trace speed modifier (affects how fast trace runs on player in that network)
- Reward multiplier (affects resource drops from nodes in that network)
- Defense difficulty modifier (affects base defense levels of nodes)

Example profiles:
- h4ck: fast crack, fast trace, high rewards, medium defense
- th3: medium crack, slow trace, medium rewards, high defense
- p14n3t: slow crack, very slow trace, low rewards, low defense

Network profiles are set at run start with minor random variance per seed.

### Player Home Network
Player's nodes form their own named network.
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
- gp_networks lists all networks with correct color coding
- Network drill-down shows correct node map per network
- Network bonuses/debuffs apply correctly to crack speed, trace speed, rewards
- Player home network generates income per tick
- All four hardware components upgrade correctly via shop
- Resource drops scale with HDD tier and network reward modifier
- Loss formula applies real currency and hardware deductions
- Hardware floor prevents zero-out
- ZDTK compatibility confirmed — no Extensions menu crash
