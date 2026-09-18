[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$Output = (Join-Path $PSScriptRoot 'phase4c1-fix-review')
)

$ErrorActionPreference = 'Stop'
$embedded = Join-Path $PSScriptRoot 'embedded'
$beforeRoot = Join-Path $PSScriptRoot 'phase4c1-task1-before'
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('phase4c1-review-' + [guid]::NewGuid().ToString('N'))
$checkpoint = Join-Path $work 'checkpoint'
New-Item -ItemType Directory -Force -Path $checkpoint | Out-Null

$changed = @(
    'Assets/Scripts/AI/Commander/Phase4A/CommanderAIIntentAdapter.cs',
    'Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs',
    'Assets/Scripts/AI/Commander/Phase4B1/GeminiStrategicAIProvider.cs',
    'Assets/Scripts/AI/Commander/Phase4B1/MockStrategicAIProvider.cs',
    'Assets/Scripts/AI/Commander/Phase4B1/StrategicAIInterpreter.cs',
    'Assets/Scripts/AI/Commander/Phase4B2/CommanderIntentRouter.cs',
    'Assets/Scripts/AI/Commander/Phase4B2/StrategicAIApprovalBridge.cs'
)
$newFiles = @(
    'Assets/Scripts/AI/Commander/Phase4C/MemoryEntry.cs',
    'Assets/Scripts/AI/Commander/Phase4C/CommanderMemory.cs',
    'Assets/Scripts/AI/Commander/Phase4C/ConversationState.cs',
    'Assets/Tests/EditMode/CommanderPhase4C1Tests.cs',
    'Assets/Tests/PlayMode/CommanderPhase4C1PlayModeTests.cs'
)

try {
    foreach ($rel in $changed) {
        $dst = Join-Path $checkpoint ($rel -replace '/', '\\')
        New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
        Copy-Item -LiteralPath (Join-Path $beforeRoot (($rel -replace '/', '\\') + '.before.txt')) -Destination $dst
        $name = ($rel -replace '/', '_') + '.diff.txt'
        $diff = Get-Content -Raw -LiteralPath (Join-Path $embedded $name)
        $oldEsc = [regex]::Escape(('a/' + ($rel -replace '/', '\\') + '.before.txt'))
        $newEsc = [regex]::Escape(('b/' + ($rel -replace '/', '\\')))
        $lines = @($diff -split "`r?`n" | Where-Object { $_ -notmatch '^warning: ' })
        $lines[0] = 'diff --git a/' + $rel + ' b/' + $rel
        $lines[1] = 'index 0000000..0000000 100644'
        $lines[2] = '--- a/' + $rel
        $lines[3] = '+++ b/' + $rel
        $diff = ($lines -join "`n")
        $patchFile = Join-Path $work (($rel -replace '/', '_') + '.patch')
        Set-Content -LiteralPath $patchFile -Value $diff -NoNewline
        & git apply --whitespace=nowarn --directory=$checkpoint --unsafe-paths -- $patchFile
        if ($LASTEXITCODE -ne 0) { throw "Could not reconstruct checkpoint for $rel" }
    }
    foreach ($rel in $newFiles) {
        $dst = Join-Path $checkpoint ($rel -replace '/', '\\')
        New-Item -ItemType Directory -Force -Path (Split-Path $dst) | Out-Null
        $name = ($rel -replace '/', '_') + '.full.txt'
        Copy-Item -LiteralPath (Join-Path $embedded $name) -Destination $dst
    }

    New-Item -ItemType Directory -Force -Path $Output | Out-Null
    $manifest = @()
    foreach ($rel in ($changed + $newFiles)) {
        $current = Join-Path $Root ($rel -replace '/', '\\')
        $base = Join-Path $checkpoint ($rel -replace '/', '\\')
        $safe = $rel -replace '/', '_'
        $out = Join-Path $Output ($safe + '.fix.diff.txt')
        $diffOutput = @(& git diff --no-index --binary --unified=10 -- $base $current 2>$null)
        if ($LASTEXITCODE -notin @(0,1)) { throw "Could not diff $rel" }
        $diffOutput = @($diffOutput | Where-Object { $_ -notmatch '^warning: ' })
        if (@($diffOutput).Count -ge 4 -and $diffOutput[0] -match '^diff --git ') {
            $diffOutput[0] = 'diff --git a/' + $rel + ' b/' + $rel
            $diffOutput[2] = '--- a/' + $rel
            $diffOutput[3] = '+++ b/' + $rel
        }
        $diffOutput | Set-Content -LiteralPath $out
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $current).Hash.ToUpperInvariant()
        $manifest += [pscustomobject]@{ path=$rel; currentSha256=$hash; changed=(@($diffOutput | Where-Object { $_ -match '^diff --git ' }).Count -gt 0); diff=(Split-Path $out -Leaf) }
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Output 'manifest.json')
    Write-Output ([pscustomobject]@{ output=$Output; files=$manifest.Count; checkpointReconstructed=$true } | ConvertTo-Json -Depth 4)
}
finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
