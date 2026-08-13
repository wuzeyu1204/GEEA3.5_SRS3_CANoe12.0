# SRS3 E2E Panel套件与单工程双通道设计

## 1. 事实边界

本设计不把数据库文件存在等同于运行通道已经可用：

- 当前运行配置只装载CAN1网络、CAN1 PDU-IL和 `VectorSimulationNode.can`；
- 5个已实现Tx PDU全部属于CAN1；
- CANFD3 DBC存在，但未加入当前CFG；
- CANFD3的E2E范围是SRS3发送的 `0x032` 四个Group与 `0x03F` 一个Group，因此本工程方向为Rx；
- CANFD3仲裁相位、数据相位、BRS和硬件通道映射尚未冻结。

所以可以使用一个CANoe工程实现CAN1/CANFD3选择，但选择器必须按功能分层：Tx Control固定CAN1；Rx Monitor可以选择CAN1或CANFD3。CANFD3未Ready时只显示配置状态，不允许显示伪造的实时接收结果。

## 2. 单工程结构

```text
GEEA3.5_SRS3_E2E_12.0.cfg
├─ CAN1 network
│  ├─ existing PDU-IL simulation node
│  ├─ E2E Tx Controller
│  └─ CAN1 Rx Monitor
├─ CANFD3 network
│  ├─ CANFD3 DBC
│  ├─ physical CAN FD channel mapping
│  └─ CANFD3 Rx Monitor
├─ Panel suite
│  ├─ E2E Tx Control       [CAN1 Tx]
│  ├─ E2E Rx Monitor       [CAN1 / CANFD3]
│  ├─ Fault Injection      [CAN1 Tx]
│  └─ Test Runner          [CAN1 / CANFD3]
└─ Test Setup / Logging / Reports
```

不同网络使用独立CAPL节点和独立状态表，避免在一个回调中依赖隐含Bus Context。Panel下拉框只选择已有路由，不动态修改CANoe硬件映射。

## 3. 统一视觉规则

- 结构沿用PDU Interactive Generator：上方对象表、下方详情表或Raw区；
- 白、浅灰、深灰承担背景、边界与层级；不使用纯黑大色块；
- 蓝色只表示当前选择、Counter或主操作；
- 黄色表示UB、等待、未配置；
- 红色表示CRC错误、故障或Fail；
- 绿色表示Ready、Valid或Pass；
- 中文作为主标签，协议名、PDU、信号名和状态枚举保留英文；
- 900×540为最小设计尺寸，使用原生像素布局和滚动区，不用Viewbox整体缩放。

## 4. E2E Tx Control（当前v1.4.0）

```text
┌ CAN1 Tx / Bridge Ready ─────────────────────────────────────┐
│ PDU | 发送模式 | UB模式 | 自动保护 | 使能 | 当前执行/停止 | 全停 │
├ PDU table: ID / Cycle / PDU / Frame / DataID / 独立发送状态 ┤
├ State | Counter | CRC | UB | DataID | Physical field map      ┤
├ Application Signals: Physical/Enum | Final Raw | Unit | Valid ┤
└ Final Tx Payload: Byte 0 ... Byte 7 / CRC / Counter / UB       ┘
```

规则：5个PDU分别维护许可、Raw、UB和保护策略。选择只切换编辑对象；执行当前可逐个建立任意组合，停止当前不影响其他ID，停止全部统一恢复安全态。

## 5. E2E Rx Monitor

```text
┌ 网络 [CAN1 ▼ / CANFD3 ▼] | Node Ready | 清统计 | 冻结显示 ┐
├ Group table                                                   ┤
│ Bus | CAN ID | Frame | Group | Cycle | DataID | Last Rx | State│
├ Selected Group status                                        ┤
│ CRC Rx/Calc | Counter | Delta | UB | Age | Timeout | Lost      │
├ Complete Rx Payload / Group field map                         ┤
└ Event history: timestamp | previous → current | reason         ┘
```

CANFD3 `0x032`中的四个Group必须显示为四行并独立维护CRC、Counter和状态；不能按整帧只给一个结果。网络选择器行为：

1. CAN1 Ready时显示10个Group；
2. CANFD3 Ready时显示5个Group；
3. DBC存在但节点未Ready时显示“通道未配置”，表格只展示静态定义，实时列为N/A；
4. 切换网络不清除另一网络的统计状态；“清统计”只作用于当前筛选网络。

建议邮箱：`SRS3_E2E::WpfBridge::RxBridge`，记录采用固定Header加分页窗口；完整历史写CSV，不塞入System Variable数组。

## 6. Fault Injection

```text
┌ CAN1 Tx | PDU | Fault Type | Apply Phase | Duration/Count     ┐
├ Parameter editor: mask / forced counter / wrong DataID / UB   ┤
├ Expected effect: CRC valid? Counter valid? ECU expectation     ┤
├ Armed faults table: target | start | remaining | state         ┤
└ Arm | Apply Once | Apply Continuous | Clear Selected | Clear All│
```

故障类型按处理阶段固定，不允许UI任意组合出语义不清的顺序。Clear All、Measurement Start/Stop和取消发送使能都必须恢复无故障状态。Fault Panel只提交注入参数，实际位修改仍由Tx Controller在Protect前/后执行。

建议邮箱：`SRS3_E2E::WpfBridge::FaultBridge`，与当前 `PanelBridge` 分离，避免改变已经验收的Tx字段索引。

## 7. Test Runner

```text
┌ Scope [CAN1/CANFD3/All] | Suite | Run | Stop | Export         ┐
├ Test cases: ID | Target | Category | Precondition | Result     ┤
├ Current step / expected / actual / timeout                     ┤
├ Evidence: Trace bookmark | Payload | Counter/CRC | ECU response│
└ Summary: Pass / Fail / Blocked / Not Run / report path         ┘
```

Test Runner调用现有Tx命令和Rx状态，不创建 `output()` 第二发送器。测试报告记录Git提交、数据库哈希、Panel DLL版本、CANoe版本、通道映射和每个用例的原始证据。

## 8. 实施顺序

1. 动态验收v1.4.0的5个CAN1 Tx PDU与任意多ID并行控制；
2. 实现CAN1 Rx规则和Rx Monitor Panel；
3. 在同一CFG中添加CANFD3 DBC、CAN FD硬件映射和Rx节点；
4. 验证 `0x032` 64-byte/BRS/5 ms以及四Group独立状态；
5. 在Rx Panel开放CANFD3实时选择；
6. 实现Fault Injection Panel；
7. 实现Test Runner与报告。

CANFD3的前置输入至少包括：所用Vector硬件通道、仲裁位率、数据位率、BRS、采样点/时序来源以及实际接线通道。缺少这些信息时只实现静态定义和UI禁用态，不修改当前CAN1配置。

## 9. 验收要点

- 单工程启动后两个网络的Ready状态独立；
- CAN1 Tx不会因为Rx Panel切到CANFD3而改路由；
- CANFD3未配置时相关实时操作禁用并给出明确原因；
- Rx共享帧按Group独立判定；
- Panel关闭/重开不改变发送许可；Measurement重启恢复安全态；
- 任一动作都可从Panel命令、CAPL状态、Trace和报告四层追溯。
