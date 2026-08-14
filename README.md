# GEEA3.5 SRS3 E2E CANoe 12

本工程用于 GEEA3.5 SRS3 Profile G/P01 的 CANoe 12 总线集成测试，提供 CAN1 发送保护/故障注入、CAN1/CANFD3 接收监控及统一 WPF Panel。

## 设计概要

```text
PDU Interactive Generator
  -> applPDUILTxPending
  -> E2E_TxProcess（原地保护、故障注入或抑制同一事件）
  -> CAN1

CAN1 / CANFD3
  -> E2E_RxMonitor（只接收、判定和统计）

Tx/Rx CAPL <-> PanelBridge Int32[320] <-> SRS3 E2E 测试控制台
```

- PDU Interactive Generator 是五个 Tx PDU 的唯一发送事件源和周期/手动触发源。
- Panel 只配置 E2E保护、UB策略和故障，不编辑应用信号，也不创建发送事件。
- Tx CAPL 只处理 PDU IG 已产生的 `TxPending`，不维护第二套调度器。
- Rx CAPL 不发送报文，只检查 DLC、UB、CRC、Counter序列、超时和恢复。
- CAN1 与 CANFD3 使用同一现场连接时互斥测试，不能同时判定通过。

详细算法、桥接字段和维护约束见 [ARCHITECTURE.md](ARCHITECTURE.md)。

## 交付内容

| 内容 | 路径 |
|---|---|
| CAN1配置 | `GEEA3.5_SRS3_CAN1_12.0.cfg` |
| CANFD3配置 | `GEEA3.5_SRS3_CANFD3_12.0.cfg` |
| Tx节点 | `Nodes/VectorSimulationNode.can` |
| CAN1 Rx节点 | `Nodes/E2E_RxMonitor.can` |
| CANFD3 Rx节点 | `Nodes/E2E_RxMonitor_CANFD3.can` |
| 唯一Panel | `Panels/SRS3 E2E Test Console - Manual Import.xvp` |
| Panel安装脚本 | `PanelPlugin/Install_SRS3_E2E_PanelPlugin.bat` |
| 系统变量 | `SyaVar/01_CANoe_IL_SystemVariables.xml`、`SyaVar/10_SRS3_E2E_Core_SystemVariables.xml` |
| 临时测试清单 | `TEST_COVERAGE_CHECKLIST.txt` |

## 环境

- Vector CANoe 12.0，PanelControlPlugin API 1.2.0.0
- .NET Framework 4.7.2
- Panel FileVersion 2.2.2.0
- CAN1：Classical CAN，500 kbit/s
- CANFD3：ISO CAN FD，500 kbit/s仲裁、2 Mbit/s数据相位、BRS开启

## 安装Panel

1. 完全关闭 CANoe 和 Panel Designer。
2. 双击 `PanelPlugin\Install_SRS3_E2E_PanelPlugin.bat`。
3. UAC弹窗选择“是”。
4. 脚本会重新构建、静态审核、清理旧 DLL，并安装唯一的 `SRS3_E2E_PanelControl_1_2_0_0.dll`。

脚本不会启动 CANoe，也不会修改 CFG、CAPL、数据库或系统变量定义。

## CANoe配置

1. System Variables 只加载：
   - `SyaVar\01_CANoe_IL_SystemVariables.xml`
   - `SyaVar\10_SRS3_E2E_Core_SystemVariables.xml`
2. 确认唯一桥接变量为 `SRS3_E2E::WpfBridge::PanelBridge Int32[320]`。
3. 导入 `Panels\SRS3 E2E Test Console - Manual Import.xvp`。
4. 将Panel控件绑定到上述 `PanelBridge`。
5. CAN1工况打开 `GEEA3.5_SRS3_CAN1_12.0.cfg`，使用 `VectorSimulationNode` 和 `E2E_RxMonitor_CAN1`，五个PDU IG对象仅存在于该配置。
6. CANFD3工况打开 `GEEA3.5_SRS3_CANFD3_12.0.cfg`，只使用 `E2E_RxMonitor_CANFD3`；该配置不加载 `VectorSimulationNode`、PDU IG或任何CANFD3发送源。

## Panel使用

1. 在PDU表格目标行直接勾选或取消“E2E保护”。
2. 在同一行选择UB策略：`自动 Auto`、`强制 0`、`强制 1`。
3. 点击同一行“应用”；按钮显示“等待”时，在PDU IG触发对应对象。
4. CAPL消费下一次匹配的 `TxPending` 后，按钮恢复“应用”。
5. 通过状态、Counter、CRC Calc/Tx、UB、输入Raw和最终Raw确认结果。

五个Tx对象为：

| CAN ID | Group | PDU IG对象 |
|---:|---|---|
| 0x040 | VehMtnSt | ZCUDZCUDCAN1SignalIPDU01 |
| 0x050 | PreCrashFrontData | ZCUDZCUDCAN1SignalIPDU47 |
| 0x076 | VehSpdLgt | ZCUDZCUDCAN1SignalIPDU37 |
| 0x0F0 | VehModMngtGlbSafe1 | ZCUDZCUDCAN1SignalIPDU04 |
| 0x390 | PassAirbLampStsRec | ZCUDZCUDCAN1SignalIPDU10 |

## CANFD3切换

CANFD3是逻辑网络名，不等于必须使用Vector硬件Channel 3。专用配置已选择ARXML中的 `ZCUD_CANFD3` Cluster，应映射到实际接线通道：

1. 停止Measurement。
2. 打开 `GEEA3.5_SRS3_CANFD3_12.0.cfg`。
3. 将实际接线通道设置为ISO CAN FD、500 kbit/s仲裁、2 Mbit/s数据相位、BRS开启。
4. 确认Simulation Setup中只有 `E2E_RxMonitor_CANFD3`，按 `0x032` 和 `0x03F` 过滤Trace。
5. `Databases/CANFD3/EEA35_SDB325300_KO11_ADCU11_ZCUD_CANFD3_251215.dbc` 作为独立解析/交叉检查基线，不与配置中的同源ARXML Cluster重复加载。
6. CAN1与CANFD3分别保存Write、Trace和测试结论，不合并为一次测试。

## 验证边界

仓库七项离线静态检查已通过，Panel 2.2.2.0 已完成本机离线构建和安装校验。CANoe Compile All、Measurement、总线行为、五个Tx对象、八类故障及Rx动态结果仍必须按 `TEST_COVERAGE_CHECKLIST.txt` 由测试人员实测；未保存证据的项目不得标记PASS。
