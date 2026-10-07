param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifestPath = Join-Path $PSScriptRoot 'phase5a-source-manifest.json'
function FileRecord([string]$relativePath) {
    $absolutePath = Join-Path $taskRoot $relativePath
    $item = Get-Item -LiteralPath $absolutePath
    [ordered]@{ path = $relativePath.Replace('\','/'); bytes = $item.Length;
        sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant() }
}
if ($VerifyOnly) {
    $record = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $mismatches = @()
    foreach ($entry in @($record.source_files) + @($record.changed_files) + @($record.protected_boundaries) + @($record.test_artifacts)) {
        $absolutePath = Join-Path $taskRoot $entry.path
        if (!(Test-Path -LiteralPath $absolutePath) -or (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
            $mismatches += $entry.path
        }
    }
    if ($mismatches.Count -gt 0) { throw ('Manifest mismatches: ' + ($mismatches -join ', ')) }
    if ((git -C $taskRoot rev-parse HEAD).Trim() -ne $record.head) { throw 'HEAD changed after snapshot.' }
    Write-Output ('Manifest consistent: sources=' + @($record.source_files).Count + '; changed=' + @($record.changed_files).Count + '; artifacts=' + @($record.test_artifacts).Count)
    exit 0
}
$trackedChanges = @(git -C $taskRoot -c core.quotepath=false diff --name-only HEAD -- .)
$untrackedChanges = @(git -C $taskRoot -c core.quotepath=false ls-files --others --exclude-standard -- .)
# Git diff returns repository-relative names, while ls-files here returns cwd-relative names.
$prefix = (git -C $taskRoot rev-parse --show-prefix).Trim()
$changedPaths = @($trackedChanges | ForEach-Object { if ($_.StartsWith($prefix)) { $_.Substring($prefix.Length) } else { $_ } }) + $untrackedChanges
$changedPaths = @($changedPaths | Where-Object {
    $_ -and $_ -ne 'Docs/CommanderPhase5A/phase5a-source-manifest.json' -and
    ($_ -like 'Assets/*' -or $_ -like 'Docs/*' -or $_ -eq 'remaining_work.md') -and
    (Test-Path -LiteralPath (Join-Path $taskRoot $_) -PathType Leaf)
} | Sort-Object -Unique)
$sources = @($changedPaths | Where-Object { $_ -like 'Assets/*.cs' } | ForEach-Object { FileRecord $_ })
$changed = @($changedPaths | ForEach-Object { FileRecord $_ })
$protectedReasons = [ordered]@{
    'Assets/Scripts/Network/CommandSerializer.cs' = 'Unchanged in Phase5A; pre-existing restricted economy versioned packets still require compatible peers.'
    'Assets/Scripts/Commands/CommandBuffer.cs' = 'Unchanged ordinary enqueue/dispatch authority; local observer source tags reused.'
    'Assets/Scripts/Commands/GatherCommand.cs' = 'Unchanged existing source restriction encoding and ordinary execution.'
    'Assets/Scripts/Commands/SlaughterSheepCommand.cs' = 'Unchanged existing source restriction encoding and ordinary execution.'
    'Assets/Scripts/Resources/ResourceSourceKind.cs' = 'Unchanged native source enum; new semantic constraint is not a network enum change.'
    'Assets/Scripts/Resources/ResourceSourceRules.cs' = 'Unchanged authoritative source compatibility/classification.'
    'Assets/Scripts/Core/GameSimulation.cs' = 'Changed local training/placement acceptance and original-command observation; no new wire, balance or timing policy.'
    'Assets/Scripts/Core/GameBootstrapper.cs' = 'Changed bounded local-origin correlation for relay/replay and shutdown cleanup; no network payload redesign.'
    'Assets/Scripts/Core/GameSimulation.TrainingObservation.cs' = 'New local receipts/spawn/origin-loss observer; issuer-scoped cleanup and fail-closed attribution.'
    'Assets/Scripts/Core/CommanderCommandOriginLedger.cs' = 'New bounded local owner-batch origin ledger; ambiguous attribution cannot suppress normal commands.'
    'Assets/Scripts/Buildings/BuildingData.cs' = 'Changed queue storage wrapper preserves integer queue/timer behavior and invalidates observational receipts on mutation.'
    'Assets/Scripts/Buildings/BuildingTrainingSystem.cs' = 'Changed accepted receipt completion/cancellation observation; native training duration/queue behavior retained.'
    'Assets/Scripts/Buildings/TrainingQueueCollection.cs' = 'New integer IList queue adapter with local receipt sidecars, no serialized provenance.'
    'Assets/Scripts/Buildings/TrainingOrderReceipt.cs' = 'New immutable native training-order identity/status observation.'
}
$protected = @($protectedReasons.Keys | ForEach-Object {
    $entry = FileRecord $_
    $entry['changed_in_phase5a'] = $changedPaths -contains $_
    $entry['explanation'] = $protectedReasons[$_]
    $entry
})
$artifacts = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.xml' -File | Sort-Object Name | ForEach-Object {
    $entry = FileRecord ('Docs/CommanderPhase5A/' + $_.Name)
    try {
        [xml]$xml = Get-Content -LiteralPath $_.FullName -Raw
        $run = $xml.DocumentElement
        $entry['result'] = $run.GetAttribute('result')
        $entry['total'] = $run.GetAttribute('total')
        $entry['passed'] = $run.GetAttribute('passed')
        $entry['failed'] = $run.GetAttribute('failed')
        $entry['skipped'] = $run.GetAttribute('skipped')
        $entry['start_time'] = $run.GetAttribute('start-time')
        $entry['end_time'] = $run.GetAttribute('end-time')
    } catch { $entry['summary_parse_error'] = $_.Exception.GetType().Name }
    $entry
})
$record = [ordered]@{
    schema = 'openempires-phase5a-source-manifest@1'
    generated_utc = [DateTime]::UtcNow.ToString('O')
    workspace = $taskRoot
    git_root = (git -C $taskRoot rev-parse --show-toplevel).Trim()
    branch = (git -C $taskRoot branch --show-current).Trim()
    head = (git -C $taskRoot rev-parse HEAD).Trim()
    dirty_worktree = $true
    unity_version = '6000.5.9f1'
    unity_instance = 'Open Empires@6d7310c7'
    verification_scope = 'Phase 5A source/hash snapshot plus 2026-10-07 post-acceptance verification: full EditMode 1124/1124, four reconciled PlayMode groups 39/39, and a full PlayMode attempt with one live Luna construction failure (201 completed). The isolated Mill scenario then passed twice with the same valid Request and completed native lifecycle; the original failed-run semantic output remains unavailable, so that failure is unclassified. Existing hostile/authority and standalone evidence remain historical. See antigravity-final-audit.md and reconciliation-playmode-evidence-2026-10-07.json; this hash snapshot is not complete release certification.'
    source_files = $sources
    changed_files = $changed
    protected_boundaries = $protected
    test_artifacts = $artifacts
    exclusions = @('Credentials/environment contents are never read or recorded.', 'Manifest excludes its own hash to avoid recursion.', 'Initial historical investigation and accepted phase records remain historical.', 'Unrelated ignored/generated workspace files are not included.', 'The hash snapshot identifies bytes; it is not full regression certification.')
}
# Mechanically generated hash/summary artifact, not a manual source edit.
[IO.File]::WriteAllText($manifestPath, ($record | ConvertTo-Json -Depth 9) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output ('Generated manifest: sources=' + $sources.Count + '; changed=' + $changed.Count + '; artifacts=' + $artifacts.Count)
