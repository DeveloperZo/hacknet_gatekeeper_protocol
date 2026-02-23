---
name: build-and-deploy
description: Builds the GatekeeperProtocol.cs plugin and deploys the DLL to Hacknet BepInEx plugins. Use when the user asks to build, compile, deploy, rebuild, or test changes in-game. Also use after any edit to Plugin/*.cs.
---

# Build and Deploy

The game locks the DLL while running. The deploy script handles: kill Hacknet → MSBuild → copy → relaunch.

## Quick Deploy

Run from the project root (`c:\Users\awill\Hacknet_GP`):

```powershell
.\scripts\deploy.ps1
```

This does everything automatically. Read the output for any compile errors.

## What the Script Does

1. Finds the Hacknet process and kills it (if running)
2. Runs MSBuild with `Debug|x86` config
3. If compile succeeds: copies DLL + PDB from `obj\x86\Debug\` to `BepInEx\plugins\`
4. Relaunches Hacknet via Steam
5. Waits 30s then tails `LogOutput.log` to confirm plugin loaded

## Manual Steps (if script fails)

```powershell
# 1. Kill Hacknet
Stop-Process -Name "Hacknet" -Force -ErrorAction SilentlyContinue

# 2. Build
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
& $msbuild "c:\Users\awill\Hacknet_GP\Plugin\GatekeeperProtocol.csproj" /p:Configuration=Debug /p:Platform=x86 /v:minimal

# 3. Copy (only needed if Hacknet was running during build)
Copy-Item "c:\Users\awill\Hacknet_GP\Plugin\obj\x86\Debug\GatekeeperProtocol.dll" `
          "D:\SteamLibrary\steamapps\common\Hacknet\BepInEx\plugins\" -Force

# 4. Relaunch
Start-Process "steam://rungameid/365450"
```

## Verify Success

Check `LogOutput.log` for:
```
[Info   :Gatekeeper Protocol] [GP] M1 plugin loaded.
```

If you see an error on our plugin entry, read the full stack trace — it names the exact line.

## Common Failures

| Symptom | Cause | Fix |
|---------|-------|-----|
| `MSB4057: target Build does not exist` | Import tag missing from csproj | Ensure `<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />` is at bottom of csproj |
| `CS0246: type not found BepInEx` | Wrong TargetFrameworkVersion | Must be `v4.7.2` not `v4.0` |
| Copy fails after compile | Hacknet still running | Kill process first, then copy from `obj\x86\Debug\` manually |
| Plugin loads but command missing | Executable token unrecognized | Verify `[Executable("...")]` token matches `AddAsset FileContents` exactly |
