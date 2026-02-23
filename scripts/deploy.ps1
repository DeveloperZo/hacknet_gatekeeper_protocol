# =============================================================================
# deploy.ps1 — Build and deploy GatekeeperProtocol to Hacknet BepInEx plugins
# Run from project root: .\scripts\deploy.ps1
# =============================================================================

$ErrorActionPreference = "Stop"

$ProjectDir  = "$PSScriptRoot\..\Plugin"
$ProjFile    = "$ProjectDir\GatekeeperProtocol.csproj"
$ObjDll      = "$ProjectDir\obj\x86\Debug\GatekeeperProtocol.dll"
$ObjPdb      = "$ProjectDir\obj\x86\Debug\GatekeeperProtocol.pdb"
$PluginsDir  = "D:\SteamLibrary\steamapps\common\Hacknet\BepInEx\plugins"
$LogOutput   = "D:\SteamLibrary\steamapps\common\Hacknet\BepInEx\LogOutput.log"
$MSBuild     = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"

# --- Step 1: Kill Hacknet if running ---
$hacknet = Get-Process -Name "Hacknet" -ErrorAction SilentlyContinue
if ($hacknet) {
    Write-Host "[deploy] Stopping Hacknet (PID $($hacknet.Id))..." -ForegroundColor Yellow
    Stop-Process -Id $hacknet.Id -Force
    Start-Sleep -Seconds 2
} else {
    Write-Host "[deploy] Hacknet not running." -ForegroundColor Gray
}

# --- Step 2: Build ---
Write-Host "[deploy] Building Debug|x86..." -ForegroundColor Cyan
& $MSBuild $ProjFile /p:Configuration=Debug /p:Platform=x86 /v:minimal
if ($LASTEXITCODE -ne 0) {
    # Compile succeeded but copy may have failed — check if obj exists
    if (-not (Test-Path $ObjDll)) {
        Write-Host "[deploy] BUILD FAILED — no DLL produced." -ForegroundColor Red
        exit 1
    }
    Write-Host "[deploy] Compile OK (copy step failed — will copy manually)." -ForegroundColor Yellow
} else {
    Write-Host "[deploy] Build succeeded." -ForegroundColor Green
}

# --- Step 3: Copy to plugins (handles locked-file case) ---
Write-Host "[deploy] Copying to $PluginsDir..." -ForegroundColor Cyan
Copy-Item $ObjDll "$PluginsDir\GatekeeperProtocol.dll" -Force
if (Test-Path $ObjPdb) {
    Copy-Item $ObjPdb "$PluginsDir\GatekeeperProtocol.pdb" -Force
}
$dll = Get-Item "$PluginsDir\GatekeeperProtocol.dll"
Write-Host "[deploy] DLL deployed: $($dll.Length) bytes @ $($dll.LastWriteTime)" -ForegroundColor Green

# --- Step 4: Relaunch Hacknet ---
Write-Host "[deploy] Launching Hacknet..." -ForegroundColor Cyan
Start-Process "steam://rungameid/365450"

# --- Step 5: Wait and tail log ---
Write-Host "[deploy] Waiting 30s for BepInEx to initialize..." -ForegroundColor Gray
Start-Sleep -Seconds 30

if (Test-Path $LogOutput) {
    Write-Host ""
    Write-Host "[deploy] === BepInEx LogOutput.log ===" -ForegroundColor Cyan
    Get-Content $LogOutput | Select-String "GP|Gatekeeper|Error|FAILED" | ForEach-Object {
        $line = $_.ToString()
        if ($line -match "Error|FAILED") {
            Write-Host $line -ForegroundColor Red
        } elseif ($line -match "GP") {
            Write-Host $line -ForegroundColor Green
        } else {
            Write-Host $line
        }
    }
    Write-Host "[deploy] ===================================" -ForegroundColor Cyan
} else {
    Write-Host "[deploy] LogOutput.log not found — check game launched correctly." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[deploy] Done. Load the GP Extension in-game and test." -ForegroundColor Green
