$ErrorActionPreference='Stop'
$scripts='C:/Users/PC/UnityStudy/MyGame/Assets/_Project/Scripts'
$changes=@('Title/TitleController','Logo/LogoSequencer','LanguageSelect/LanguageSelectController','Title/IntroSequencePlayer','Title/IntroView','Title/TitlePresentation','Title/TitleIllustration')
foreach($path in $changes){
 $name=Split-Path $path -Leaf
 $current=(Get-Content -LiteralPath "$scripts/$path.cs" -Raw).Replace("`r`n","`n")
 $before=(Get-Content -LiteralPath "$PSScriptRoot/Startup/$name.cs.before.txt" -Raw).Replace("`r`n","`n")
 $after=(Get-Content -LiteralPath "$PSScriptRoot/Startup/$name.cs.txt" -Raw).Replace("`r`n","`n")
 if($current -ne $before -and $current -ne $after){throw "File changed since preparation: $path"}
}
foreach($path in $changes){
 $name=Split-Path $path -Leaf
 Copy-Item -LiteralPath "$PSScriptRoot/Startup/$name.cs.txt" -Destination "$scripts/$path.cs"
 if((Get-FileHash -LiteralPath "$PSScriptRoot/Startup/$name.cs.txt").Hash -ne (Get-FileHash -LiteralPath "$scripts/$path.cs").Hash){throw "Verification failed: $path"}
 Write-Output "Installed and verified: $path"
}
$introMedia='C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/intro-film/v2/LeafAndLeapIntro_Compatible.mp4'
$installedMedia='C:/Users/PC/UnityStudy/MyGame/Assets/StreamingAssets/Video/LeafAndLeapIntro.mp4'
Copy-Item -LiteralPath $introMedia -Destination $installedMedia
if((Get-FileHash -LiteralPath $introMedia).Hash -ne (Get-FileHash -LiteralPath $installedMedia).Hash){throw 'Video verification failed'}
Write-Output 'Installed compatible MP4, preserving original audio.'
