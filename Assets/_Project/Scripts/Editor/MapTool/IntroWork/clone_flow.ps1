$ErrorActionPreference='Stop'
$source='C:/Users/PC/UnityStudy/MyGame'
$destination='C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/IntroFlowProject'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach($folder in @('Assets','ProjectSettings','Packages')) {
    & robocopy "$source/$folder" "$destination/$folder" /E /MT:8 /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if($LASTEXITCODE -ge 8){throw "Copy failed: $folder"}
}
foreach($folder in @('Artifacts','ScriptAssemblies','PackageCache')) {
    & robocopy "$source/Library/$folder" "$destination/Library/$folder" /E /MT:8 /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if($LASTEXITCODE -ge 8){throw "Cache copy failed: $folder"}
}
foreach($file in @('ArtifactDB','SourceAssetDB','ScriptMapper')) {
    Copy-Item -LiteralPath "$source/Library/$file" -Destination "$destination/Library/$file"
}
Write-Output $destination
