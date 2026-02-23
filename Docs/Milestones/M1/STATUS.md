# M1 Status

**Last updated:** 2026-02-22
**Status:** BLOCKED ON BUILD → fix committed, needs deploy + retest

---

## Current Goal

Get all 6 GP crack executables (`GPSSHv2`, `GPFTPv2`, `GPWebv2`, `GPSSHv3`, `GPFTPv3`, `GPWebv3`) launching from the terminal on `gp_test_node` (10.0.0.10) without "No Command X - Check Syntax".

---

## Root Cause Found (this session)

The executables were never registering. Pathfinder 5.x **does not scan** `[Pathfinder.Meta.Load.Executable]` attributes automatically. Those attributes are deprecated/no-op. The fix is explicit registration via `ExecutableManager.RegisterExecutable<T>(xmlName)` in `Load()`.

**What was happening:**
1. `FileContents="#GP_SSH_V2#"` in `StartingActions.xml` is an XML token
2. Pathfinder fires a `TextReplaceEvent` at extension load to swap the token → binary exe data
3. That replacement only happens if `#GP_SSH_V2#` is registered as a `XmlId` in `ExecutableManager`
4. Our attributes were never scanned → nothing registered → files stored with literal `"#GP_SSH_V2#"` as content
5. When the exe is run, Pathfinder's `ExecutableExecuteEvent` fires, looks up the binary content → finds nothing → falls through → "No Command GPWebv2"

---

## Fix Applied (needs build + deploy)

**File:** `Plugin/GatekeeperProtocol.cs`

Added in `Load()`:
```csharp
ExecutableManager.RegisterExecutable<GPSSHCrackV2>("#GP_SSH_V2#");
ExecutableManager.RegisterExecutable<GPFTPCrackV2>("#GP_FTP_V2#");
ExecutableManager.RegisterExecutable<GPWebCrackV2>("#GP_WEB_V2#");
ExecutableManager.RegisterExecutable<GPSSHCrackV3>("#GP_SSH_V3#");
ExecutableManager.RegisterExecutable<GPFTPCrackV3>("#GP_FTP_V3#");
ExecutableManager.RegisterExecutable<GPWebCrackV3>("#GP_WEB_V3#");
```

Removed all 6 `[Pathfinder.Meta.Load.Executable(...)]` attributes (they compile but do nothing).

---

## What to Confirm in Log After Rebuild

```
[GP] Registered 6 executables: GP_SSH/FTP/WEB_V2, GP_SSH/FTP/WEB_V3
```
This line appears between ports and commands in the startup sequence.

---

## Test Sequence (after deploy + fresh game)

> **Must start a fresh game** — existing save has the literal `#GP_SSH_V2#` strings burned in as file content. A new game triggers `StartingActions.xml` again so Pathfinder's TextReplaceEvent fires with the now-registered tokens.

```
gp_debug                    ← no connection needed; confirms Pathfinder commands work
connect 10.0.0.10           ← gp_test_node, not 252.77.206.31
gp_debug                    ← should show 6 GP ports (CLOSED)
GPSSHv2                     ← should open a 10s progress bar (V2 BREACH)
gp_debug                    ← ssh_v2 should show OPEN
GPSSHv3                     ← should prompt "Key file required: ssh_v3_key.dat"
```

V3 key files are pre-seeded in `/home` for M1 testing (see `StartingActions.xml`). If the key prompt shows, `scp ssh_v3_key.dat` from `/home` to wherever the exe looks, or verify the `/home` path is the player's home.

---

## Secondary Issue (ZeroDayToolKit)

ZDTK's `DisableCustomCommand` patch was previously blocking executables. The cfg fix (`GatekeeperProtocol = true`) is in place. The log no longer shows `Extensions/GatekeeperProtocol: False`, suggesting the cfg is being read. This issue is likely resolved but should be confirmed alongside the exe fix.

---

## M1 Completion Criteria Remaining

| Task | State |
|---|---|
| 6 custom ports register on startup | DONE (confirmed in log) |
| `gp_debug` prints hardware + port panel | DONE (code correct, untested) |
| V2 crackers run 10s bar and open port | **NEEDS TEST** (fix just applied) |
| V3 crackers check key file and open port | **NEEDS TEST** (fix just applied) |
| `gp_test_node` has V2 + V3 ports in XML | DONE |
| `gp_relay_alpha` holds V3 key files | DONE |
| CPU multiplier scales crack time | NEEDS TEST |

M1 is one successful test run away from done.
