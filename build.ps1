#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build script for PhantomExe.ILVirt.Tool
.DESCRIPTION
    Builds the PhantomExe IL virtualization tool with proper error handling,
    dependency management, and build diagnostics.
.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Debug
.PARAMETER SkipClean
    Skip cleaning bin/obj directories before build
.PARAMETER Rebuild
    Force a complete rebuild
.PARAMETER Restore
    Force NuGet package restore
.PARAMETER VerboseBuild
    Enable verbose build output
.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Release
    .\build.ps1 -Rebuild -VerboseBuild
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [switch]$SkipClean,
    [switch]$Rebuild,
    [switch]$Restore,
    [switch]$VerboseBuild
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# ============================================================================
# Configuration
# ============================================================================

$script:BuildStartTime = Get-Date
$script:ProjectName = "PhantomExe.ILVirt.Tool"
$script:MinDotNetVersion = [version]"8.0.0"

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Section {
    param([string]$Message, [string]$Color = "Cyan")
    Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor $Color
    Write-Host " $Message" -ForegroundColor $Color
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor $Color
}

function Write-Info {
    param([string]$Message)
    Write-Host "  ℹ️  $Message" -ForegroundColor Gray
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✅ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  ⚠️  $Message" -ForegroundColor Yellow
}

function Write-Failure {
    param([string]$Message)
    Write-Host "  ❌ $Message" -ForegroundColor Red
}

function Test-Command {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

function Get-ElapsedTime {
    $elapsed = (Get-Date) - $script:BuildStartTime
    return "{0:mm\:ss\.fff}" -f $elapsed
}

# ============================================================================
# Validation
# ============================================================================

function Test-DotNetSdk {
    Write-Section "Validating Environment"
    
    if (-not (Test-Command "dotnet")) {
        Write-Failure ".NET SDK not found"
        Write-Host "`nPlease install .NET SDK from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
        exit 1
    }
    
    try {
        $sdkVersion = (dotnet --version 2>&1) -replace '[^\d.].*$'
        $version = [version]$sdkVersion
        
        if ($version -lt $script:MinDotNetVersion) {
            Write-Failure "SDK version $version is below minimum required $script:MinDotNetVersion"
            exit 1
        }
        
        Write-Success ".NET SDK: $sdkVersion"
    }
    catch {
        Write-Failure "Failed to detect .NET SDK version"
        Write-Host "  Run 'dotnet --version' manually to diagnose" -ForegroundColor Yellow
        exit 1
    }
    
    # Check for required workloads
    $workloads = dotnet workload list 2>&1 | Out-String
    if ($workloads -match "No workloads installed") {
        Write-Info "No workloads required for this project"
    }
}

function Find-ProjectFile {
    Write-Section "Locating Project"
    
    $searchPaths = @(
        "src/$script:ProjectName/$script:ProjectName.csproj",
        "src/src/$script:ProjectName/$script:ProjectName.csproj",
        "$script:ProjectName/$script:ProjectName.csproj",
        "$script:ProjectName.csproj"
    )
    
    foreach ($path in $searchPaths) {
        if (Test-Path $path) {
            $fullPath = Resolve-Path $path
            Write-Success "Found: $fullPath"
            return $fullPath.Path
        }
    }
    
    # Fallback: search recursively
    Write-Warning "Project not found in standard locations, searching..."
    $found = Get-ChildItem -Recurse -Filter "$script:ProjectName.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1
    
    if ($found) {
        Write-Success "Found: $($found.FullName)"
        return $found.FullName
    }
    
    Write-Failure "Project file not found: $script:ProjectName.csproj"
    Write-Host "`nAvailable .csproj files:" -ForegroundColor Yellow
    Get-ChildItem -Recurse -Filter "*.csproj" -ErrorAction SilentlyContinue | 
        ForEach-Object { Write-Host "  → $($_.FullName)" -ForegroundColor DarkGray }
    exit 1
}

# ============================================================================
# Build Operations
# ============================================================================

function Invoke-Clean {
    param([string]$ProjectDir)
    
    if ($SkipClean -and -not $Rebuild) {
        Write-Info "Skipping clean (use -SkipClean:$false to clean)"
        return
    }
    
    Write-Section "Cleaning Build Artifacts"
    
    # Kill processes that may lock files
    $processes = @("dotnet", "msbuild", "MSBuild", $script:ProjectName)
    foreach ($proc in $processes) {
        $running = Get-Process -Name $proc -ErrorAction SilentlyContinue
        if ($running) {
            Write-Info "Stopping $proc processes..."
            $running | Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
        }
    }
    
    # Clean project directories
    $dirsToClean = @("bin", "obj")
    $cleaned = 0
    
    foreach ($dir in $dirsToClean) {
        $path = Join-Path $ProjectDir $dir
        if (Test-Path $path) {
            try {
                Remove-Item $path -Recurse -Force -ErrorAction Stop
                Write-Success "Removed $dir/"
                $cleaned++
            }
            catch {
                Write-Warning "Failed to remove $dir/: $_"
            }
        }
    }
    
    if ($cleaned -eq 0) {
        Write-Info "No build artifacts to clean"
    }
    
    # Clear NuGet caches if rebuilding
    if ($Rebuild) {
        Write-Info "Clearing NuGet caches..."
        dotnet nuget locals all --clear 2>&1 | Out-Null
        Write-Success "NuGet caches cleared"
    }
}

function Invoke-Restore {
    param([string]$ProjectFile)
    
    if ($Restore -or $Rebuild) {
        Write-Section "Restoring NuGet Packages"
        
        $restoreArgs = @(
            "restore",
            $ProjectFile,
            "--verbosity", "minimal"
        )
        
        if ($Rebuild) {
            $restoreArgs += "--force"
        }
        
        & dotnet $restoreArgs
        
        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Package restore failed"
            exit 1
        }
        
        Write-Success "Packages restored"
    }
    else {
        Write-Info "Skipping restore (use -Restore to force)"
    }
}

function Invoke-Build {
    param([string]$ProjectFile)
    
    Write-Section "Building Project"
    Write-Info "Configuration: $Configuration"
    Write-Info "Project: $(Split-Path $ProjectFile -Leaf)"
    
    $buildArgs = @(
        "build",
        $ProjectFile,
        "-c", $Configuration,
        "--no-incremental"
    )
    
    if ($VerboseBuild) {
        $buildArgs += "--verbosity", "detailed"
    }
    else {
        $buildArgs += "--verbosity", "minimal"
    }
    
    if ($Rebuild) {
        $buildArgs += "--no-restore"
    }
    
    # Execute build
    Write-Host ""
    & dotnet $buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Failure "Build failed with exit code $LASTEXITCODE"
        
        # Provide helpful diagnostics
        Write-Host "`nTroubleshooting tips:" -ForegroundColor Yellow
        Write-Host "  1. Run: .\build.ps1 -Rebuild -VerboseBuild" -ForegroundColor Gray
        Write-Host "  2. Check missing NuGet packages" -ForegroundColor Gray
        Write-Host "  3. Verify .csproj references" -ForegroundColor Gray
        Write-Host "  4. Review error messages above" -ForegroundColor Gray
        
        exit 1
    }
}

function Show-BuildSummary {
    param([string]$ProjectFile)
    
    Write-Section "Build Summary" -Color Green
    
    $projectDir = Split-Path $ProjectFile
    $elapsed = Get-ElapsedTime
    
    Write-Success "Build completed in $elapsed"
    Write-Host ""
    
    # Find output assemblies
    $outputPattern = Join-Path $projectDir "bin/$Configuration/*/$script:ProjectName.dll"
    $assemblies = Get-ChildItem $outputPattern -ErrorAction SilentlyContinue
    
    if ($assemblies) {
        Write-Host "  📦 Output:" -ForegroundColor Cyan
        foreach ($asm in $assemblies) {
            $tfm = Split-Path (Split-Path $asm.FullName) -Leaf
            $size = "{0:N2} KB" -f ($asm.Length / 1KB)
            Write-Host "     • $tfm`: " -NoNewline -ForegroundColor Gray
            Write-Host "$($asm.Directory.FullName)\" -NoNewline -ForegroundColor DarkGray
            Write-Host $asm.Name -NoNewline -ForegroundColor White
            Write-Host " ($size)" -ForegroundColor DarkGray
        }
    }
    
    # Find executables
    $exePattern = Join-Path $projectDir "bin/$Configuration/*/$script:ProjectName.exe"
    $executables = Get-ChildItem $exePattern -ErrorAction SilentlyContinue
    
    if ($executables) {
        Write-Host ""
        Write-Host "  🚀 Executable:" -ForegroundColor Cyan
        foreach ($exe in $executables) {
            Write-Host "     $($exe.FullName)" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "  To run: " -NoNewline -ForegroundColor Gray
    Write-Host "dotnet run --project `"$ProjectFile`" -- <args>" -ForegroundColor White
    Write-Host ""
}

# ============================================================================
# Main Execution
# ============================================================================

try {
    Write-Host ""
    Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║        PhantomExe.ILVirt.Tool Build Script               ║" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    
    # Validate environment
    Test-DotNetSdk
    
    # Locate project
    $projectFile = Find-ProjectFile
    $projectDir = Split-Path $projectFile
    
    # Execute build pipeline
    Invoke-Clean -ProjectDir $projectDir
    Invoke-Restore -ProjectFile $projectFile
    Invoke-Build -ProjectFile $projectFile
    
    # Show results
    Show-BuildSummary -ProjectFile $projectFile
    
    exit 0
}
catch {
    Write-Host ""
    Write-Failure "Build script failed: $_"
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
finally {
    # Cleanup
    $ProgressPreference = "Continue"
}