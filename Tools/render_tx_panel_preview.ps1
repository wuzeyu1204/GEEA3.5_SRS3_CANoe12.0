param(
    [string]$OutputPath = "",
    [int]$Width = 1040,
    [int]$Height = 680,
    [ValidateSet("Protection", "RxElements", "RxPayload", "RxEvents")]
    [string]$View = "Protection",
    [ValidateSet("CAN1", "CANFD3")]
    [string]$Network = "CAN1"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot ("Reports\E2EConsole_{0}_{1}.png" -f $View, $Network)
}

function Find-PanelApi {
    $roots = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($env:CANOE12_ROOT)) { $roots.Add($env:CANOE12_ROOT) }
    foreach ($candidate in @(
        "C:\Program Files\Vector CANoe 12.0",
        "C:\Program Files (x86)\Vector CANoe 12.0"
    )) { $roots.Add($candidate) }

    foreach ($root in $roots | Select-Object -Unique) {
        foreach ($exec in @("Exec64", "Exec32")) {
            $candidate = Join-Path $root "$exec\Components\Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll"
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }
    throw "Vector.PanelControlPlugin 1.2.0.0 was not found. Set CANOE12_ROOT to the CANoe 12 install folder."
}

$vectorApi = Find-PanelApi
$plugin = Join-Path $repoRoot "PanelPlugin\SRS3E2EPanel\bin\Release\SRS3_E2E_PanelControl_1_2_0_0.dll"
if (-not (Test-Path -LiteralPath $plugin)) { throw "Panel plugin not built: $plugin" }

Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
[void][Reflection.Assembly]::LoadFrom($vectorApi)
$assembly = [Reflection.Assembly]::LoadFrom($plugin)
$type = $assembly.GetType("SRS3.E2E.PanelControl.E2EConsoleControl", $true)
$control = [Activator]::CreateInstance($type)
$control.Width = $Width
$control.Height = $Height

$consoleTabs = $control.FindName("ConsoleTabs")
$rxControl = $control.FindName("UnifiedRxControl")
if ($View -eq "Protection") {
    $consoleTabs.SelectedIndex = 0
} else {
    $consoleTabs.SelectedIndex = 1
    $rxControl.CurrentNetwork = $Network
    $detailTabs = $rxControl.FindName("DetailTabs")
    $detailTabs.SelectedIndex = switch ($View) {
        "RxPayload" { 1 }
        "RxEvents" { 2 }
        default { 0 }
    }
}

$size = New-Object Windows.Size($Width, $Height)
$control.Measure($size)
$control.Arrange((New-Object Windows.Rect(0, 0, $Width, $Height)))
$control.UpdateLayout()

$bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($Width, $Height, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32))
$bitmap.Render($control)
$encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory | Out-Null }
$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create)
try { $encoder.Save($stream) } finally { $stream.Dispose() }
Write-Output ("Rendered {0} ({1}x{2}): {3}" -f $View, $Width, $Height, $OutputPath)
