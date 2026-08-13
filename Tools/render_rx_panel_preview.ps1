param(
    [string]$OutputPath = "",
    [ValidateSet("CAN1", "CANFD3")][string]$Network = "CAN1",
    [int]$Width = 900,
    [int]$Height = 640,
    [ValidateSet("Elements", "Payload")][string]$Detail = "Elements"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "Reports\RxPanelPreview.png"
}

$vectorApi = "C:\Program Files\Vector CANoe 12.0\Exec64\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll"
$plugin = Join-Path $repoRoot "PanelPlugin\SRS3E2EPanel\bin\Release\SRS3_E2E_PanelControl_1_2_0_0.dll"
if (-not (Test-Path -LiteralPath $vectorApi)) { throw "Vector Panel API not found: $vectorApi" }
if (-not (Test-Path -LiteralPath $plugin)) { throw "Panel plugin not built: $plugin" }

Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
[void][Reflection.Assembly]::LoadFrom($vectorApi)
$assembly = [Reflection.Assembly]::LoadFrom($plugin)
$type = $assembly.GetType("SRS3.E2E.PanelControl.E2ERxControl", $true)
$control = [Activator]::CreateInstance($type)
$control.CurrentNetwork = $Network
$control.Width = $Width
$control.Height = $Height
$size = New-Object Windows.Size($Width, $Height)
$control.Measure($size)
$control.Arrange((New-Object Windows.Rect(0, 0, $Width, $Height)))
$control.UpdateLayout()
$detailTabs = $control.FindName("DetailTabs")
if ($null -ne $detailTabs) { $detailTabs.SelectedIndex = if ($Detail -eq "Payload") { 1 } else { 0 } }
$control.UpdateLayout()
$groupsGrid = $control.FindName("GroupsGrid")
if ($null -ne $groupsGrid) {
    $layout = $groupsGrid.Columns | ForEach-Object { "{0}={1:0.0}/{2}" -f $_.Header, $_.ActualWidth, $_.Visibility }
    Write-Output ("Group columns: " + ($layout -join "; "))
}

$bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($Width, $Height, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32))
$bitmap.Render($control)
$encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory | Out-Null }
$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create)
try { $encoder.Save($stream) } finally { $stream.Dispose() }
Write-Output $OutputPath
