# SRS3 E2E CANoe 12 工程交接记忆（唯一有效方案）

> 更新日期：2026-08-13
> 本文件覆盖仓库中任何历史 README、设计说明或注释；发生冲突时以本文件为准。

## 1. 操作边界

这是 CANoe 12 的 SRS3 Profile G/P01 E2E 通信与测试工程。

- Codex 可以修改仓库内 Panel、CAPL、节点源码、系统变量模板、脚本和文档。
- 不启动或自动操作 CANoe，不声称 CANoe 编译或总线验证已经通过。
- 用户负责 Compile All、Start Measurement、PDU IG 设置/触发和 Trace 验收。
- 修改 CFG 前必须确认 CANoe 与 Panel Designer 已关闭。
- 工作树包含大量未提交成果，禁止覆盖性回退。
- 当前 CFG 引用的 CBF 保留到用户 Compile All 重新生成。

## 2. 固定架构

```text
PDU Interactive Generator
          |
          | 已产生的 PDU TxPending 事件
          v
VectorSimulationNode / E2E_TxController
  - 保存 PDU IG 输入 Raw
  - 可选 E2E 保护
  - 可选故障注入
  - 原地写回或抑制同一事件
          |
          v
        CAN1
          |
          v
E2E_RxMonitor_CAN1
  - 只接收、判定和统计
  - 不发送报文

Tx 0..63 + 保留 64..95 + Rx 96..319
          |
          v
SRS3_E2E::WpfBridge::PanelBridge Int32[320]
          |
          v
单一 E2EConsoleControl / 单一 XVP
```

必须保持：

1. PDU IG 是五个 Tx PDU 的唯一生产者和手动/周期触发源。
2. Panel/CAPL 不创建新的 Tx 事件或周期发送器。
3. Panel 不编辑应用信号；应用值和触发方式由 PDU IG 管理。
4. 故障只影响命令之后的匹配 TxPending 事件。
5. Rx 只监控；Panel 的网络选择不改变总线路由。
6. CANFD3 在网络、数据库和节点真实配置前保持未验证。

## 3. 唯一 Panel 与桥接

- 唯一 XVP：`Panels/SRS3 E2E Test Console - Manual Import.xvp`
- 唯一导出控件：`SRS3.E2E.PanelControl.E2EConsoleControl`
- 唯一安装脚本：`PanelPlugin/Install_SRS3_E2E_PanelPlugin.bat`
- 唯一系统变量：`SRS3_E2E::WpfBridge::PanelBridge Int32[320]`
- 协议版本：2
- Tx：0..63；保留：64..95；Rx：96..319
- 字段契约：`PanelPlugin/Bridge_Contract.md`

`PanelBridge[320]` 由 `SyaVar/10_SRS3_E2E_Core_SystemVariables.xml` 唯一提供。目标 CFG 只外部引用 01、10 两份系统变量文件，不得再内嵌或加载任何同名桥接变量。

## 4. 五个 Tx PDU

| 索引 | CAN ID | PDU/Group | PDU IG 对象 | 周期 | DataID |
|---:|---:|---|---|---:|---:|
| 0 | 0x040 | VehMtnSt | ZCUDZCUDCAN1SignalIPDU01 | 15 ms | 0x0036 |
| 1 | 0x050 | PreCrashFrontData | ZCUDZCUDCAN1SignalIPDU47 | 10 ms | 0x0411 |
| 2 | 0x076 | VehSpdLgt | ZCUDZCUDCAN1SignalIPDU37 | 20 ms | 0x0037 |
| 3 | 0x0F0 | VehModMngtGlbSafe1 | ZCUDZCUDCAN1SignalIPDU04 | 30 ms | 0x0074 |
| 4 | 0x390 | PassAirbLampStsRec | ZCUDZCUDCAN1SignalIPDU10 | 800 ms | 0x0474 |

重要实际状态：当前磁盘 CFG 已保存五个 PDU IG 注册对象，离线脚本识别为 5/5。触发模式与实际发送行为仍须由用户在 CANoe 中核对，未实测不得声称通过。

## 5. 本轮已落盘

### System Variable / CFG

- `SyaVar` 只保留 01、10 两份 XML；10 中包含唯一的协议 v2 `PanelBridge Int32[320]`。
- 01、10 均可解析，完整变量名无重复；桥接初值严格为 320 项，Tx/Rx 协议位均为 2。
- 已删除旧的 Tx/Rx 分离桥接 XML 和独立统一桥接 20 XML。
- 用户随后在 CANoe 中保存的最新 CFG 系统变量快照与 01+10 完全一致：118 个完整变量名均唯一，只有一个 `PanelBridge[320]`，且不存在 `RxBridge`。Codex 未直接编辑 CFG。
- CFG 只加载 VectorSimulationNode、一个 CAN1 Rx 节点和唯一 XVP。

### Rx

- `E2E_RxMonitor.cin` 使用 `E2E_RX_UNIFIED_OFFSET = 96`。
- 所有遥测与命令均写统一 PanelBridge。
- `E2E_RxExportUnifiedBridge` 路径明确。
- Rx 清通道/清 Group 命令只清统计。
- CAN1 与 CANFD3 节点均无发送路径；CANFD3 节点当前未加入 CFG。
- Watchdog 在 `on start` 启动。

### Tx / 故障

- `applPDUILTxPending -> E2E_TxProcess` 为唯一接入。
- Tx 不使用全局发送许可、单帧/连续 Panel 调度或应用信号覆盖。
- 输入 Raw 写 14..21，最终 Raw 写 22..29。
- 保护关闭时保持 E2E 字段原样；保护开启时原地计算 UB、Counter、CRC/DataID。
- 五 PDU 的保护、UB、状态和保护计数独立。
- 故障 52..63 的 arm/clear/ack 状态机已实现。
- 八类故障：CRC、冻结 Counter、Counter=15、Counter 跳变、错误 DataID、UB=0、Payload、事件抑制。
- 抑制返回 0；其他情况返回 1；不创建新事件。

### Panel / 清理

- Panel 2.2.0.0 已用唯一脚本 `/check` 离线重建和打包；DLL SHA-256 为 `71658B3D97048DC24155E39283ED00B29EE9D1EF8ABC557E8058804AA671EC38`，未执行安装。
- 两个旧 XVP、旧 Tx 控件源码、旧桥接 XML、重复桥接模板和过期迁移说明保持删除。
- 安装包只携带规范的 01/10 系统变量；`PanelPlugin/dist` 是可重建生成目录，已加入忽略且冻结提交前清除。
- Visual Studio 缓存已移出工作区，bin/obj 已由检查脚本清理。
- 根 README、设计、集成和验证文档已统一为当前架构。

## 6. 静态检查结果

通过 7 项（全部为离线/静态检查）：

- `verify_e2e_framework.py`
- `verify_e2e_golden_vectors.py`
- `verify_panel_delivery.py`
- `verify_tx_scheduler_model.py`
- `verify_fault_injection.py`
- `verify_e2e_rx.py`
- `verify_panel_send_gate.py`

额外审计通过：

- 01、10 两份系统变量 XML 可解析、无重复完整变量名，且仅有一个 PanelBridge[320]；
- CAPL/Nodes 中旧桥接、旧 Panel 调度和主动发送路径零命中；
- SyaVar 中旧长度、旧桥接文件零命中；最新 CFG 快照与 01+10 的变量集合完全一致；
- 唯一 Tx 节点、唯一 CAN1 Rx 节点、唯一 XVP；
- 冻结检查时 CANoe/Panel Designer 未运行；Codex 未启动或控制 CANoe；
- PanelPlugin 下无 .vs/bin/obj。

动态证据边界：2026-08-13 用户截图证明 Measurement 已能启动、统一 Panel 已连接，Write 显示 CAN1 checker ready；这仅关闭启动/数组越界问题，不能代表 Tx、故障或 Rx 动态验证通过。

## 7. 下一步（用户在 CANoe）

按 `Docs/Panel_Function_Verification.md` 执行并保存证据，顺序为：

1. Compile All 与启动/Ready 检查；
2. 零自发 Tx 与 PDU IG 唯一发送所有权；
3. 五个 PDU 正常保护、直通、UB 策略和 Counter 14→0；
4. 五 PDU × 八类故障，以及 next/N/continuous/clear 状态机；
5. 十个 CAN1 Rx Group 的状态、CRC、Counter、UB、Age、Raw、Timeout 与恢复；
6. CANFD3 仅在真实网络、数据库和节点配置完成后测试。

## 8. 完成定义

只有以下条件全部满足才可声明工程完成：

- 唯一 Panel、唯一安装脚本、唯一 PanelBridge[320]；
- Tx/Rx CAPL 使用协议 v2 且没有主动发送器；
- 七项静态检查全部通过；
- 用户提供 CANoe Compile All 0 error 证据；
- 用户提供五 PDU、故障注入和 CAN1 Rx 的 Trace/Panel 证据；
- 未实测项始终标记为未验证。
