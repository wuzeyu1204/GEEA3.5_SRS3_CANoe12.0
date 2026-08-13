# SRS3 E2E系统变量文件

四个文件按职责和导入顺序编号，任一变量只允许有一个文件所有者：

| 顺序 | 文件 | 唯一职责 | 主要命名空间/变量 |
|---:|---|---|---|
| 01 | `01_CANoe_IL_SystemVariables.xml` | CANoe PDU-IL基础变量 | `IL`、`IL_CAN1` |
| 10 | `10_SRS3_E2E_Core_SystemVariables.xml` | E2E控制、Tx/Rx状态、故障和测试变量 | `SRS3_E2E::Control/Tx/Rx/Fault/Test` |
| 20 | `20_SRS3_E2E_TxBridge_SystemVariables.xml` | Tx WPF邮箱 | `SRS3_E2E::WpfBridge::PanelBridge` |
| 30 | `30_SRS3_E2E_RxBridge_SystemVariables.xml` | Rx WPF邮箱 | `SRS3_E2E::WpfBridge::RxBridge` |

当前工程的`PanelBridge`已经保存在CANoe Configuration内部，因此当前配置只需引用
01、10、30三个外部文件，不要再导入20，否则会与Configuration中的`PanelBridge`重复。
20保留给新建/干净配置使用。

从旧路径迁移时，在System Variables Configuration底部逐行使用浏览按钮替换路径：

```text
E2E_SystemVariables.xml              -> 01_CANoe_IL_SystemVariables.xml
E2E_Framework_SystemVariables.xml    -> 10_SRS3_E2E_Core_SystemVariables.xml
E2E_Rx_Wpf_Bridge_SystemVariables.xml -> 30_SRS3_E2E_RxBridge_SystemVariables.xml
```

完成后确认变量树中只有一个`PanelBridge`和一个`RxBridge`，再点击Apply并保存CFG。
