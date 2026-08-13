# SRS3 E2E CAPL

当前实现采用统一协议 v2，并严格区分发送所有权与 E2E 后处理：

- `Generated/E2E_TxRules_Generated.cin`：五个 Tx PDU 的 DataID、DLC、E2E 位和应用元素规则。
- `E2E_ProfileG_Core.cin`：Profile G/P01 CRC 与 Counter 基础算法。
- `E2E_BitCodec.cin`：DBC Motorola/Intel 位布局读写。
- `E2E_FaultInjection.cin`：五个 PDU 独立的八类故障状态机和统一桥接 52..63 命令协议。
- `E2E_TxController.cin`：消费 PDU IG 已产生的 TxPending 事件，原地保护、注入故障或抑制同一事件。
- `Generated/E2E_RxRules_Generated.cin` 与 `E2E_RxMonitor.cin`：CAN1/CANFD3 接收判定和统一桥接 96..319 遥测。

PDU Interactive Generator 是五个 Tx PDU 的唯一生产者及手动/周期触发源。Panel 只配置保护、UB 策略与故障，不编辑应用信号；Rx 节点只接收、判定和统计。

CANoe Compile All、Start Measurement、PDU IG 触发和 Trace 验收尚需在 CANoe 12 中由用户执行。
