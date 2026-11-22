$ErrorActionPreference='Stop'
$work=Split-Path -Parent $MyInvocation.MyCommand.Path
$project='C:\Users\PC\UnityStudy\MyGame'
$destination=Join-Path $project 'Assets\_Project\Scripts\InGame\FX\NowCutIn.cs'
$art=Join-Path $project 'Assets\_Project\Resources\CutInArt'
if(!(Test-Path -LiteralPath (Join-Path $work 'NowCutIn.before.txt'))){Copy-Item -LiteralPath $destination -Destination (Join-Path $work 'NowCutIn.before.txt')}
New-Item -ItemType Directory -Force -Path $art | Out-Null
foreach($name in @('CounterPortrait','NowKo')){
 Copy-Item -LiteralPath (Join-Path $work "Art\$name.png") -Destination (Join-Path $art "$name.png") -Force
 if(!(Test-Path -LiteralPath (Join-Path $art "$name.png.meta"))){Copy-Item -LiteralPath (Join-Path $work "Art\$name.png.meta") -Destination (Join-Path $art "$name.png.meta")}
}
Copy-Item -LiteralPath (Join-Path $work 'NowCutIn.cs.txt') -Destination $destination -Force
Write-Output 'Installed illustrated cut-in code and two transparent assets; original code backed up.'
