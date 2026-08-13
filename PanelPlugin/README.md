# SRS3 E2E WPF Panel 2.2.0

本目录交付 CANoe 12 WPF Panel 的源码、手动导入模板和唯一的一键脚本。Panel 定位为“E2E 保护与故障控制台”，统一包含：

- 保护与故障：5 个 CAN1 PDU 独立配置 E2E/UB，查看 Counter、CRC、DataID、Raw 对比并配置故障；
- 接收监控：切换 CAN1/CANFD3，查看 Group 判定、Counter/Delta、CRC、UB、Age、应用元素及完整 Raw；
- Panel 不编辑应用信号、不创建 PDU、不维护发送周期；PDU IG 是唯一发送源。

## 唯一操作入口

先关闭 CANoe 和 Panel Designer，然后双击：

```bat
PanelPlugin\Install_SRS3_E2E_PanelPlugin.bat
```

脚本会依次完成：

1. 清理 `work`、`bin`、`obj` 和旧预览输出；
2. 自动定位 CANoe 12 Panel API 和 .NET Framework MSBuild；
3. 编译 Release，并重新生成干净的 `PanelPlugin\dist\SRS3_E2E_Panel`；
4. 打包唯一 XVP、规范的 01/10 系统变量文件，审核 `PanelBridge Int32[320]` 和 DLL 哈希；
5. 请求管理员权限，删除旧版/备份 DLL，仅安装一个经过字节校验的 DLL。

若 CANoe 安装在非标准目录，先设置环境变量：

```bat
set "CANOE12_ROOT=D:\Vector\CANoe 12.0"
```

脚本不会启动 CANoe，也不会修改 `.cfg`、Simulation Setup、CAPL、PDU IG、数据库或系统变量引用。

## 用户在 CANoe 中完成

1. 只加载 `SyaVar\01_CANoe_IL_SystemVariables.xml` 与 `SyaVar\10_SRS3_E2E_Core_SystemVariables.xml`；不要加载旧 20/30 或独立桥接模板；
2. 导入 `Panels\SRS3 E2E Test Console - Manual Import.xvp`；
3. 将控件 Symbol 绑定到 `SRS3_E2E::WpfBridge::PanelBridge`；
4. 编译 `VectorSimulationNode` 与 `E2E_RxMonitor_CAN1`，再进行总线验证。

桥接字段见 [Bridge_Contract.md](Bridge_Contract.md)，当前唯一有效架构和剩余 CANoe 操作见仓库根目录 `CODEX_HANDOFF.md`。脚本与离线审核结果不能替代 CANoe 编译和实车/台架总线测试。

## 兼容性

- CANoe 12.0.75 / PanelControlPlugin API 1.2.0.0
- .NET Framework 4.7.2 / AnyCPU
- DLL：`SRS3_E2E_PanelControl_1_2_0_0.dll`
- AssemblyVersion `1.2.0.0`，FileVersion `2.2.0.0`
