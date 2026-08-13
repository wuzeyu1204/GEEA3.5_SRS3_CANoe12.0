# SRS3 E2E Panel

仓库只保留一个可见页面：

```text
SRS3 E2E Test Console - Manual Import.xvp
```

页面只包含 `SRS3.E2E.PanelControl.E2EConsoleControl`，通过“保护与故障”和“接收监控”页签统一承载 Tx 配置/遥测及 Rx 监控。绑定变量为：

```text
SRS3_E2E::WpfBridge::PanelBridge  Int32[320]
```

当前 CFG 已引用该页面。应用信号、触发方式和周期由 PDU Interactive Generator 管理；Panel 不创建或周期发送报文，Rx 页也只清统计状态而不发送报文。
