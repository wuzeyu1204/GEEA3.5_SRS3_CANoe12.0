param(
    [string]$OutputPath = "",
    [ValidateSet("CAN1", "CANFD3")][string]$Network = "CAN1",
    [int]$Width = 1040,
    [int]$Height = 680,
    [ValidateSet("Elements", "Payload", "Events")][string]$Detail = "Elements"
)

$view = switch ($Detail) {
    "Payload" { "RxPayload" }
    "Events" { "RxEvents" }
    default { "RxElements" }
}

& (Join-Path $PSScriptRoot "render_tx_panel_preview.ps1") `
    -OutputPath $OutputPath -Width $Width -Height $Height -View $view -Network $Network
exit $LASTEXITCODE
