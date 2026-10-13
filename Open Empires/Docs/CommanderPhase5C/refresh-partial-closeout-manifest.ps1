param([switch]$VerifyOnly, [switch]$SourceOnly)
$ErrorActionPreference = 'Stop'
$taskRepository = (& git rev-parse --show-toplevel).Trim()
$taskOutput = 'Open Empires/Docs/CommanderPhase5C/evidence/partial-closeout-source-manifest.json'
$taskGenerator = Join-Path $PSScriptRoot '../CommanderFinalization/GrandFix/refresh-grand-fix-manifest.ps1'
if ($VerifyOnly) {
    & $taskGenerator -RepositoryRoot $taskRepository -OutputPath $taskOutput -VerifyOnly
    exit $LASTEXITCODE
}
$taskArtifacts = @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Recurse |
    Where-Object { $_.Name -notmatch 'manifest.*\.json$|\.tmp$' } |
    ForEach-Object { $_.FullName.Substring($taskRepository.Length + 1).Replace('\','/') })
if (-not $SourceOnly) {
    foreach ($taskAssembly in 'Runtime','Editor','EditModeTests','PlayModeTests','TestSupport') {
        $taskArtifacts += "Open Empires/Library/ScriptAssemblies/OpenEmpires.$taskAssembly.dll"
    }
}
& $taskGenerator -RepositoryRoot $taskRepository -OutputPath $taskOutput -SnapshotKind final -AdditionalArtifactPaths $taskArtifacts
if ($LASTEXITCODE -ne 0) { throw 'Manifest generation failed.' }
$taskAbsoluteOutput = Join-Path $taskRepository $taskOutput
$taskSnapshot = Get-Content -LiteralPath $taskAbsoluteOutput -Raw | ConvertFrom-Json
$taskSnapshot.paid_usage_source = 'Open Empires/Docs/CommanderPhase5C/evidence/paid-attempt-ledger.json'
$taskSnapshot.scope += if ($SourceOnly) { ' Phase5C targeted partial handoff: source and documentation only; changing Editor assemblies excluded. No fresh compilation or Windows/Web package claim.' } else { ' Phase5C partial-closeout documentation/evidence and current Editor assemblies explicitly included. No fresh Windows/Web package claim.' }
[IO.File]::WriteAllText($taskAbsoluteOutput, ($taskSnapshot | ConvertTo-Json -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
Write-Output 'Partial handoff manifest generated; full release acceptance remains deferred.'
