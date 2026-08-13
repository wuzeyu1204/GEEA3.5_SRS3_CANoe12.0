# SRS3 E2E Panels

当前工程只保留两张CANoe 12 WPF页面：

| 页面 | 控件 | 绑定变量 | 用途 |
|---|---|---|---|
| `SRS3 E2E WPF Control.xvp` | `E2E Tx WPF Control` | `SRS3_E2E::WpfBridge::PanelBridge` | CAN1五个受保护PDU的信号编辑、单帧/连续许可与E2E状态 |
| `SRS3 E2E Rx WPF Control.xvp` | `E2E Rx WPF Control` | `SRS3_E2E::WpfBridge::RxBridge` | CAN1/CANFD3十五个E2E Group的只读接收监控 |

旧的原生GroupBox页面及`E2E_PanelController.cin`已经删除。当前WPF控件通过数组邮箱与CAPL通信，不使用`setControlVisibility()`或`enableControl()`操纵Panel控件。

## 运行约束

- Tx Panel只提交控制命令；周期仍来自PDU-IL，CAPL在TxPending中决定拦截或放行。
- Rx Panel只写当前显示Group的索引，不发送报文、不改变Tx许可。
- 安装或更新控件前必须关闭CANoe和Vector Panel Designer，然后运行`PanelPlugin/Install_SRS3_E2E_PanelPlugin.bat`。
- 当前CFG内置`PanelBridge`，外部系统变量只引用`01_CANoe_IL`、`10_SRS3_E2E_Core`和`30_SRS3_E2E_RxBridge`。

