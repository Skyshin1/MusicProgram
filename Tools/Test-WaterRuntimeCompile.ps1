$ErrorActionPreference = 'Stop'
$waterProject = Split-Path -Parent $PSScriptRoot
$waterUnity = 'C:/Program Files/Unity 6000.3.7f1/Editor/Data'
$waterOutput = Join-Path $waterProject 'Logs/DeepSeaDemo'
New-Item -ItemType Directory -Path $waterOutput -Force | Out-Null
$waterRefs = @(Get-ChildItem "$waterUnity/NetStandard/ref/2.1.0" -Filter '*.dll')
$waterRefs += @(Get-ChildItem "$waterUnity/Managed/UnityEngine" -Filter '*.dll')
$waterRefs += @(Get-ChildItem "$waterProject/Library/ScriptAssemblies" -Filter '*.dll' | Where-Object { $_.Name -notmatch '^(AbstractOcclusion.WebGpuWater|Assembly-CSharp)' })
$waterSources = @(Get-ChildItem "$waterProject/Packages/com.abstractocclusion.webgpuwater/Runtime" -Recurse -Filter '*.cs' | ForEach-Object FullName)
$waterArgs = @('/nologo','/target:library','/langversion:latest','/nostdlib+','/define:WEBGPUWATER_URP;ENABLE_INPUT_SYSTEM;UNITY_STANDALONE_WIN',"/out:$waterOutput/Water.XR.CompileCheck.dll")
$waterArgs += @($waterRefs | Sort-Object FullName -Unique | ForEach-Object { '/reference:' + $_.FullName })
$waterArgs += $waterSources
$waterResponse = Join-Path $waterOutput 'water-compile.rsp'
[System.IO.File]::WriteAllLines($waterResponse, ($waterArgs | ForEach-Object { '"' + $_ + '"' }))
& "$waterUnity/NetCoreRuntime/dotnet.exe" "$waterUnity/DotNetSdkRoslyn/csc.dll" "@$waterResponse"
exit $LASTEXITCODE
