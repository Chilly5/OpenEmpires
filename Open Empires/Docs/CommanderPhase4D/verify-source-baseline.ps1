[CmdletBinding()]
param(
    [switch] $CheckOriginalSource
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = (Resolve-Path (Join-Path $scriptDir '../..')).Path
$baselinePath = Join-Path $scriptDir 'phase4d-source-baseline.json'
$frozenPath = Join-Path $scriptDir 'phase4d-protected-boundary-frozen.json'

function Get-RelativePath([string] $Path) {
    return [IO.Path]::GetFullPath((Join-Path $projectRoot $Path))
}

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Assert-Equal([string] $Name, [object] $Actual, [object] $Expected) {
    if ($Actual -ne $Expected) {
        throw "$Name mismatch: expected '$Expected', got '$Actual'."
    }
}

function Assert-FileHash([string] $Name, [string] $Path, [string] $ExpectedHash) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name missing: $Path"
    }
    $actualHash = Get-Sha256 $Path
    if ($actualHash -ne $ExpectedHash.ToUpperInvariant()) {
        throw "$Name SHA-256 mismatch: expected $ExpectedHash, got $actualHash."
    }
    return $actualHash
}

try {
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    $frozen = Get-Content -LiteralPath $frozenPath -Raw | ConvertFrom-Json
    Assert-Equal 'Baseline schema' $baseline.schema 'commander-phase4d-source-baseline@1'
    Assert-Equal 'Frozen boundary schema' $frozen.schema 'commander-phase4d-protected-boundary-frozen@1'

    $manifestRef = $baseline.phase4cSourceManifest
    $manifestPath = Get-RelativePath $manifestRef.path
    $manifestHash = Assert-FileHash 'Phase 4C source manifest' $manifestPath $manifestRef.sha256
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $manifestFiles = @($manifest.files)
    Assert-Equal 'Manifest declared count' $manifestRef.declaredFileCount $manifestFiles.Count
    Assert-Equal 'Manifest schema count' $manifestRef.declaredFileCount $manifestFiles.Count

    $sourceMissing = [Collections.Generic.List[string]]::new()
    $sourceChanged = [Collections.Generic.List[string]]::new()
    foreach ($entry in $manifestFiles) {
        $path = Get-RelativePath $entry.path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $sourceMissing.Add($entry.path)
            continue
        }
        if ($CheckOriginalSource -and (Get-Sha256 $path) -ne $entry.sha256.ToUpperInvariant()) {
            $sourceChanged.Add($entry.path)
        }
    }
    if ($sourceMissing.Count -gt 0) {
        throw "Phase 4C manifest paths missing ($($sourceMissing.Count)): $($sourceMissing -join ', ')"
    }

    $boundaryRef = $baseline.protectedBoundary
    $auditPath = Get-RelativePath $boundaryRef.path
    $auditHash = Assert-FileHash 'Phase 4C boundary audit' $auditPath $boundaryRef.sha256
    $audit = Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json
    Assert-Equal 'Boundary audit protected count' $boundaryRef.protectedFileCount @($audit.frozenBoundaryHashes).Count

    $frozenSourceAuditPath = Get-RelativePath $frozen.sourceAudit.path
    $frozenSourceAuditHash = Assert-FileHash 'Frozen source audit reference' $frozenSourceAuditPath $frozen.sourceAudit.sha256
    Assert-Equal 'Frozen/source audit hash agreement' $frozen.sourceAudit.sha256 $boundaryRef.sha256
    Assert-Equal 'Frozen entry count' $frozen.entryCount @($frozen.entries).Count
    Assert-Equal 'Baseline/frozen protected count' $boundaryRef.protectedFileCount $frozen.entryCount

    $auditEntries = @{}
    foreach ($entry in $audit.frozenBoundaryHashes) { $auditEntries[$entry.path] = $entry.baselineSha256.ToUpperInvariant() }
    $frozenMissing = [Collections.Generic.List[string]]::new()
    $frozenChanged = [Collections.Generic.List[string]]::new()
    $frozenUnchanged = 0
    foreach ($entry in $frozen.entries) {
        if (-not $auditEntries.ContainsKey($entry.path) -or $auditEntries[$entry.path] -ne $entry.sha256.ToUpperInvariant()) {
            throw "Frozen boundary reference mismatch for $($entry.path)."
        }
        $path = Get-RelativePath $entry.path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $frozenMissing.Add($entry.path)
            continue
        }
        $actual = Get-Sha256 $path
        if ($actual -eq $entry.sha256.ToUpperInvariant()) { $frozenUnchanged++ }
        else { $frozenChanged.Add($entry.path) }
    }
    if ($frozenMissing.Count -gt 0) {
        throw "Frozen protected files missing ($($frozenMissing.Count)): $($frozenMissing -join ', ')"
    }

    $artifactCount = 0
    foreach ($artifact in $baseline.regressionEvidence.artifacts) {
        $path = Get-RelativePath $artifact.path
        [void](Assert-FileHash "Pinned test artifact $($artifact.path)" $path $artifact.sha256)
        [xml] $xml = Get-Content -LiteralPath $path -Raw
        $run = $xml.SelectSingleNode('/test-run')
        if ($null -eq $run) { throw "Pinned test artifact has no /test-run element: $($artifact.path)" }
        foreach ($field in @('total', 'passed', 'failed', 'skipped')) {
            Assert-Equal "$($artifact.path) $field" ([int]$run.GetAttribute($field)) ([int]$artifact.$field)
        }
        if ($run.GetAttribute('result') -ne $artifact.result) { throw "Pinned test artifact result mismatch: $($artifact.path)" }
        if ($run.GetAttribute('inconclusive') -and [int]$run.GetAttribute('inconclusive') -ne 0) {
            throw "Pinned test artifact has inconclusive tests: $($artifact.path)"
        }
        if ($null -ne $artifact.PSObject.Properties['testCase']) {
            $found = $false
            foreach ($case in $xml.SelectNodes('//test-case')) {
                if ($case.GetAttribute('name') -eq $artifact.testCase) { $found = $true; break }
            }
            if (-not $found) { throw "Pinned named test case not found: $($artifact.testCase)" }
        }
        $artifactCount++
    }

    $summaryRef = $baseline.regressionEvidence.dynamicHousingSummary
    [void](Assert-FileHash 'Dynamic housing summary' (Get-RelativePath $summaryRef.path) $summaryRef.sha256)

    Write-Output "Baseline references: PASS; manifest SHA-256 $manifestHash; audit SHA-256 $auditHash; frozen audit SHA-256 $frozenSourceAuditHash"
    Write-Output "Phase 4C source paths present: $($manifestFiles.Count)/$($manifestRef.declaredFileCount)"
    Write-Output "Frozen protected paths present: $($frozen.entries.Count)/$($frozen.entryCount); current hashes unchanged $frozenUnchanged; changed $($frozenChanged.Count); missing $($frozenMissing.Count)"
    foreach ($path in $frozenChanged) { Write-Output "FROZEN_PATH_HASH_DRIFT (current working tree; inspect against Phase 4D changes): $path" }
    Write-Output "Pinned XML artifacts verified: $artifactCount/$($baseline.regressionEvidence.artifacts.Count); summary SHA-256 $($summaryRef.sha256)"
    if ($CheckOriginalSource) {
        Write-Output "Original source hash check: $($manifestFiles.Count - $sourceChanged.Count)/$($manifestFiles.Count) match; mismatches $($sourceChanged.Count)"
        foreach ($path in $sourceChanged) { Write-Output "ORIGINAL_SOURCE_HASH_DRIFT (diagnostic; may be an intentional Phase 4D edit): $path" }
        if ($sourceChanged.Count -gt 0) { exit 2 }
    } else {
        Write-Output 'Original source content hashes: not checked (use -CheckOriginalSource); ongoing Phase 4D edits are permitted.'
    }
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
