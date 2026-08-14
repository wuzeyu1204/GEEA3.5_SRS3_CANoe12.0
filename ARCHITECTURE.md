# SRS3 E2E CANoe 12 内部架构设计

> 冻结基线：2026-08-14
> 本文是工程内部唯一详细设计依据；客户操作入口以根目录 `README.md` 为准。

## 1. 目标与边界

工程实现AUTOSAR E2E Profile G/P01行为的CANoe 12测试适配器，覆盖：

- 五个CAN1 Tx PDU的原地E2E保护与故障注入；
- 十个CAN1和五个CANFD3 Rx Group的只读监控；
- 单一WPF Panel对Tx/Rx的配置、遥测和统计显示；
- 可复现的规则生成、Golden Vector和静态架构检查。

不属于工程职责：

- Panel不编辑PDU应用信号；
- Panel/CAPL不创建PDU发送事件或周期；
- Rx节点不产生任何总线报文；
- Panel网络选择不改变CANoe硬件路由、数据库或位时序；
- 离线脚本不能替代CANoe编译和总线验证。

## 2. 固定架构

```text
PDU Interactive Generator
          |
          | 已产生的PDU TxPending事件
          v
VectorSimulationNode / E2E_TxController
  1. 保存PDU IG输入Raw
  2. 消费该PDU的保护/UB配置
  3. 可选E2E保护
  4. 可选故障注入
  5. 原地写回或抑制同一事件
          |
          v
        CAN1

CAN1 -----------------> E2E_RxMonitor_CAN1 ------+
CANFD3 ---------------> E2E_RxMonitor_CANFD3 ----+--> PanelBridge[320]
                                                       |
                                                       v
                                              E2EConsoleControl
```

必须保持的设计不变量：

1. PDU IG是五个Tx对象唯一的生产者和调度器。
2. `applPDUILTxPending -> E2E_TxProcess` 是唯一Tx接入点。
3. 除“抑制发送事件”返回0外，其余路径返回1放行原事件。
4. 故障只作用于命令之后的匹配PDU事件。
5. Rx节点只接收、判断、统计和清零本地统计。
6. 全工程只有一个 `PanelBridge Int32[320]`、一个导出Panel控件和一个XVP。
7. CAN1与CANFD3现场连接互斥，必须分轮测试。

## 3. 权威源与生成物

| 层级 | 权威文件 | 作用 |
|---|---|---|
| 规则源 | `Config/E2E_Rules_Manifest.json` | 五个Tx PDU、E2E位布局和应用元素 |
| Rx规则 | `Config/E2E_Rx_Rules.json` | 仓库内冻结的十五个Rx Group、超时策略及来源追溯 |
| 算法 | `CAPL/E2E/E2E_ProfileG_Core.cin` | CRC、Counter和Profile G基础逻辑 |
| 位操作 | `CAPL/E2E/E2E_BitCodec.cin` | Intel/Motorola原始位读写 |
| Tx适配 | `CAPL/E2E/E2E_TxController.cin` | TxPending原地后处理与遥测 |
| 故障 | `CAPL/E2E/E2E_FaultInjection.cin` | 八类故障和命令状态机 |
| Rx监控 | `CAPL/E2E/E2E_RxMonitor.cin` | Rx判定、超时、统计和桥接导出 |
| 生成规则 | `CAPL/E2E/Generated/*.cin` | 由JSON冻结的CAPL规则表 |
| 节点 | `Nodes/*.can` | CANoe节点入口 |
| Panel | `PanelPlugin/SRS3E2EPanel/*` | WPF控件源码 |
| 系统变量 | `SyaVar/01_*.xml`、`SyaVar/10_*.xml` | IL变量和唯一统一桥接 |
| CAN1配置 | `GEEA3.5_SRS3_CAN1_12.0.cfg` | Tx/PDU IG与CAN1 Rx工况 |
| CANFD3配置 | `GEEA3.5_SRS3_CANFD3_12.0.cfg` | 纯接收CANFD3工况 |
| 算法证据 | `Test/GoldenVectors.json`、`Test/RxGoldenVectors.json` | 独立Golden Vector |

`.cbf` 是当前CFG引用的CANoe编译产物，冻结版保留：

- `Nodes/VectorSimulationNode.cbf`
- `Nodes/E2E_RxMonitor.cbf`
- `Nodes/E2E_RxMonitor_CANFD3.cbf`

`bin`、`obj`、`PanelPlugin/dist`、`Log`、`Reports`、IDE缓存和预览图片均为可重建临时产物，不纳入冻结版。

## 4. Profile G/P01算法

冻结规则采用AUTOSAR E2E P01行为：

- CRC：CRC-8/SAE-J1850-ZERO；多项式 `0x1D`；Init `0x00`；XorOut `0x00`；不反射；
- DataID：低字节在前、高字节在后参与CRC；
- CRC字段本身不参与CRC输入；
- Counter：4 bit，合法值0..14，模15递增，15为非法值；
- 应用元素顺序：按ApplicationRecordElement名称不区分大小写排序；
- 多字节逻辑元素内部序列化为little-endian；
- 物理PDU位布局按DBC/ARXML的packing byte order处理；
- 非整字节元素补零到整字节，不做符号扩展；
- Rx超时：`max(50 ms, 4 × cycle_ms)`。

任何算法、位布局或元素顺序调整都必须同步更新规则JSON、生成CAPL和Golden Vector。

Rx离线验证只读取当前仓库内的 `Config/E2E_Rx_Rules.json`，不访问相邻ZXDoc仓库。该快照的 `provenance` 保留原始仓库、源内相对路径、commit和源文件SHA256，用于规则追溯但不构成运行依赖。

## 5. Tx设计

### 5.1 目标PDU

| Index | CAN ID | Group | PDU IG对象 | DLC | 周期 | DataID |
|---:|---:|---|---|---:|---:|---:|
| 0 | 0x040 | VehMtnSt | ZCUDZCUDCAN1SignalIPDU01 | 8 | 15 ms | 0x0036 |
| 1 | 0x050 | PreCrashFrontData | ZCUDZCUDCAN1SignalIPDU47 | 8 | 10 ms | 0x0411 |
| 2 | 0x076 | VehSpdLgt | ZCUDZCUDCAN1SignalIPDU37 | 8 | 20 ms | 0x0037 |
| 3 | 0x0F0 | VehModMngtGlbSafe1 | ZCUDZCUDCAN1SignalIPDU04 | 8 | 30 ms | 0x0074 |
| 4 | 0x390 | PassAirbLampStsRec | ZCUDZCUDCAN1SignalIPDU10 | 8 | 800 ms | 0x0474 |

### 5.2 TxPending处理顺序

1. 按PDU名称查找规则；非目标PDU直接返回1。
2. 仅当本次TxPending与Panel待应用的目标PDU匹配时，消费保护开关和UB策略；不相关PDU保持命令Pending。
3. 保存PDU IG输入Byte 0..7到桥接14..21。
4. 保护关闭时保持E2E字段原样并标记直通。
5. 保护开启时计算合法Counter、DataID/CRC和UB。
6. 消费匹配PDU的故障命令并按范围更新剩余次数。
7. 保存最终Byte 0..7到桥接22..29并更新每PDU遥测。
8. 正常路径返回1；事件抑制故障返回0。

保护、UB、状态、保护帧数和故障活动状态均按五个PDU独立保存。

### 5.3 UB策略

| 值 | 模式 | 行为 |
|---:|---|---|
| 0 | Auto | 保护开启时UB=1 |
| 1 | Force 0 | 强制UB=0，并按最终数据计算CRC |
| 2 | Force 1 | 强制UB=1，并按最终数据计算CRC |

### 5.4 故障状态机

| 类型 | 参数示例 | 作用 |
|---|---:|---|
| 破坏CRC | 0x01 | 发送CRC按配置掩码异或 |
| 冻结Counter | 0x01 | 重复上一Counter |
| Counter=15 | 0x01 | 写入非法Counter 0xF |
| Counter跳变 | 2 | 对期望值做模15跳变 |
| 错误DataID | 0x01 | 使用正确DataID异或参数参与CRC |
| UB=0 | 0x01 | 强制故障帧UB为0 |
| 破坏Payload | 0x0001 | CRC计算后翻转首个应用元素对应bit |
| 抑制发送事件 | 0x01 | 返回0，不放行当前TxPending |

范围支持下一帧、指定帧数和持续生效；命令支持应用、清当前和清全部，并使用Sequence/Ack避免重复消费。

Freeze Counter重复“上一实际发送Counter”，不推进已经保存的下一合法Counter。因此单次冻结为 `2 → 2 → 3`，边界为 `14 → 14 → 0`，持续冻结清除后为 `5 → 5 → 5 → 6`。

## 6. Rx设计

两个Rx节点共享规则与实现，只通过 `E2E_RxInitialize(bus_index)` 选择总线。接收流程为：匹配Group、检查DLC、解析UB/CRC/Counter、重建CRC输入、判断INITIAL/OK/REPEATED/CRC_ERROR/UB_INACTIVE/非法Counter，并由watchdog处理Timeout。共享CAN ID按Group独立维护状态，不能只给整帧一个结论。

进入Timeout时同时失效该Group的历史Counter序列基准；恢复后的第一张合法帧重新进入INITIAL，下一张连续帧进入OK，即 `OK → TIMEOUT → INITIAL → OK`。

### 6.1 CAN1范围

| Index | CAN ID | Group | DLC | 周期 | Timeout |
|---:|---:|---|---:|---:|---:|
| 0 | 0x010 | CrashStsSafe | 4 | 200 ms | 800 ms |
| 1 | 0x012 | ADataRawSafe | 8 | 10 ms | 50 ms |
| 2 | 0x013 | AsyDataWithCmpSafe | 8 | 10 ms | 50 ms |
| 3 | 0x021 | BltLockStSafeAtDrvr | 8 | 10 ms | 50 ms |
| 4 | 0x070 | RecOfPostImpctBrkgSuspc | 8 | 20 ms | 80 ms |
| 5 | 0x089 | AgDataRawSafe | 8 | 10 ms | 50 ms |
| 6 | 0x142 | AgDataRaw2Safe | 8 | 30 ms | 120 ms |
| 7 | 0x142 | PassAirbLampReq | 8 | 30 ms | 120 ms |
| 8 | 0x148 | RecOfPostImpctBrkgCfmd | 8 | 30 ms | 120 ms |
| 9 | 0x274 | SRSResdSig1ToZCUDMSafety | 8 | 80 ms | 320 ms |

### 6.2 CANFD3范围

| Index | CAN ID | Group | DLC | 周期 | Timeout |
|---:|---:|---|---:|---:|---:|
| 10 | 0x032 | ADataRawSafe | 64 | 5 ms | 50 ms |
| 11 | 0x032 | AgDataRawSafe | 64 | 5 ms | 50 ms |
| 12 | 0x032 | AsyDataWithCmpSafe | 64 | 5 ms | 50 ms |
| 13 | 0x032 | IntgtIMUDataRawSafe | 64 | 5 ms | 50 ms |
| 14 | 0x03F | CrashStsSafe | 2 | 200 ms | 800 ms |

Rx命令只清当前Group或当前通道统计，不发送总线报文。

## 7. 统一PanelBridge协议

唯一变量：

```text
SRS3_E2E::WpfBridge::PanelBridge  Int32[320]
ProtocolVersion = 2
Tx = 0..63
Reserved = 64..95
Rx = 96..319
```

### 7.1 Tx区0..63

| Index | 方向 | 含义 |
|---:|---|---|
| 0 | 双向 | 协议版本2 |
| 1 | Panel→Tx | 当前PDU 0..4 |
| 3 | Panel→Tx | UB策略0/1/2 |
| 4 | Panel→Tx | 保护开关0/1 |
| 6 | Panel→Tx | 配置应用命令 |
| 8 | Tx→Panel | 当前处理状态 |
| 9 | Tx→Panel | Counter |
| 10 | Tx→Panel | DataID |
| 11 | Tx→Panel | CRC Calc |
| 12 | Tx→Panel | CRC Tx |
| 13 | Tx→Panel | 实际UB |
| 14..21 | Tx→Panel | PDU IG输入Raw B0..B7 |
| 22..29 | Tx→Panel | 最终发送Raw B0..B7 |
| 30 | Tx→Panel | 当前PDU保护帧数 |
| 31 | Tx→Panel | Tx Bridge Active |
| 32..36 | Tx→Panel | 五PDU保护回读 |
| 37..41 | Tx→Panel | 五PDU UB回读 |
| 42..46 | Tx→Panel | 五PDU最近状态 |
| 47..51 | Tx→Panel | 五PDU保护帧数 |
| 52..55 | Panel→Tx | 故障类型、范围、参数、帧数 |
| 56..57 | Panel→Tx | 故障命令、Sequence |
| 58..63 | Tx→Panel | 活动类型、剩余、次数、结果、Mask、Ack |

### 7.2 Rx区96..319

下表为相对索引，绝对索引需加96。

| Relative | 方向 | 含义 |
|---:|---|---|
| 0 | 双向 | Rx协议版本2 |
| 1 | Panel→Rx | 当前Group 0..14 |
| 3 | Rx→Panel | CAN1 Ready |
| 4 | Rx→Panel | CANFD3 Ready |
| 8 | Rx→Panel | Counter Delta |
| 19 | Rx→Panel | 当前Payload Group Ack |
| 20..34 | Rx→Panel | 十五Group状态 |
| 35..49 | Rx→Panel | 接收帧数 |
| 50..64 | Rx→Panel | Age/ms |
| 65..79 | Rx→Panel | Counter |
| 80..94 | Rx→Panel | CRC Rx |
| 95..109 | Rx→Panel | CRC Calc |
| 110..124 | Rx→Panel | UB |
| 125..139 | Rx→Panel | Valid Count |
| 140..154 | Rx→Panel | Error Count |
| 155..218 | Rx→Panel | 当前Group Raw B0..B63 |
| 219..221 | Panel→Rx | 清零命令、通道、Sequence |
| 222 | Rx→Panel | 命令Ack |

安全门控：未绑定、数组不足320、协议版本不为2或Bridge Active未就绪时，配置和故障操作保持禁用并清空陈旧遥测。

## 8. Panel 2.2.2设计

唯一导出控件为 `SRS3.E2E.PanelControl.E2EConsoleControl`，唯一XVP为 `Panels/SRS3 E2E Test Console - Manual Import.xvp`。Rx控件只作为嵌入视图，不单独导出。

Tx表格每行包含E2E保护、UB策略和应用按钮：

- `RequestedProtectionEnabled` / `RequestedUbMode` 保存待应用值；
- `ProtectionEnabled` / `UbMode` 保存CAPL回读值；
- 200 ms轮询只更新回读，不覆盖尚未应用或正在等待的行内值；
- 点击该行“应用”写索引1/3/4/6；
- 对应TxPending消费命令前按钮显示“等待”，期间禁止并发配置；
- 命令清零且32..41回读匹配后结束pending状态。

Panel只显示功能名称、状态和值，不保留说明性副标题、操作注释或页脚。

## 9. 系统变量与CFG

工程只保留两份外部系统变量：

1. `01_CANoe_IL_SystemVariables.xml`：CANoe PDU-IL基础变量；
2. `10_SRS3_E2E_Core_SystemVariables.xml`：E2E控制、状态、故障、测试变量及唯一PanelBridge。

两份XML合计变量全名必须唯一；不得恢复旧Tx/Rx分离桥接、独立20/30模板或内嵌同名PanelBridge。CAN1 CFG保存五个PDU IG对象并加载 `VectorSimulationNode`、`E2E_RxMonitor_CAN1`；CANFD3 CFG只加载 `E2E_RxMonitor_CANFD3`，不得残留CAN1 PDU IG、`VectorSimulationNode`或CAN1桌面引用。两份CFG均由CANoe维护，修改前必须关闭CANoe和Panel Designer。

## 10. CAN1/CANFD3互斥工况

CANFD3固定逻辑参数：

- 网络：`ZCUD_CANFD3`
- ISO CAN FD
- 仲裁500 kbit/s
- 数据2 Mbit/s
- BRS开启
- 配置数据库：`Databases/SDB325300_SRS3_AR-4.2.2_251215_UnFlattened.arxml` 中的 `ZCUD_CANFD3` Cluster
- 交叉检查DBC：`Databases/CANFD3/EEA35_SDB325300_KO11_ADCU11_ZCUD_CANFD3_251215.dbc`，不与同源ARXML重复加载
- 目标ID：0x032、0x03F

`CANFD3` 是逻辑名称，不代表物理Channel 3。现场只能选择CAN1或CANFD3连接时，测试分两轮：

- CAN1轮：打开 `GEEA3.5_SRS3_CAN1_12.0.cfg`，物理通道设Classical CAN 500 kbit/s，采集CAN1证据；
- CANFD3轮：停Measurement，打开 `GEEA3.5_SRS3_CANFD3_12.0.cfg`，将实际接线通道切换为ISO CAN FD 500k/2M/BRS，采集CANFD3证据。

Panel网络下拉框只切换数据显示，不能代替硬件配置。两轮证据必须分别存档，不能合并成“同时通过”。

## 11. 构建、安装与检查

唯一安装入口：`PanelPlugin/Install_SRS3_E2E_PanelPlugin.bat`。

脚本流程：进程门禁、清理生成目录、定位CANoe 12 API和MSBuild、Release重建、打包唯一XVP与01/10系统变量、静态审核、UAC安装、源/目标字节校验。脚本不启动CANoe，也不修改CFG/CAPL/SysVar。

冻结版七项静态检查：

1. `verify_e2e_framework.py`
2. `verify_e2e_golden_vectors.py`
3. `verify_panel_delivery.py`
4. `verify_tx_scheduler_model.py`
5. `verify_fault_injection.py`
6. `verify_e2e_rx.py`
7. `verify_panel_send_gate.py`

静态通过范围：5个Tx PDU、16个Tx应用元素、15个Rx Group、15条Tx Golden Vector、75条Rx Golden Vector、75项故障/状态语义、双CFG角色隔离、零CAPL主动发送器和统一PanelBridge契约。

## 12. 冻结状态与完成定义

Panel 2.2.2.0 已在本机使用CANoe 12 API离线构建并安装；安装DLL独立复核SHA-256为：

```text
E42C93C76948B6DA3B4D4A76F507B49929DF2C8BFA29C6D980D6C562773C54F9
```

该哈希只记录本次本机构建，不作为跨机器可复现构建承诺。

只有以下动态证据齐全后才能声明工程完成：

- Compile All 0 error，warning原文已保存；
- Start无CAPL runtime error或数组越界；
- PDU IG零自发Tx和单次触发单次Tx证据；
- 五个Tx PDU正常保护、直通、三种UB和Counter 14→0；
- 五PDU×八故障及next/N/continuous/clear状态机；
- CAN1十个Rx Group的状态、CRC、Counter、UB、Age、Raw、Timeout和恢复；
- CANFD3在真实网络/通道配置下的五Group证据；
- BLF/ASC、Write文本、Panel/Trace截图和结果清单可关联到同一Git提交。

当前只能声明源码、离线算法、静态架构和Panel安装检查通过；未由用户实测的CANoe编译与总线项目保持Pending或Blocked。
