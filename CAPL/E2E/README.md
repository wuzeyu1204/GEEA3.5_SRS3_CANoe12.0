# CAPL E2E framework

This directory contains the isolated E2E implementation framework.

The files are intentionally **not included** by `Nodes/VectorSimulationNode.can`
yet. The current PDU-IL communication baseline therefore remains unchanged.

Planned include order:

1. `E2E_ProfileG_Core.cin`
2. `E2E_BitCodec.cin`
3. `Generated/E2E_TxRules_Generated.cin`
4. `E2E_FaultInjection.cin`
5. `E2E_TxController.cin`
6. `E2E_PanelController.cin`

Do not enable `gE2E_FrameworkEnable` until the generated signal bindings and
golden-vector tests have been completed.

