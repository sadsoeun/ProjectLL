$unityExe='C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe'
$preview='C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/CutInPreview'
$log=Join-Path $PSScriptRoot 'preview-verification.log'
$process=Start-Process -FilePath $unityExe -ArgumentList @('-batchmode','-quit','-projectPath',$preview,'-executeMethod','MyGame.EditorTools.NowCutInVerification.Run','-logFile',$log) -WindowStyle Hidden -PassThru
Write-Output "Isolated preview process: $($process.Id)"
