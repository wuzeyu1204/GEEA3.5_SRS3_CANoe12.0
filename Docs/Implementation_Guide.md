# SRS3 E2E CANoe 12 集成与验收指南

## 准备

1. 关闭 CANoe 和 Panel Designer。
2. 保留现有工作树修改，不覆盖 CFG/PDU IG 数据。
3. 确认 CFG 的 System Variables 只有一个 `PanelBridge Int32[320]`。
4. 确认 CFG 仅加载 `VectorSimulationNode` 与 `E2E_RxMonitor_CAN1`，并引用唯一控制台 XVP。

## 补齐 PDU IG

当前 CFG 已保存 `ZCUDZCUDCAN1SignalIPDU37`。请在 PDU Interactive Generator 中再加入：

- `ZCUDZCUDCAN1SignalIPDU01`
- `ZCUDZCUDCAN1SignalIPDU47`
- `ZCUDZCUDCAN1SignalIPDU04`
- `ZCUDZCUDCAN1SignalIPDU10`

由你选择每个对象的 Manual/Event based/Cyclic 模式和周期，并保存 CFG。Panel 不管理这些设置。

## 静态检查

依次执行根目录 `Tools/verify_*.py` 中交接文档列出的七个脚本。补齐五个 PDU IG 对象后，`verify_panel_send_gate.py` 应报告 5 个注册项并通过。

## CANoe 动态验收

1. Compile All Nodes，要求 0 error；保留全部 warning 原文。
2. Start 后不操作 PDU IG，确认没有 Panel/CAPL 自发 Tx。
3. PDU IG 逐个手动触发五个目标，各验证输入 Raw 与最终 Raw。
4. 逐 PDU 切换保护和 UB 策略，核对 Counter、CRC、UB、DataID 与保护帧数。
5. 八类故障各验证至少一帧，核对事件是否放行/抑制及 CRC Calc/Tx 关系。
6. Rx 页核对 CAN1 Group 状态、Counter Delta、CRC Rx/Calc、UB、Age 和 Raw。
7. CANFD3 只在网络、数据库和节点实际接入后验证。

未完成上述步骤前，不得声明 CANoe 编译或总线测试通过。
