$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$artRoot = Join-Path $projectRoot 'Assets/Project/Art'
$data = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
foreach ($file in $data.Files) {
    $path = Join-Path $projectRoot $file.Current
    if (!(Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.SHA256) {
        throw "Asset changed or missing; restore stopped before moving anything: $($file.Current)"
    }
}
function Remove-EmptyCreatedFolders {
    foreach ($relative in ($data.CreatedFolders | Sort-Object Length -Descending)) {
        $folder = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
        if (!$folder.StartsWith($artRoot + '\')) { throw 'Folder outside Art refused.' }
        if ((Test-Path -LiteralPath $folder -PathType Container) -and @(Get-ChildItem -LiteralPath $folder -Force).Count -eq 0) {
            [IO.Directory]::Delete($folder, $false)
            if (Test-Path -LiteralPath ($folder + '.meta')) { Remove-Item -LiteralPath ($folder + '.meta') }
        }
    }
}
for ($i = $data.Operations.Count - 1; $i -ge 0; $i--) {
    $operation = $data.Operations[$i]
    $source = [IO.Path]::GetFullPath((Join-Path $projectRoot $operation.To))
    $target = [IO.Path]::GetFullPath((Join-Path $projectRoot $operation.From))
    if (!$source.StartsWith($artRoot + '\') -or !$target.StartsWith($artRoot + '\')) { throw 'Move outside Art refused.' }
    if ((Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath ($target + '.meta'))) { throw "Destination exists: $target" }
    Move-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta')
    Move-Item -LiteralPath $source -Destination $target
    Remove-EmptyCreatedFolders
}
foreach ($file in $data.Files) {
    if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $file.Original) -Algorithm SHA256).Hash -ne $file.SHA256) { throw "Restored file mismatch: $($file.Original)" }
}
Write-Output 'Original Art folder structure restored. Refresh Unity and use the manifest for the old paths in documentation.'
