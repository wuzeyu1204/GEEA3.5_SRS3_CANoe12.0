# GEEA3.5 SRS3 CANoe 12 E2E Framework

本仓库以现有CAN1 `AsrPDUIL2`通信工程为基线，逐步增加SRS3 E2E
Profile G通信、Panel控制、故障注入、接收监控和自动化测试。

## 当前状态

- GitHub基线：`origin/main`，恢复时提交 `e4b692a`。
- CANoe版本基线：12.0.75。
- 当前CAN1 PDU-IL和KL15/KL30配置保持不变。
- 当前 `applPDUILTxPending()` 仅观察IPDU37，不修改Payload。
- E2E系统变量已作为外部定义导入CANoe配置；原 `IL`、`IL_CAN1` 命名空间保留，
  `SRS3_E2E::Control::GlobalEnable` 初值为0。
- 新增的 `CAPL/E2E/` 和 `Nodes/E2E_RxMonitor.can` 尚未接入CANoe配置，默认不会
  改变现有通信行为。
- 本次搭建只执行文件级静态检查，没有启动、连接或操作CANoe。

## 目标范围

- CANoe向SRS发送：5个CAN1 PDU、5个E2E Group、16个可编辑应用元素。
- SRS向CANoe监控：CAN1 10个Group、CANFD3 5个Group。
- E2E：项目 `PROFILE_G / P01` 行为。
- CRC：CRC-8/SAE-J1850-ZERO，Counter范围0～14。
- Panel：应用信号编辑、自动保护、一次性/持续覆盖、故障注入和状态显示。
- Test Module：正常、边界、CRC、Counter、UB、超时和恢复测试。

## 目录

```text
CAPL/E2E/                         E2E算法和控制框架，当前未接入
Config/E2E_Rules_Manifest.json    ARXML/DBC交叉核对后的规则清单
Docs/E2E_Engineering_Design.md    完整工程设计
Docs/Implementation_Guide.md      分阶段实施与验证步骤
Nodes/E2E_RxMonitor.can           Rx监控节点安全占位框架
Panels/README.md                  CANoe Panel Designer控件和绑定规范
SyaVar/E2E_Framework_SystemVariables.xml
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
```

通过条件：

```text
Tx PDUs:              5
Tx application items: 16
Rx E2E groups:        15
ARXML profiles:       16
PASS: manifest, DBC and ARXML fields are consistent.
PASS: all payloads, CRC inputs, counters and CRC values match.
```

该命令不会启动CANoe，也不会写入工程文件。

## 实施顺序

1. 冻结规则清单并建立Golden Vector（已完成静态验证）。
2. 在CANoe中导入E2E系统变量（已完成静态验收）。
3. 使用Panel Designer创建固定控件Panel。
4. 以安全Pass Through方式把E2E框架接入TxPending。
5. 先完成IPDU37/VehSpdLgt的正确Protect。
6. 扩展到全部5个Tx PDU。
7. 增加故障注入。
8. 增加CAN1 Rx Monitor和Test Module。
9. 最后增加CANFD3。

每一步的操作、检查点和回退方法见
[实施指南](Docs/Implementation_Guide.md)。在上一步验收前，不进入下一步。

## 关键原则

- 正常周期发送仍由PDU-IL负责，不增加 `on timer + output()` 发送器。
- Panel只操作System Variable，算法全部在CAPL中。
- 先修改应用信号，再使用最终数据计算Counter和CRC。
- Pass Through不修改PDU-IL应用数据，但仍可执行E2E Protect。
- 故障注入和正常保护分层，Restore Normal必须在下一PDU周期恢复。
- ARXML负责E2E逻辑，DBC负责物理位布局；两者必须静态交叉验证。

## 文档

- [完整工程设计](Docs/E2E_Engineering_Design.md)
- [分步骤实施指南](Docs/Implementation_Guide.md)
- [Panel控件与绑定规范](Panels/README.md)
- [测试框架说明](Test/README.md)
