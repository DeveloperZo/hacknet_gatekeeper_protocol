---
name: hacknet-gp-node
description: Generates a well-formed Hacknet Computer XML node for the Gatekeeper Protocol extension. Use when the user asks to create a new node, test node, relay node, Gatekeeper node, or GP network node. Also use when adding nodes to the Extension/Nodes/ directory.
---

# Hacknet GP Node Generator

## Node Types in GP

| Type | Purpose | Key Properties |
|------|---------|---------------|
| `gp_test_*` | Dev testing — exercises port types | Short trace (30s), progress admin |
| `gp_relay_*` | Holds v3 key files | Easy to crack, long trace |
| `gp_target_*` | M1 mission targets | Standard + v2 ports |
| `gk_node_*` | Gatekeeper-protected (M2+) | V2 + CORE port, tracker tag |
| `hw_vendor` | Upgrade shop (M3+) | No hacking required, daemon |

## Before Generating — Gather

1. **Node ID** (e.g. `gp_test_v2`) — used in actions and links
2. **Port tier**: standard only / v2 / v3 / CORE (M2)
3. **Trace time**: 30s (entry) / 60s (mid) / 120s (high) / 180s (gatekeeper)
4. **Admin type**: `progress` (GP nodes) / `fast` (gatekeeper) / `none` (one-shot)
5. **Contains key files?** — relay nodes hold `ssh_key_v3/ftp/web.dat`
6. **Network links** (`<dlink>`) to other nodes

## Template — V2 Port Node

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Computer id="NODE_ID"
          name="DISPLAY NAME"
          ip="10.0.X.X"
          security="3"
          allowsDefaultBootModule="false"
          type="1">

  <adminPass pass="admin" />

  <ports>22, 21, 80</ports>
  <PFPorts>ssh_v2:10022, ftp_v2:10021, web_v2:10080</PFPorts>
  <portsForCrack val="3" />
  <trace time="30" />

  <admin type="progress" resetPassword="false" isSuper="false" />

  <file path="home" name="README.txt">DESCRIPTION OF NODE PURPOSE</file>

</Computer>
```

## Template — V3 Relay Node (holds key files)

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Computer id="NODE_ID"
          name="DISPLAY NAME"
          ip="10.0.X.X"
          security="1"
          allowsDefaultBootModule="false"
          type="3">

  <adminPass pass="relay" />
  <ports>22, 21</ports>
  <portsForCrack val="2" />
  <trace time="120" />
  <admin type="progress" resetPassword="false" isSuper="false" />

  <file path="home" name="ssh_key_v3.dat">V3-KEY:SSH:ID-0xXXXX</file>
  <file path="home" name="ftp_key_v3.dat">V3-KEY:FTP:ID-0xXXXX</file>
  <file path="home" name="web_key_v3.dat">V3-KEY:WEB:ID-0xXXXX</file>

</Computer>
```

## Port Number Reference

```
Standard (vanilla):   22=ssh  21=ftp  80=web  25=smtp  1433=sql
V2 (GP M1):     ssh_v2:10022  ftp_v2:10021  web_v2:10080
V3 (GP M1):       ssh_v3:20022    ftp_v3:20021    web_v3:20080
CORE (GP M2):         core_v3:2001    core_vault:2002     core_sovereign:2003
```

## After Creating the Node

1. Add to `Extension/Nodes/GP/` (create the `GP/` subfolder if it doesn't exist)
2. Add the node ID to `StartingVisibleNodes` in `ExtensionInfo.xml` if it should appear on the netmap
3. Add `<dlink target="NODE_ID" />` on the node it should connect from
