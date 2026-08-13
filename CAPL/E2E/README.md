# CAPL E2E framework

当前Tx节点按以下顺序包含E2E实现：

1. `E2E_ProfileG_Core.cin`
2. `E2E_BitCodec.cin`
3. `Generated/E2E_TxRules_Generated.cin`
4. `E2E_FaultInjection.cin`
5. `E2E_TxController.cin`

接收节点另外包含：

1. `E2E_ProfileG_Core.cin`
2. `E2E_BitCodec.cin`
3. `Generated/E2E_RxRules_Generated.cin`
4. `E2E_RxMonitor.cin`

WPF Panel不直接访问总线。Tx通过`PanelBridge`向`E2E_TxController.cin`提交许可和应用Raw；Rx监控节点通过`RxBridge`发布状态、计数和最后Payload。

`E2E_FaultInjection.cin`当前仍为安全透传占位，负向故障注入尚未完成，不得作为已实现功能对外宣称。

