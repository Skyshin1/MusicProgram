param([switch]$Editor)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$unityData = 'C:/Program Files/Unity 6000.3.7f1/Editor/Data'
$outputDir = Join-Path $projectRoot 'Logs/DeepSeaDemo'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$references = @()
$references += Get-ChildItem -LiteralPath "$unityData/NetStandard/ref/2.1.0" -Filter '*.dll'
$references += Get-ChildItem -LiteralPath "$unityData/Managed/UnityEngine" -Filter '*.dll'
$references += Get-ChildItem -LiteralPath "$projectRoot/Library/ScriptAssemblies" -Filter '*.dll'
if ($Editor) { $references += Get-ChildItem -LiteralPath "$unityData/Managed" -Filter 'UnityEditor*.dll' }
$sources = @(Get-ChildItem -LiteralPath "$projectRoot/Assets/DeepSeaDemo/Runtime" -Filter '*.cs' | ForEach-Object FullName)
foreach ($relative in @(
 'Assets/SonicWorld/Runtime/VolumetricFogPulseEmitter.cs',
 'Assets/SonicWorld/Runtime/VolumetricFogCollisionPulse.cs',
 'Assets/SonicWorld/Runtime/SonarRevealManager.cs',
 'Assets/SonicWorld/Runtime/SonarWhiteOutlineRendererFeature.cs',
 'Assets/SonicWorld/Runtime/QuestLeftStickLocomotion.cs',
 'Assets/SonicWorld/Runtime/XRHandSonarInput.cs',
 'Assets/SonicWorld/Runtime/SurfaceDocumentReader.cs',
 'Assets/SonicWorld/Runtime/GrabFlashlight.cs',
 'Assets/DeepSeaAI/Runtime/RepairTool.cs',
 'Assets/DeepSeaAI/Runtime/RepairableFacility.cs',
 'Assets/DeepSeaAI/Runtime/SonarRevealStyle.cs',
 'Assets/DeepSeaAI/Runtime/RepairSkillCheckController.cs',
 'Assets/DeepSeaAI/Runtime/DeepSeaStalkerController.cs',
 'Assets/DeepSeaAI/Runtime/DeepSeaStalkerConfig.cs',
 'Assets/DeepSeaAI/Runtime/PlayerRespawnController.cs',
 'Assets/DeepSeaAI/Runtime/DeepSeaFishAI.cs',
 'Assets/DeepSeaAI/Runtime/NoiseSystem.cs')) { $sources += Join-Path $projectRoot $relative }
if ($Editor -and (Test-Path "$projectRoot/Assets/DeepSeaDemo/Editor")) {
 $sources += Get-ChildItem -LiteralPath "$projectRoot/Assets/DeepSeaDemo/Editor" -Filter '*.cs' | ForEach-Object FullName
}
$compileArgs = @('/nologo','/target:library','/langversion:latest','/nostdlib+','/nowarn:0436,0618',"/out:$outputDir/DeepSeaDemo.CompileCheck.dll")
if ($Editor) { $compileArgs += '/define:UNITY_EDITOR;UNITY_STANDALONE_WIN;ENABLE_INPUT_SYSTEM;UNITY_6000_0_OR_NEWER' }
else { $compileArgs += '/define:UNITY_STANDALONE_WIN;ENABLE_INPUT_SYSTEM;UNITY_6000_0_OR_NEWER' }
$compileArgs += $references | Sort-Object FullName -Unique | ForEach-Object { '/reference:' + $_.FullName }
$compileArgs += $sources
$responsePath = Join-Path $outputDir 'compile.rsp'
[System.IO.File]::WriteAllLines($responsePath, ($compileArgs | ForEach-Object { '"' + $_ + '"' }))
& "$unityData/NetCoreRuntime/dotnet.exe" "$unityData/DotNetSdkRoslyn/csc.dll" "@$responsePath"
exit $LASTEXITCODE
