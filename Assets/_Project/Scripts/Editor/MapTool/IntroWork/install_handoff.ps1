$ErrorActionPreference='Stop'
$targetRoot='C:/Users/PC/UnityStudy/MyGame/Assets/_Project/Scripts/Title'
$verifiedRoot='C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/IntroFlowProject/Assets/_Project/Scripts/Title'
$names=@('IntroSequencePlayer','TitlePresentation','TitleIllustration','TitleController','IntroTitleTransition')
foreach($name in $names){
 $staged="$PSScriptRoot/Handoff/$name.cs.txt"
 if((Get-FileHash -LiteralPath $staged).Hash -ne (Get-FileHash -LiteralPath "$verifiedRoot/$name.cs").Hash){throw "Different from test source: $name"}
 if($name -eq 'IntroTitleTransition'){
  if((Test-Path -LiteralPath "$targetRoot/$name.cs") -and (Get-FileHash -LiteralPath "$targetRoot/$name.cs").Hash -ne (Get-FileHash -LiteralPath $staged).Hash){throw 'New transition file already exists with different contents'}
 } else {
  $current=(Get-Content -LiteralPath "$targetRoot/$name.cs" -Raw).Replace("`r`n","`n")
  $before=(Get-Content -LiteralPath "$PSScriptRoot/Handoff/$name.before.txt" -Raw).Replace("`r`n","`n")
  $after=(Get-Content -LiteralPath $staged -Raw).Replace("`r`n","`n")
  if($current -ne $before -and $current -ne $after){throw "Original changed since preparation: $name"}
 }
}
foreach($name in $names){
 Copy-Item -LiteralPath "$PSScriptRoot/Handoff/$name.cs.txt" -Destination "$targetRoot/$name.cs"
 if((Get-FileHash -LiteralPath "$PSScriptRoot/Handoff/$name.cs.txt").Hash -ne (Get-FileHash -LiteralPath "$targetRoot/$name.cs").Hash){throw "Install mismatch: $name"}
 Write-Output "Installed and verified: $name"
}
$film='C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/intro-film/v2/LeafAndLeapIntro_LogoHandoff.mp4'
$destination='C:/Users/PC/UnityStudy/MyGame/Assets/StreamingAssets/Video/LeafAndLeapIntro.mp4'
Copy-Item -LiteralPath $film -Destination $destination
if((Get-FileHash -LiteralPath $film).Hash -ne (Get-FileHash -LiteralPath $destination).Hash){throw 'Movie hash mismatch'}
Write-Output 'Installed logo-free ending video for the continuous UI logo.'
