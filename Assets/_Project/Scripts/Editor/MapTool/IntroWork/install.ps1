$ErrorActionPreference='Stop'
$gameRoot='C:/Users/PC/UnityStudy/MyGame'
$workRoot=$PSScriptRoot
foreach($item in @(@{Name='TitleController';Path='Title/TitleController.cs'},@{Name='LocalizationTable';Path='Services/LocalizationTable.cs'})) {
    $target=Join-Path "$gameRoot/Assets/_Project/Scripts" $item.Path
    $current=(Get-Content -LiteralPath $target -Raw).Replace("`r`n","`n")
    $before=(Get-Content -LiteralPath "$workRoot/Backup/$($item.Name).before.txt" -Raw).Replace("`r`n","`n")
    $staged=(Get-Content -LiteralPath "$workRoot/Staged/$($item.Name).cs.txt" -Raw).Replace("`r`n","`n")
    if($current -ne $before -and $current -ne $staged){throw "Source changed after review: $target"}
}
$mediaRoot=Join-Path $gameRoot 'Assets/StreamingAssets/Video'
New-Item -ItemType Directory -Path $mediaRoot -Force | Out-Null
Copy-Item -LiteralPath 'C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/intro-film/v2/LeafAndLeapIntro.mp4' -Destination (Join-Path $mediaRoot 'LeafAndLeapIntro.mp4')
foreach($name in @('IntroStory','IntroView','IntroSequencePlayer','TitleController')) {
    Copy-Item -LiteralPath "$workRoot/Staged/$name.cs.txt" -Destination "$gameRoot/Assets/_Project/Scripts/Title/$name.cs"
}
Copy-Item -LiteralPath "$workRoot/Staged/LocalizationTable.cs.txt" -Destination "$gameRoot/Assets/_Project/Scripts/Services/LocalizationTable.cs"
Write-Output 'Installed opening film, title flow, runtime subtitles and five language translations.'
