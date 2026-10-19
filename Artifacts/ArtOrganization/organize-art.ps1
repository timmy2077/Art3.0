$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$artRoot = Join-Path $projectRoot 'Assets/Project/Art'
$manifestPath = Join-Path $PSScriptRoot 'manifest.json'
if (Test-Path -LiteralPath $manifestPath) { throw 'An organization manifest already exists. Do not run twice.' }
$utf8 = New-Object Text.UTF8Encoding($false)
$baseline = @(Get-ChildItem -LiteralPath $artRoot -Recurse -File | ForEach-Object {
    [PSCustomObject]@{ Original = $_.FullName.Substring($projectRoot.Length + 1).Replace('\','/'); Current = $_.FullName.Substring($projectRoot.Length + 1).Replace('\','/'); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$protected = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets') -Recurse -File | Where-Object {
    !$_.FullName.StartsWith($artRoot + '\') -and $_.Extension -in @('.unity','.prefab','.asset','.anim','.controller','.overrideController','.spriteatlas','.cs')
} | ForEach-Object {
    [PSCustomObject]@{ Path = $_.FullName.Substring($projectRoot.Length + 1).Replace('\','/'); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$operations = New-Object Collections.Generic.List[object]
$createdFolders = New-Object Collections.Generic.List[string]
function Save-Manifest {
    $data = [ordered]@{ Files=$baseline; Protected=$protected; Operations=@($operations.ToArray()); CreatedFolders=@($createdFolders.ToArray()) }
    [IO.File]::WriteAllText($manifestPath, ($data | ConvertTo-Json -Depth 6), $utf8)
}
function Ensure-ArtFolder([string]$relative) {
    $folder = Join-Path $artRoot $relative
    if (Test-Path -LiteralPath $folder) { return }
    $parent = Split-Path $relative -Parent
    if ($parent) { Ensure-ArtFolder $parent }
    [void][IO.Directory]::CreateDirectory($folder)
    $guid = [Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText(($folder + '.meta'), "fileFormatVersion: 2`nguid: $guid`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n", $utf8)
    $createdFolders.Add($folder.Substring($projectRoot.Length + 1).Replace('\','/'))
    Save-Manifest
}
function Move-ArtAsset([string]$from, [string]$to) {
    $source = [IO.Path]::GetFullPath((Join-Path $artRoot $from))
    $target = [IO.Path]::GetFullPath((Join-Path $artRoot $to))
    if (!$source.StartsWith($artRoot + '\') -or !$target.StartsWith($artRoot + '\')) { throw 'Move outside Art refused.' }
    if (!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath ($source + '.meta'))) { throw "Asset or metadata missing: $from" }
    if ((Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath ($target + '.meta'))) { throw "Destination exists: $to" }
    $parent = Split-Path $to -Parent
    if ($parent) { Ensure-ArtFolder $parent }
    $old = $source.Substring($projectRoot.Length + 1).Replace('\','/')
    $new = $target.Substring($projectRoot.Length + 1).Replace('\','/')
    $operations.Add([PSCustomObject]@{From=$old; To=$new})
    foreach ($file in $baseline) {
        if ($file.Current -eq $old -or $file.Current -eq ($old + '.meta') -or $file.Current.StartsWith($old + '/')) {
            $file.Current = $new + $file.Current.Substring($old.Length)
        }
    }
    Save-Manifest
    Move-Item -LiteralPath ($source + '.meta') -Destination ($target + '.meta')
    Move-Item -LiteralPath $source -Destination $target
}
Save-Manifest
Move-ArtAsset '画像砖' 'Bricks'
Move-ArtAsset 'Illustrations' 'Bricks/Illustrations'
Move-ArtAsset 'Sequences/Logo' 'Cinematics/Logo'
Move-ArtAsset 'TitleSequence' 'Cinematics/Title'
Move-ArtAsset 'Sequences' 'Bricks/Animations'
Move-ArtAsset 'Bricks/Animations/耕种图静帧.png' 'Bricks/Illustrations/耕种图静帧.png'
Move-ArtAsset 'Bricks/Animations/耕种图静帧 1.png' 'Bricks/Illustrations/耕种图静帧 1.png'
Move-ArtAsset 'Sprites/Object' 'Props'
Move-ArtAsset 'Sprites/Character' 'Sprites/Actions'
Move-ArtAsset 'Sprites' 'Characters'
$playerFiles = @(Get-ChildItem -LiteralPath (Join-Path $artRoot 'Characters/Player') -File | Where-Object Extension -ne '.meta')
foreach ($file in $playerFiles) { Move-ArtAsset ('Characters/Player/' + $file.Name) ('Characters/Player/Variant01/' + $file.Name) }
Move-ArtAsset 'Player2' 'Characters/Player/Variant02'
foreach ($name in @('玩家.png','玩家1.png','玩家4 (1).png','玩家带纹理.png','寻砖人.png')) { Move-ArtAsset ('Bricks/' + $name) ('Characters/Player/Portraits/' + $name) }
foreach ($name in @('baishen.png','dagan.png')) { Move-ArtAsset ('Bricks/' + $name) ('Characters/NPCs/' + $name) }
foreach ($number in 1..12) { Move-ArtAsset "Bricks/$number.png" "Characters/NPCs/Courier/Frames/$number.png" }
foreach ($name in @('10002.jpg','重制版地图.png','pictorial-brick-clean-seamless.png')) { Move-ArtAsset ('Bricks/' + $name) ('Environments/Backgrounds/' + $name) }
Move-ArtAsset 'Bricks/茅草屋.png' 'Environments/Buildings/茅草屋.png'
foreach ($name in @('纯色封面.png','开始界面最终版.png','开始游戏按钮.png')) { Move-ArtAsset ('Bricks/' + $name) ('UI/MainMenu/' + $name) }
Move-ArtAsset 'Bricks/下一步.png' 'UI/Common/下一步.png'
Move-ArtAsset 'Bricks/空砖块.png' 'Bricks/Textures/空砖块.png'
foreach ($name in @('扬场图.png','砖块动画11.png')) { Move-ArtAsset ('Bricks/' + $name) ('Bricks/Illustrations/' + $name) }
foreach ($name in @('白牛','黑牛')) { Move-ArtAsset ('Props/' + $name) ('Props/Animals/' + $name) }
Move-ArtAsset 'Props/耙子' 'Props/Tools/耙子'
Move-ArtAsset 'Props/Fence' 'Props/FencesAndPuzzlePieces'
$failures = @($baseline | Where-Object { !(Test-Path -LiteralPath (Join-Path $projectRoot $_.Current)) -or (Get-FileHash -LiteralPath (Join-Path $projectRoot $_.Current) -Algorithm SHA256).Hash -ne $_.SHA256 })
$protectedFailures = @($protected | Where-Object { (Get-FileHash -LiteralPath (Join-Path $projectRoot $_.Path) -Algorithm SHA256).Hash -ne $_.SHA256 })
if ($failures.Count -or $protectedFailures.Count) { throw "Verification failed: Art=$($failures.Count), protected=$($protectedFailures.Count)" }
$baseline | Export-Csv -LiteralPath (Join-Path $PSScriptRoot 'asset-paths.csv') -NoTypeInformation -Encoding utf8
Write-Output "Verified $($baseline.Count) original Art files and $($protected.Count) protected scene/prefab/script/config files. Moves: $($operations.Count)."
