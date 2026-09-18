[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$Output = (Join-Path $PSScriptRoot 'phase4c1-fixround2-review')
)

$ErrorActionPreference = 'Stop'
$beforeRoot = Join-Path $PSScriptRoot 'phase4c1-fixround2-before'
$files = @(
    @{ path='Assets/Scripts/AI/Commander/Phase4B2/StrategicAIApprovalBridge.cs'; before='StrategicAIApprovalBridge.cs.before.txt' },
    @{ path='Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs'; before='CommanderChatUI.cs.before.txt' },
    @{ path='Assets/Tests/PlayMode/CommanderPhase4C1PlayModeTests.cs'; before='CommanderPhase4C1PlayModeTests.cs.before.txt' }
)
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$manifest = @()
foreach ($item in $files) {
    $current = Join-Path $Root ($item.path -replace '/', '\\')
    $before = Join-Path $beforeRoot $item.before
    $safe = $item.path -replace '/', '_'
    $out = Join-Path $Output ($safe + '.fixround2.diff.txt')
    $diff = @(& git diff --no-index --binary --unified=10 -- $before $current 2>$null)
    if ($LASTEXITCODE -notin @(0,1)) { throw "Could not diff $($item.path)" }
    $diff = @($diff | Where-Object { $_ -notmatch '^warning: ' })
    if (@($diff).Count -ge 4 -and $diff[0] -match '^diff --git ') {
        $diff[0] = 'diff --git a/' + $item.path + ' b/' + $item.path
        $diff[2] = '--- a/' + $item.path
        $diff[3] = '+++ b/' + $item.path
    }
    $diff | Set-Content -LiteralPath $out
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $current).Hash.ToUpperInvariant()
    $manifest += [pscustomobject]@{ path=$item.path; currentSha256=$hash; changed=(@($diff | Where-Object { $_ -match '^diff --git ' }).Count -gt 0); diff=(Split-Path $out -Leaf) }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Output 'manifest.json')
$manifest | Format-Table path,changed,currentSha256 -AutoSize
