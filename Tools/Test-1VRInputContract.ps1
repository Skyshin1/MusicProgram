$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
function Read-Source([string]$relative) { [IO.File]::ReadAllText((Join-Path $project $relative)) }
function Assert-Contract([bool]$ok, [string]$message) { if (-not $ok) { throw $message }; Write-Output "PASS: $message" }
$ui = Read-Source 'Assets/DeepSeaDemo/Runtime/DemoUI.cs'
$router = Read-Source 'Assets/DeepSeaDemo/Runtime/DemoInputRouter.cs'
$catalog = Read-Source 'Assets/DeepSeaDemo/Runtime/DemoTextCatalog.cs'
Assert-Contract ($ui.Contains('typeof(UnityEngine.UI.Button)') -and $ui.Contains('button.onClick.AddListener')) 'Menus use Unity Button onClick'
Assert-Contract (-not $ui.Contains('typeof(BoxCollider)')) 'Menu creation has no physics collider'
Assert-Contract ($ui.Contains('TrackedDeviceGraphicRaycaster') -and $ui.Contains('XRUIInputModule')) 'Standard XRI UI chain is present'
Assert-Contract (-not $router.Contains('i.enabled = enabled')) 'Pause does not disable hand interactors'
Assert-Contract ($router.Contains('selectFilters.Add') -and $router.Contains('selectFilters.Remove')) 'World select filters have symmetric lifetime'
Assert-Contract ($router.Contains('Actions.left.Trigger') -and $router.Contains('Actions.Hand(right).Tracked')) 'Router reads action-based triggers and tracking'
Assert-Contract ($ui.Contains('RenderDocument') -and $ui.Contains('documentPage')) 'Reading panel supports pagination'
$ids = @([regex]::Matches($catalog, 'id = "([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
Assert-Contract (($ids | Select-Object -Unique).Count -eq $ids.Count) 'Text catalog IDs are unique'
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $project 'Assets/DeepSeaDemo/Runtime') -Filter '*.cs') {
    $content = [IO.File]::ReadAllText($file.FullName)
    foreach ($match in [regex]::Matches($content, 'DemoTextCatalog.Get\("([^"]+)"\)')) {
        Assert-Contract ($ids -contains $match.Groups[1].Value) ("Text ID resolves: " + $match.Groups[1].Value)
    }
}
$sourceHash = (Get-FileHash -LiteralPath (Join-Path $project 'Assets/Scenes/1-VR.unity') -Algorithm SHA256).Hash
Assert-Contract ($sourceHash -eq '5D464BC9807B94609374DD8FC62766F278C60908862377F497003D2FCFB4BB44') 'Saved 1-VR source unchanged since approved copy'
Write-Output 'Static contract checks only. Not proof of Unity import, pointer clicks, scene playthrough or headset performance.'
