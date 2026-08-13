# SRS3 E2E CANoe 12 动态验证规程

本文是冻结版的人工 CANoe 验证步骤。离线脚本通过不等于 CANoe 编译、Measurement 或总线行为通过；每个动态项目只有在保存了可复核证据后才能标记 PASS。

## 1. 测试基线

- CANoe：12.0，CAN1 Classical CAN，500 kbit/s。
- 发送节点：`VectorSimulationNode`；接收节点：`E2E_RxMonitor_CAN1`。
- 不加载 `E2E_RxMonitor_CANFD3`，CANFD3 保持未验证。
- 系统变量只加载：
  - `SyaVar/01_CANoe_IL_SystemVariables.xml`
  - `SyaVar/10_SRS3_E2E_Core_SystemVariables.xml`
- 唯一桥接：`SRS3_E2E::WpfBridge::PanelBridge Int32[320]`，协议版本 2。
- 唯一 Panel：`Panels/SRS3 E2E Test Console - Manual Import.xvp`。
- PDU IG 是唯一发送源；Panel 与 CAPL 不得创建额外发送事件。

### 1.1 五个 Tx 对象

| # | CAN ID | Group | PDU IG 对象 | 周期参考 | DataID |
|---:|---:|---|---|---:|---:|
| 0 | 0x040 | VehMtnSt | ZCUDZCUDCAN1SignalIPDU01 | 15 ms | 0x0036 |
| 1 | 0x050 | PreCrashFrontData | ZCUDZCUDCAN1SignalIPDU47 | 10 ms | 0x0411 |
| 2 | 0x076 | VehSpdLgt | ZCUDZCUDCAN1SignalIPDU37 | 20 ms | 0x0037 |
| 3 | 0x0F0 | VehModMngtGlbSafe1 | ZCUDZCUDCAN1SignalIPDU04 | 30 ms | 0x0074 |
| 4 | 0x390 | PassAirbLampStsRec | ZCUDZCUDCAN1SignalIPDU10 | 800 ms | 0x0474 |

## 2. 测试准备与证据

1. 关闭 Measurement，执行 **Compile All Nodes**。
2. 保存 Write 窗口完整文本；通过条件为 0 error。Warning 必须逐条保留，不能只记录数量。
3. Trace 增加过滤器：Tx `0x040,0x050,0x076,0x0F0,0x390`；Rx `0x010,0x012,0x013,0x021,0x070,0x089,0x142,0x148,0x274`。
4. Trace 显示列至少包括：Timestamp、Channel、Direction、ID、DLC、Data、Name。
5. 开启 Logging，保存 BLF；同时准备 Panel 截图和 Write 文本。
6. PDU IG 的五个对象先设为 **Manual / Event based**，关闭自动周期触发，避免干扰单帧判定。
7. 如果真实总线上存在相同 ID 的其他 ECU，必须用 Trace 的 Direction 区分本机 Tx 与总线 Rx；无法区分时该项记为 BLOCKED，不能判 PASS。

建议证据命名：`YYYYMMDD_TC-ID_ID_Group_Result.ext`，例如 `20260813_TX-040_0x040_VehMtnSt_PASS.blf`。

## 3. TC-START：编译和启动

1. Compile All Nodes。
2. 确认 `VectorSimulationNode` 与 `E2E_RxMonitor_CAN1` 均无编译错误。
3. Start Measurement，不操作 PDU IG，等待 5 秒。
4. 检查 Write：应出现 Tx 适配器 ready 与 `[E2E-RX] CAN1 checker ready: 10 protected groups, monitor only`；不得出现 `invalid array range`、CAPL runtime error 或节点停止。
5. 检查 Panel：顶部显示 Tx 保护与 Rx 监控已就绪；五行状态允许显示“等待PDU IG”。

通过条件：0 compile error、无运行时错误、两个节点 Ready、PanelBridge 长度/协议不报错。

## 4. TC-OWN：发送所有权与零自发发送

1. 保持五个 PDU IG 对象为 Manual，清空 Trace。
2. Start 后 5 秒内不触发任何 PDU IG 对象。
3. 检查五个目标 ID 的本机 Tx Direction。
4. 分别点击 Panel 的 PDU 行、保护开关、UB 下拉框，但不要触发 PDU IG。

通过条件：Panel 配置动作不产生 `0x040/050/076/0F0/390` 本机 Tx；只有 PDU IG 触发后才出现对应发送事件。

## 5. TC-TX-NOM：五 PDU 正常保护

对表中五个 PDU 逐一执行：

1. Panel 选择目标行，勾选 E2E 保护，UB 选择 **自动 Auto**，点击“应用配置”。
2. 确认该行保护回读已勾选，状态为“等待PDU IG”。
3. 记下当前帧数、Counter、CRC Calc/Tx 和 Raw。
4. 在 PDU IG 对对应对象执行一次 Manual Trigger。
5. 检查 Trace：只新增 1 个目标 ID 本机 Tx，DLC=8。
6. 检查 Panel：
   - 最近状态=`已保护`；
   - 帧数增加 1；
   - UB=1；
   - DataID 等于表中值；
   - CRC Calc 等于 CRC Tx；
   - “PDU IG输入 Raw”记录触发前数据，“最终发送 Raw”记录实际发送数据。
7. 再触发一次，Counter 应按 `0..14` 模 15 前进，不能出现 15。

通过条件：五个 PDU 全部满足。任一 PDU 的 ID、DLC、DataID、CRC、Counter、UB 或事件数量不符，TC-TX-NOM 失败。

## 6. TC-PASS/UB/CNT：模式、UB 与计数器

### TC-PASS：保护关闭直通

1. 选定一个 PDU，取消 E2E 保护并应用配置。
2. Manual Trigger 一次。
3. 期望状态=`直通`；输入 Raw 与最终 Raw 8 字节完全相同；保护帧数不增加。
4. 恢复保护开启。

### TC-UB：三种 UB 策略

对同一 PDU 依次设置 Auto、Force 0、Force 1，每次应用后触发一帧：

- Auto：当前实现应得到 UB=1；
- Force 0：UB=0，CRC 按该帧应用数据重新计算；
- Force 1：UB=1。

测试完成恢复 Auto。

### TC-CNT：14→0 回卷

1. 保护开启、UB Auto、无故障。
2. 连续 Manual Trigger 至少 16 次。
3. 导出 Trace 中 Counter 序列。
4. 期望序列只包含 0..14，且出现 `13,14,0,1` 连续关系；每帧 CRC Calc=CRC Tx。

## 7. TC-FI：八类故障注入

公共步骤：选择目标 PDU，保护开启、UB Auto；故障范围先选“仅下一帧”；点击“应用故障”后等待命令 Ack/活动状态更新，再 Manual Trigger 一帧。每个故障后再触发一帧验证自动恢复，最后点击“清除当前”或“清除全部”。

| 类型 | 建议参数 | 故障帧期望 | 恢复帧期望 |
|---|---:|---|---|
| 破坏 CRC | 0x01 | CRC Tx = 正常 CRC XOR 0x01；CRC Tx ≠ CRC Calc；应用数据不变 | CRC Tx=Calc |
| 冻结 Counter | 0x01 | Counter 与上一帧重复；状态“故障已作用” | 下一帧恢复递增 |
| Counter=15 | 0x01 | Counter=0xF | 下一帧回到合法 0..14 |
| Counter 跳变 | 2 | Counter 相对内部期望值跳 2，模 15 | 下一帧按跳变后的序列继续 |
| 错误 DataID | 0x01 | 应用数据不变；CRC 用 `正确DataID XOR 1` 计算，所以 CRC Tx≠面板 CRC Calc | CRC Tx=Calc |
| UB=0 | 0x01 | UB=0；其余保护字段有效 | UB 恢复 Auto |
| 破坏 Payload | 0x0001 | 第一应用元素对应 bit 被翻转，CRC 字段仍是破坏前计算值 | Payload 与 CRC 恢复正常 |
| 抑制发送事件 | 0x01 | Panel 状态“事件已抑制”；Trace 中没有该次本机 Tx；故障作用次数+1 | 下一次触发正常发送 |

补充范围测试：

1. “指定帧数”设为 3，故障必须只作用 3 个匹配 TxPending 事件，剩余帧数按 3→2→1→0。
2. “持续”模式至少触发 3 帧，故障持续存在；点击“清除当前”后下一帧恢复。
3. 在两个不同 PDU 上分别设置故障，确认活动 bit mask 独立；“清除全部”后五行故障均清空。

建议先在 0x076 完成八类冒烟测试，再对其余四个 PDU 重复八类测试。冻结版完整覆盖为 5×8=40 个组合。

## 8. TC-RX-CAN1：接收监控

CAN1 监控范围：

| Group | CAN ID | 周期 | Timeout |
|---|---:|---:|---:|
| CrashStsSafe | 0x010 | 200 ms | 800 ms |
| ADataRawSafe | 0x012 | 10 ms | 50 ms |
| AsyDataWithCmpSafe | 0x013 | 10 ms | 50 ms |
| BltLockStSafeAtDrvr | 0x021 | 10 ms | 50 ms |
| RecOfPostImpctBrkgSuspc | 0x070 | 20 ms | 80 ms |
| AgDataRawSafe | 0x089 | 10 ms | 50 ms |
| AgDataRaw2Safe | 0x142 | 30 ms | 120 ms |
| PassAirbLampReq | 0x142 | 30 ms | 120 ms |
| RecOfPostImpctBrkgCfmd | 0x148 | 30 ms | 120 ms |
| SRSResdSig1ToZCUDMSafety | 0x274 | 80 ms | 320 ms |

对每个有真实报文源的 Group：

1. 在“接收监控”选择 CAN1 与目标 Group。
2. 首帧允许显示“初始帧”；后续合法帧应显示“正常”或在合法 Delta 内显示“允许丢帧”。
3. 检查 Frames 增加、Age 周期性回到接近 0、Counter 合法、CRC Rx=CRC Calc、UB=1、Valid Count 增加。
4. 核对 Raw 页的字节与 Trace 完全一致。
5. 停止该 Group 的真实发送源，等待超过表中 Timeout；期望状态=`超时`，Age≥Timeout。
6. 恢复合法发送；期望先重新建立初始状态，再回到正常。
7. 点击“清当前 Group”：只清当前统计，不得发送任何 CAN 报文；点击“清通道”应清 CAN1 全部统计。

若没有可控的 Rx 报文源，只能记录实时正常监控结果；CRC、UB、重复、序列、非法 Counter、DLC 和 Timeout 故障项标记 BLOCKED，不得推定通过。

## 9. 停止条件与测试后清理

出现以下任一情况立即停止当前测试：CAPL runtime error、数组越界、意外周期发送、总线错误帧持续增长、错误 ID/DLC、Panel 与 Trace 数据无法对应。

测试结束：清除全部故障；五个 PDU 恢复保护开启/UB Auto；PDU IG 恢复项目原始触发模式；停止 Logging 和 Measurement；保存 BLF、Write 文本、Panel/Trace 截图及结果表。

## 10. 当前证据状态

2026-08-13 用户截图证明 Measurement 可启动、统一 Panel 已连接，Write 显示 CAN1 checker ready；这只关闭“无法启动/数组越界”问题，不代表上述 Tx、故障和 Rx 动态测试已通过。
