$ErrorActionPreference='Stop'
$destination='C:/Users/PC/UnityStudy/MyGame/Assets/_Project/Scripts/UI'
foreach($name in @('LocalizedTextFit','LocalizedText','GoldUiTheme','UiKit','UiScreenLayout')){
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$name.cs.txt") -Destination (Join-Path $destination "$name.cs") -Force
}
Write-Output 'Installed translation overflow fitting in shared UI.'
