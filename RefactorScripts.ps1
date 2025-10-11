# PowerShell Script to Refactor all C# files in Assets/Scripts
# Adds namespaces and organizes with regions

$scriptsPath = "C:\Users\Nnican\Documents\LD58 (Fly Collector)\Assets\Scripts"

# Namespace mapping based on folder structure
$namespaceMap = @{
    "BugAI" = "BugAI"
    "BugCatching" = "BugCatching"
    "BugData" = "BugData"
    "BugRuntime" = "BugRuntime"
    "CameraCore" = "CameraCore"
    "CameraFocus" = "CameraFocus"
    "CameraInspect" = "CameraInspect"
    "CameraStates" = "CameraStates"
    "Core" = "Core"
    "Environment" = "Environment"
    "InteractionActions" = "InteractionActions"
    "InteractionConditions" = "InteractionConditions"
    "InteractionCore" = "InteractionCore"
    "InteractionObjects" = "InteractionObjects"
    "Inventory" = "Inventory"
    "Scene" = "Scene"
    "UI" = "UI"
}

function Get-Namespace {
    param([string]$FilePath)

    foreach ($key in $namespaceMap.Keys) {
        if ($FilePath -like "*\$key\*") {
            return $namespaceMap[$key]
        }
    }
    return $null
}

function Test-HasNamespace {
    param([string[]]$Lines)

    foreach ($line in $Lines) {
        if ($line -match '^\s*namespace\s+\w+') {
            return $true
        }
    }
    return $false
}

function Process-CSharpFile {
    param([string]$FilePath)

    Write-Host "Processing: $FilePath"

    # Skip if already processed (has namespace)
    $content = Get-Content $FilePath -Raw
    $lines = Get-Content $FilePath

    if (Test-HasNamespace -Lines $lines) {
        Write-Host "  ⏭️  Already has namespace, skipping"
        return
    }

    $namespace = Get-Namespace -FilePath $FilePath
    if (-not $namespace) {
        Write-Host "  ⚠️  Could not determine namespace, skipping"
        return
    }

    # For now, just report what would be done
    Write-Host "  ✅ Would add namespace: $namespace"
}

# Find all C# files
$files = Get-ChildItem -Path $scriptsPath -Filter "*.cs" -Recurse

Write-Host "Found $($files.Count) C# files"
Write-Host "================================"

foreach ($file in $files) {
    Process-CSharpFile -FilePath $file.FullName
}

Write-Host "================================"
Write-Host "Processing complete!"
