# GEEA3.5 SRS3 E2E CANoe 12

本工程实现 SRS3 Profile G/P01 的 CAN1 Tx 保护/故障注入与 Rx 监控。当前唯一有效架构和工作边界见 [CODEX_HANDOFF.md](CODEX_HANDOFF.md)。

## 架构

```text
PDU Interactive Generator
  -> applPDUILTxPending
  -> E2E_TxProcess（原地保护/故障/抑制）
  -> CAN1
  -> E2E_RxMonitor_CAN1（只接收和统计）
  -> PanelBridge Int32[320]
  -> E2EConsoleControl
```

PDU IG 是五个目标 PDU 的唯一生产者及手动/周期触发源。Panel 只配置保护、UB 策略和故障，不编辑应用信号；CAPL 不创建新的 Tx 事件。

## 当前静态状态

- `SyaVar/10_SRS3_E2E_Core_SystemVariables.xml` 提供唯一 `SRS3_E2E::WpfBridge::PanelBridge Int32[320]`，协议版本 2；
- Tx 使用统一桥接 0..63，Rx 使用偏移 96..319；
- CFG 只加载 `VectorSimulationNode`、一个 CAN1 Rx 节点和唯一 XVP；
- 旧分离桥接模板、旧 XVP 和旧 Tx 控件源码已移除；
- Panel 离线构建/打包通过，版本 2.2.0.0；
- 最新 CFG 系统变量快照与 `SyaVar` 的 01+10 完全一致，118 个变量无重复；五个目标 PDU IG 注册对象均已保存。

## 验证边界

离线脚本不等于 CANoe 集成验证。Compile All、Measurement、五个 Tx ID、八类故障和 CAN1 Rx Trace 均由用户在 CANoe 12 中执行；CANFD3 在网络、数据库和节点配置完成前保持未验证。
