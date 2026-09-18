[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path }
$baselinePath = Join-Path $Root 'Docs\CommanderPhase4C-source-baseline.json'
if (-not (Test-Path -LiteralPath $baselinePath)) { throw "Missing baseline: $baselinePath" }
$baseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json

function Hash-File([string]$path) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $path))) { return $null }
    return (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Root $path)).Hash.ToUpperInvariant()
}

$baselineMap = @{}
foreach ($item in $baseline.files) { $baselineMap[$item.path] = $item.sha256.ToUpperInvariant() }
$sourceRoots = @((Join-Path $Root 'Assets\Scripts'), (Join-Path $Root 'Assets\Tests'))
$currentFiles = @(foreach ($sourceRoot in $sourceRoots) {
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter *.cs |
        ForEach-Object { $_.FullName.Substring($Root.Length + 1).Replace('\','/') }
})
$currentMap = @{}
foreach ($path in $currentFiles) { $currentMap[$path] = Hash-File $path }

$changed = @(); $missing = @(); $new = @()
foreach ($path in $baselineMap.Keys) {
    if (-not $currentMap.ContainsKey($path)) { $missing += [pscustomobject]@{ path = $path; baselineSha256 = $baselineMap[$path] }; continue }
    if ($currentMap[$path] -ne $baselineMap[$path]) {
        $changed += [pscustomobject]@{ path = $path; baselineSha256 = $baselineMap[$path]; currentSha256 = $currentMap[$path] }
    }
}
foreach ($path in $currentMap.Keys | Where-Object { -not $baselineMap.ContainsKey($_) -and $_ -notmatch '(?i)baseline' }) {
    $new += [pscustomobject]@{ path = $path; currentSha256 = $currentMap[$path] }
}

$frozenNames = @($baselineMap.Keys | Where-Object {
    $_ -match '^Assets/Scripts/Commands/' -or $_ -match '^Assets/Scripts/Network/'
})
$frozenNames += @('Assets/Scripts/Core/GameSimulation.cs',
    'Assets/Scripts/AI/Commander/CommanderPlanner.cs',
    'Assets/Scripts/AI/Commander/CommanderGoalManager.cs')
$frozenNames = @($frozenNames | Select-Object -Unique)
$frozen = foreach ($path in $frozenNames) {
    $base = $baselineMap[$path]; $now = $currentMap[$path]
    [pscustomobject]@{ path = $path; baselineSha256 = $base; currentSha256 = $now; present = ($null -ne $now); unchanged = ($null -ne $base -and $base -eq $now); auditGap = ($null -eq $base -or $null -eq $now) }
}

$auditFiles = @($currentMap.Keys | Where-Object {
    $_ -match '^Assets/Scripts/AI/Commander/Phase4A/.*(Provider|CommanderChatUI)\.cs$' -or
    $_ -match '^Assets/Scripts/AI/Commander/Phase4B1/.*(Provider|StrategicAIInterpreter|StrategicAIInterpreterFactory)\.cs$' -or
    $_ -match '^Assets/Scripts/AI/Commander/Phase4B2/(StrategicAIApprovalBridge|CommanderIntentRouter|StrategicApprovalLayer)\.cs$' -or
    $_ -match '^Assets/Scripts/AI/Commander/Phase4C/.*\.cs$'
})
$forbidden = '\b(GameSimulation|CommandBuffer|ICommand|StrategicPlanner|StrategicPipeline|CommanderGoalManager)\b'
$references = @()
foreach ($path in $auditFiles) {
    $full = Join-Path $Root ($path -replace '/', '\'); $lineNo = 0
    foreach ($line in Get-Content -LiteralPath $full) {
        $lineNo++
        if ($line -cnotmatch $forbidden) { continue }
        $symbol = if ($line -match '\b(class|interface|struct|enum|void|Task|public|private|protected|internal)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(') { $Matches[2] } elseif ($line -match '\b(class|interface|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)') { $Matches[2] } else { 'unresolved-symbol' }
        $classification = if ($path -match 'CommanderChatUI|StrategicContextBuilder|StrategicPlanner') { 'documented-host-candidate' } else { 'advisory-reference-candidate' }
        $references += [pscustomobject]@{ path = $path; line = $lineNo; symbol = $symbol; classification = $classification; matchedSymbols = @([regex]::Matches($line, $forbidden) | ForEach-Object Value | Select-Object -Unique) }
    }
}

# Counts only: values are never emitted. Exclude this script so its detector regexes cannot self-match.
$credentialPatterns = [ordered]@{
    highConfidenceShapes = @('AIza[0-9A-Za-z_-]{20,}', 'sk-[0-9A-Za-z]{20,}', 'AQ\.[0-9A-Za-z_-]{20,}')
    assignmentCandidates = @('(?i)(api[_-]?key|secret|password|token)\s*[:=]\s*["''][^"'']{8,}')
}
$credentialCounts = [ordered]@{}
foreach ($kind in $credentialPatterns.Keys) {
    $patterns = $credentialPatterns[$kind]
    $count = 0
    $scanRoots = @((Join-Path $Root 'Assets\Scripts'), (Join-Path $Root 'Assets\Tests'), (Join-Path $Root 'Docs'))
    $files = @()
    foreach ($scanRoot in $scanRoots) { foreach ($extension in @('*.cs','*.md','*.json')) {
        $files += Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Filter $extension
    }}
    $files = @($files | Where-Object { $_.FullName -notmatch '\\Docs\\CommanderPhase4C\\check-boundaries\.ps1$' -and $_.FullName -notmatch '\\.env$' })
    foreach ($file in $files) { foreach ($pattern in $patterns) {
        $text = Get-Content -Raw -LiteralPath $file.FullName
        if ($null -eq $text) { $text = '' }
        $count += ([regex]::Matches($text, $pattern)).Count
    }}
    $credentialCounts[$kind] = $count
}

$envPath = Join-Path $Root '.env'
$repoRoot = (Split-Path -Parent $Root).Replace('\','/')
$gitStatus = [ordered]@{ available = $true; error = $null; tracked = $null; ignored = $null }
$projectRel = (Split-Path $Root -Leaf).Replace('\','/')
$savedErrorAction = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
$trackedOutput = @(& git -C $repoRoot -c "safe.directory=$repoRoot" ls-files -- "$projectRel/.env" 2>$null)
$trackedExit = $LASTEXITCODE
& git -C $repoRoot -c "safe.directory=$repoRoot" check-ignore -q -- "$projectRel/.env" 2>$null
$ignoredExit = $LASTEXITCODE
$ErrorActionPreference = $savedErrorAction
if ($trackedExit -eq 0) { $gitStatus.tracked = ($trackedOutput.Count -gt 0) } else { $gitStatus.available = $false; $gitStatus.error = 'git tracked-state query unavailable' }
if ($ignoredExit -in @(0,1)) { $gitStatus.ignored = ($ignoredExit -eq 0) } else { $gitStatus.available = $false; $gitStatus.error = 'git ignore-state query unavailable' }

[pscustomobject]@{
    audit = 'Commander Phase 4C read-only boundary audit (in-flight source; not final phase evidence)'
    generatedAt = (Get-Date).ToUniversalTime().ToString('o')
    baseline = [pscustomobject]@{ path = 'Docs/CommanderPhase4C-source-baseline.json'; schemaVersion = $baseline.schemaVersion; fileCount = $baseline.fileCount; schemaValid = ($baseline.PSObject.Properties.Name -contains 'schemaVersion' -and $baseline.PSObject.Properties.Name -contains 'files') }
    files = [pscustomobject]@{ changed = @($changed); new = @($new); missing = @($missing) }
    frozenBoundaryHashes = @($frozen)
    advisoryForbiddenReferences = @($references)
    credentialShapeCounts = $credentialCounts
    env = [pscustomobject]@{ present = (Test-Path -LiteralPath $envPath); git = [pscustomobject]$gitStatus }
    patternSelfTest = [pscustomobject]@{ aqLiteralDotDetected = ([regex]::Matches('AQ.' + ('A' * 24), $credentialPatterns.highConfidenceShapes[2]).Count -eq 1) }
    documentedBoundaryExceptions = @('CommanderChatUI host integration', 'StrategicContextBuilder game-owned snapshot boundary', 'StrategicPlanner game-owned planning boundary')
} | ConvertTo-Json -Depth 8
