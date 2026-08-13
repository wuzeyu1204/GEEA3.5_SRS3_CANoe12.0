# SRS3 E2E 系统变量

当前工程只加载两份外部系统变量文件：

| 顺序 | 文件 | 唯一职责 |
|---:|---|---|
| 01 | `01_CANoe_IL_SystemVariables.xml` | CANoe PDU-IL 基础变量（`IL`、`IL_CAN1`） |
| 10 | `10_SRS3_E2E_Core_SystemVariables.xml` | E2E 控制、Tx/Rx 状态、故障、测试变量，以及唯一的统一 WPF 桥接变量 |

统一桥接变量定义在 `10_SRS3_E2E_Core_SystemVariables.xml` 中：

```text
SRS3_E2E::WpfBridge::PanelBridge  Int32[320]
协议版本：2
Tx：0..63
保留：64..95
Rx：96..319
```

不要再加载旧的 `20_SRS3_E2E_TxBridge_SystemVariables.xml`、`30_SRS3_E2E_RxBridge_SystemVariables.xml` 或 `20_SRS3_E2E_UnifiedBridge_SystemVariables.xml`。这些拆分/独立桥接文件已移除，否则会与 `10` 中的唯一 `PanelBridge[320]` 冲突。

若 CFG 里仍有内嵌的 `PanelBridge[64]`，必须先在 CANoe 的 System Variables 配置中删除该内嵌变量及旧 `30` 引用，再重新加载 `01`、`10`。文件修改不会自动替换 CANoe 当前已加载的定义。
