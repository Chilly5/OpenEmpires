param(
    [string]$RepositoryRoot,
    [string]$UnityRelativePath = 'Open Empires',
    [string]$OutputPath = 'Open Empires/Docs/CommanderFinalization/GrandFix/grand-fix-source-manifest.json',
    [ValidateSet('baseline','working','final')][string]$SnapshotKind = 'working',
    [string[]]$AdditionalArtifactPaths = @(),
    [switch]$VerifyOnly
)
$ErrorActionPreference = 'Stop'
if (-not $RepositoryRoot) { $RepositoryRoot = (& git rev-parse --show-toplevel).Trim() }
$repoPath = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\','/')
$repoPrefix = $repoPath + [IO.Path]::DirectorySeparatorChar
$unityPrefix = $UnityRelativePath.Replace('\','/').Trim('/') + '/'

function Resolve-SafePath([string]$relative) {
    $resolved = [IO.Path]::GetFullPath((Join-Path $repoPath $relative))
    if (-not $resolved.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A manifest path escaped the repository.'
    }
    return $resolved
}
function Is-SecretPath([string]$relative) {
    # This exact checked-in template has empty credential slots and no values.
    # All real .env files and private runtime ledger/lock stores stay excluded.
    if ($relative -eq 'backend/speech-gateway/.env.example') { return $false }
    if ($relative -match '(^|/)private-state(/|$)') { return $true }
    return $relative -match '(^|/)(\.env($|\.)|secrets?($|[./_-])|credentials?($|[./_-])|license\.ulf$|.*\.(pfx|p12|pem|key)$)'
}
$outputAbsolute = Resolve-SafePath $OutputPath
$outputRelative = $outputAbsolute.Substring($repoPrefix.Length).Replace('\','/')
if (Is-SecretPath $outputRelative) { throw 'Manifest output cannot be a secret store.' }

function Git-NulItems([string[]]$arguments) {
    $raw = [string]::Join("`n", @(& git -C $repoPath @arguments))
    if ($LASTEXITCODE -ne 0) { throw 'Git inventory failed.' }
    return @($raw.Split([char]0) | Where-Object { $_.Length -gt 0 })
}
function Is-Relevant([string]$relative) {
    if ($relative -eq $outputRelative -or (Is-SecretPath $relative)) { return $false }
    if ($relative -eq 'backend/speech-gateway/.env.example') { return $true }
    # Never hash generated manifests recursively or pretend build hashes are runtime proof.
    if ($relative -match '/GrandFix/grand-fix-(source|baseline|final)-manifest\.json$') { return $false }
    if ($relative.StartsWith($unityPrefix + 'Assets/', [StringComparison]::Ordinal)) {
        return $relative -match '\.(cs|asmdef|asmref|inputactions|jslib|js|mjs|ts|html|css|uss|uxml|shader|hlsl|cginc|compute|asset|prefab|unity|meta|json|txt|md)$'
    }
    if ($relative.StartsWith($unityPrefix + 'Packages/') -or $relative.StartsWith($unityPrefix + 'ProjectSettings/')) {
        return $relative -match '\.(json|txt|asset|yaml|yml)$'
    }
    if ($relative.StartsWith('backend/') -and -not ($relative -match '^backend/(target|data)/')) {
        return $relative -match '\.(rs|toml|lock|js|mjs|ts|html|css|json|yaml|yml|md|sh|ps1)$'
    }
    if ($relative.StartsWith('.github/workflows/')) { return $relative -match '\.(yaml|yml)$' }
    if ($relative -match '(^|/)(README[^/]*|THIRD_PARTY_NOTICES[^/]*|remaining_work\.md|\.gitignore)$') { return $true }
    return $relative.StartsWith($unityPrefix + 'Docs/CommanderFinalization/GrandFix/')
}
function Current-Inventory {
    $paths = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($path in (Git-NulItems @('ls-files','--cached','--others','--exclude-standard','-z'))) {
        if (Is-Relevant $path) { [void]$paths.Add($path) }
    }
    foreach ($artifact in $AdditionalArtifactPaths) {
        $absolute = Resolve-SafePath $artifact
        $relative = $absolute.Substring($repoPrefix.Length).Replace('\','/')
        if ((Is-SecretPath $relative) -or $relative -eq $outputRelative) { throw 'Unsafe additional artifact path.' }
        [void]$paths.Add($relative)
    }
    # Explicit known baseline model/package artifacts; these are not fresh build certificates.
    foreach ($relative in @(($unityPrefix + 'Assets/StreamingAssets/Whisper/ggml-tiny.bin'),
        ($unityPrefix + 'Builds/Windows/OpenEmpires.exe'),
        ($unityPrefix + 'Builds/Windows/OpenEmpires_Data/Managed/OpenEmpires.Runtime.dll'))) {
        if (Test-Path -LiteralPath (Resolve-SafePath $relative) -PathType Leaf) { [void]$paths.Add($relative) }
    }
    return @($paths | Sort-Object -CaseSensitive)
}
function File-Record([string]$relative) {
    $absolute = Resolve-SafePath $relative
    $exists = Test-Path -LiteralPath $absolute -PathType Leaf
    $length = $null; $hash = $null
    if ($exists) {
        $length = (Get-Item -LiteralPath $absolute).Length
        # Stream hashes also work when Windows PowerShell utility modules are unavailable.
        $algorithm = [Security.Cryptography.SHA256]::Create()
        $stream = [IO.File]::OpenRead($absolute)
        try { $hash = [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
        finally { $stream.Dispose(); $algorithm.Dispose() }
    }
    return [ordered]@{ path=$relative; exists=$exists; bytes=$length; sha256=$hash }
}
$head = (& git -C $repoPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Git HEAD unavailable.' }
$branch = (& git -C $repoPath branch --show-current).Trim()
$inventory = @(Current-Inventory)

if ($VerifyOnly) {
    $snapshot = Get-Content -LiteralPath $outputAbsolute -Raw | ConvertFrom-Json
    if ($snapshot.schema -ne 'openempires-grand-fix-source@1' -or $snapshot.head -ne $head -or
        $snapshot.branch -ne $branch) { throw 'Manifest identity changed.' }
    # Explicit additional artifacts survive verification without repeating the command arguments.
    $additional = @($snapshot.additional_artifacts | ForEach-Object { [string]$_ })
    $AdditionalArtifactPaths = $additional
    $inventory = @(Current-Inventory)
    $recordPaths = @($snapshot.records | ForEach-Object { [string]$_.path })
    if (@(Compare-Object $inventory $recordPaths).Count -ne 0) { throw 'Relevant source inventory changed.' }
    foreach ($record in $snapshot.records) {
        if (Is-SecretPath $record.path) { throw 'Secret path in snapshot rejected.' }
        $actual = File-Record $record.path
        if ($actual.exists -ne $record.exists -or $actual.bytes -ne $record.bytes -or $actual.sha256 -ne $record.sha256) {
            throw ('Manifest byte mismatch: ' + $record.path)
        }
    }
    Write-Output ('Manifest byte-consistent: records=' + $recordPaths.Count + '; HEAD=' + $head + '; NOT test/release proof')
    exit 0
}

$changes = @()
foreach ($entry in (Git-NulItems @('status','--porcelain=v1','-z','--untracked-files=all','--no-renames'))) {
    if ($entry.Length -lt 4) { throw 'Invalid Git status record.' }
    $relative = $entry.Substring(3)
    if (Is-Relevant $relative) {
        $changes += [ordered]@{ path=$relative; index=$entry.Substring(0,1); worktree=$entry.Substring(1,1) }
    }
}
$records = @($inventory | ForEach-Object { File-Record $_ })
$manifest = [ordered]@{
    schema='openempires-grand-fix-source@1'; snapshot_kind=$SnapshotKind
    generated_utc=[DateTime]::UtcNow.ToString('o'); repository=$repoPath; unity_relative_path=$UnityRelativePath
    branch=$branch; head=$head; records=$records; changes=@($changes)
    additional_artifacts=@($AdditionalArtifactPaths)
    scope='Relevant committed+working source/configuration/UI/input/meta/Web/backend/GrandFix evidence. Explicit model and existing packaged artifacts have byte identity only, not fresh runtime proof.'
    historical_provenance='Historical Phase5A and audit manifests are preserved. This snapshot does not re-certify their tests or replace their baseline HEAD.'
    exclusions='Secret stores, environment values, process command lines, recursive manifest self-hashes, Library/Temp/Logs, backend target/data; physical/runtime/peer claims require separate artifacts.'
    paid_usage_source=($unityPrefix + 'Docs/CommanderFinalization/GrandFix/paid-usage-ledger.json')
    paid_usage_policy='Run-persistent ledger is separately hashed when present; this generator never invents or resets transaction counts.'
    certification='BYTE INVENTORY ONLY - implementation/acceptance verdict belongs to the handoff report, not this manifest'
}
$directory = [IO.Path]::GetDirectoryName($outputAbsolute)
[void][IO.Directory]::CreateDirectory($directory)
$serialized = $manifest | ConvertTo-Json -Depth 12
[IO.File]::WriteAllText($outputAbsolute, $serialized + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Output ('Manifest captured: records=' + $records.Count + '; changes=' + $changes.Count + '; HEAD=' + $head + '; NOT test/release proof')
