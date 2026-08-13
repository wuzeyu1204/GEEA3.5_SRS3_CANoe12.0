# SRS3 E2E Panel 设计

## 唯一控件

`SRS3.E2E.PanelControl.E2EConsoleControl` 是唯一导出控件，XVP 为 `Panels/SRS3 E2E Test Console - Manual Import.xvp`。控件包含“保护与故障”和“接收监控”两个页签；Rx 子控件只作为嵌入视图。

## 绑定与安全

控件只绑定 `SRS3_E2E::WpfBridge::PanelBridge Int32[320]`。未绑定、长度不足 320、协议版本不为 2 或 Tx Bridge Active 不为 1 时，配置与故障按钮锁定并清空旧遥测。

Tx 页只允许：

- 选择五个 PDU；
- 设置保护开关和 UB 策略；
- arm/clear 八类故障；
- 查看输入/最终 Raw、Counter、CRC、UB、DataID 和计数。

Rx 页只允许选择网络/Group、冻结显示和清空统计。网络选择不改变总线路由。

## 所有权边界

应用信号和触发方式由 PDU Interactive Generator 管理。Panel 写桥接数组只产生配置命令，不会产生发送事件。

## 交付

唯一脚本 `PanelPlugin/Install_SRS3_E2E_PanelPlugin.bat` 支持 `/check` 离线构建、打包和审计。动态行为仍需 CANoe 12 验收。
