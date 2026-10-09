param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$identityPath = Join-Path $PSScriptRoot 'source-identity.json'
$paths = [System.Collections.Generic.List[string]]::new()
foreach ($directory in @('Assets/Scripts', 'ProjectSettings', 'Packages', 'Assets/ScriptableObjects', 'Assets/Scenes')) {
    $absolute = Join-Path $projectRoot $directory
    if (Test-Path -LiteralPath $absolute) {
        foreach ($file in Get-ChildItem -LiteralPath $absolute -Recurse -File) {
            if ($file.Extension -in @('.cs', '.asset', '.json', '.unity')) { $paths.Add($file.FullName) }
        }
    }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Tests') -Recurse -File) {
    if ($file.Name -like 'CommanderPhase5B*.cs' -or $file.Name -like 'CommanderEconomyClarification*.cs') { $paths.Add($file.FullName) }
}
$records = @($paths | Sort-Object -Unique | ForEach-Object {
    [ordered]@{ path = $_.Substring($projectRoot.Length + 1).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$sourceText = ($records | ForEach-Object { $_.path + ':' + $_.sha256 }) -join "`n"
$sha = [System.Security.Cryptography.SHA256]::Create()
try { $digest = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($sourceText))).Replace('-','').ToLowerInvariant() }
finally { $sha.Dispose() }
if ($VerifyOnly) {
    $saved = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
    if ($saved.aggregateSha256 -ne $digest -or $saved.recordCount -ne $records.Count) { throw 'Phase 5B source identity changed.' }
    Write-Output "SOURCE_IDENTITY_VERIFIED records=$($records.Count) sha256=$digest"
} else {
    $currentHead = (& git -c safe.directory=D:/unity_projects/OpenEmpires -C $projectRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot establish current source baseline HEAD.' }
    $payload = [ordered]@{ baselineHead = $currentHead; originalImplementationBaselineHead = '35be7427503cdfe04c3a6ce5474fc50fabf247a6'; recordCount = $records.Count; aggregateSha256 = $digest;
        coverage = 'Runtime scripts, project/package configuration, ScriptableObjects/scenes, Phase5B and economy-clarification tests. Hash equality is source identity only, not gameplay certification.'; records = $records }
    [IO.File]::WriteAllText($identityPath, ($payload | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    Write-Output "SOURCE_IDENTITY_WRITTEN records=$($records.Count) sha256=$digest"
}
