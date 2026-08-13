# GEEA3.5 SRS3 CANoe 12 E2E Framework

## Rx接收测试基线（2026-08-13）

- 已实现CAN1 10个、CANFD3 5个E2E Group的只读CAPL Checker；共享CAN ID按Group独立判定。
- 已实现`E2E Rx WPF Control`：CAN1/CANFD3筛选、状态/Counter/Delta/CRC/UB/Age、应用元素Raw和完整0..64字节Payload同屏显示。
- `Nodes/E2E_RxMonitor.can`与`Nodes/E2E_RxMonitor_CANFD3.can`不包含`output()`，不会成为第二发送器。
- `Tools/verify_e2e_rx.py`已通过15个Group、45条接收向量；动态CAN1测试需按
  [Implementation Guide](Docs/Implementation_Guide.md)导入RxBridge并添加节点。当前CFG尚未配置CANFD3，故CANFD3动态项保持Blocked而不是伪造PASS。

## 当前可运行基线（2026-08-12）

- `Nodes/VectorSimulationNode.can` 已在现有 `applPDUILTxPending()` 中接入E2E Tx和发送许可门；5个目标PDU默认返回0不发送，Panel单帧/连续命令才允许返回1。
- 已覆盖 5 个 Tx PDU、16 个应用信号，并按各自 DBC Motorola 位位置写入应用值、UB、4-bit Counter 与 CRC-8。
- WPF 面板采用 PDU Interactive Generator 风格：上方 PDU 表、中间信号表、下方完整 Byte 0..7 Raw Payload；CRC、Counter、UB 保持同屏可见。
- `GlobalEnable`在每次Measurement启动/停止时强制清零。勾选发送使能只解锁Panel命令，不会自动发送PDU。
- 面板插件源码版本为 `1.6.1.0`；支持5个ID独立启动/停止和任意组合并行发送，PDU表直接显示每个ID的发送状态，并提供停止全部；Rx页面支持CAN1/CANFD3共15个Group。
- 已通过规则一致性检查和 15 组 ZXDoc Golden Vector；用户截图已验证 `0x076` 连续发送、Counter与CRC变化，其余4帧及多ID并发仍需CANoe动态验收。

最快发送路径见 [实施指南](Docs/Implementation_Guide.md) 顶部的“当前快速发送步骤”。

本仓库以现有CAN1 `AsrPDUIL2`通信工程为基线，逐步增加SRS3 E2E
Profile G通信、Panel控制、故障注入、接收监控和自动化测试。

## 当前状态

- GitHub基线：`origin/main`，恢复时提交 `e4b692a`。
- CANoe版本基线：12.0.75。
- 当前CAN1 PDU-IL和KL15/KL30配置保持不变。
- 当前 `applPDUILTxPending()` 已调用 `E2E_TxProcess()`，覆盖全部5个受保护Tx PDU。
- E2E系统变量已作为外部定义导入CANoe配置；原 `IL`、`IL_CAN1` 命名空间保留，
  `SRS3_E2E::Control::GlobalEnable` 初值为0。
- `CAPL/E2E/` 的Tx部分已接入 `VectorSimulationNode.can`；`Nodes/E2E_RxMonitor.can`
  仍是后续阶段。`GlobalEnable=0`或无Panel发送许可时，5个目标PDU的Tx回调返回0并阻止上总线。
- 本次搭建只执行文件级静态检查，没有启动、连接或操作CANoe。

## 目标范围

- CANoe向SRS发送：5个CAN1 PDU、5个E2E Group、16个可编辑应用元素。
- SRS向CANoe监控：CAN1 10个Group、CANFD3 5个Group。
- E2E：项目 `PROFILE_G / P01` 行为。
- CRC：CRC-8/SAE-J1850-ZERO，Counter范围0～14。
- Panel：应用信号编辑、发送使能、原值/改值单帧、改值连续、停止发送、自动保护和状态显示。
- Test Module：正常、边界、CRC、Counter、UB、超时和恢复测试。

当前工程的5个E2E Tx PDU全部属于CAN1。CANFD3 DBC中的 `0x032`、`0x03F` 发送节点为SRS3，因而对本工程是Rx监控对象；当前 `.cfg` 尚未装载CANFD3网络。单工程双通道方案是CAN1保留Tx/Rx、CANFD3增加Rx Monitor，Panel的通道选择用于筛选/查看对应网络对象，不能把CAN1 PDU任意改路由到CANFD3。

## 目录

```text
CAPL/E2E/                         已接入的Tx E2E算法、字段编解码和面板桥接
Config/E2E_Rules_Manifest.json    ARXML/DBC交叉核对后的规则清单
Docs/E2E_Engineering_Design.md    完整工程设计
Docs/Implementation_Guide.md      分阶段实施与验证步骤
Nodes/E2E_RxMonitor.can           Rx监控节点安全占位框架
Panels/README.md                  CANoe Panel Designer控件和绑定规范
PanelPlugin/README.md             WPF表格控件、桥接协议和安装说明
PanelPlugin/Install_SRS3_E2E_PanelPlugin.bat
                                   自动定位、构建并复制CANoe 12控件DLL
SyaVar/10_SRS3_E2E_Core_SystemVariables.xml
                                   已导入的E2E系统变量外部定义
Test/E2E_Test_Catalog.csv         测试用例目录
Test/GoldenVectors.json           5个Tx PDU的15组已冻结Golden Vector
Tools/generate_e2e_golden_vectors.py
                                   使用外部ZXDoc参考实现生成向量
Tools/verify_e2e_framework.py     只读规则一致性检查
Tools/verify_e2e_golden_vectors.py
                                   不依赖ZXDoc的独立算法校验
Reports/                          本地运行产物，不提交
```

## 静态验证

在仓库根目录运行：

```powershell
python Tools\verify_e2e_framework.py
python Tools\verify_e2e_golden_vectors.py
python Tools\verify_panel_send_gate.py
```

通过条件：

```text
Tx PDUs:              5
Tx application items: 16
Rx E2E groups:        15
ARXML profiles:       16
PASS: manifest, DBC and ARXML fields are consistent.
PASS: all payloads, CRC inputs, counters and CRC values match.
PASS: Panel controls PDU permission while PDU-IL remains the only sender.
```

该命令不会启动CANoe，也不会写入工程文件。

## 实施顺序

1. 冻结规则清单并建立Golden Vector（已完成静态验证）。
2. 在CANoe中导入E2E系统变量（已完成静态验收）。
3. 使用Panel Designer创建WPF表格Panel（已完成）。
4. 以安全Pass Through方式把E2E框架接入TxPending（已完成源码接入）。
5. 完成IPDU37/VehSpdLgt的正确Protect（已完成静态向量验证）。
6. 扩展到全部5个Tx PDU（已完成静态向量验证，待CANoe总线验收）。
7. 增加故障注入。
8. 增加CAN1 Rx Monitor和Test Module。
9. 最后增加CANFD3。

每一步的操作、检查点和回退方法见
[实施指南](Docs/Implementation_Guide.md)。在上一步验收前，不进入下一步。

## 关键原则

- PDU-IL是唯一底层发送路径；Panel通过TxPending返回0/1控制5个目标PDU是否上总线，不增加`on timer + output()`发送器。
- Panel只操作System Variable，算法全部在CAPL中。
- 先修改应用信号，再使用最终数据计算Counter和CRC。
- 原值单帧不修改PDU-IL应用数据，但只放行一次并可执行E2E Protect。
- 改值连续仅在Panel明确启动后按数据库周期发送；停止发送撤销许可。
- Tx Panel采用每PDU独立运行状态：选择只切换编辑/观察对象，不停止其他ID；执行当前和停止当前只作用于所选PDU，停止全部统一撤销5个PDU许可。
- 每个PDU在启动时分别冻结发送模式、UB模式、自动保护和应用Raw；切换编辑对象不会改变已运行PDU的保护策略。
- ARXML负责E2E逻辑，DBC负责物理位布局；两者必须静态交叉验证。

## 文档

- [完整工程设计](Docs/E2E_Engineering_Design.md)
- [Panel套件与单工程双通道设计](Docs/Panel_Suite_Design.md)
- [分步骤实施指南](Docs/Implementation_Guide.md)
- [Panel控件与绑定规范](Panels/README.md)
- [测试框架说明](Test/README.md)
