# SRS3 CANoe E2E分步骤实施指南

## 当前快速发送步骤（Panel许可发送，v1.4.0）

1. 关闭 CANoe 和 Panel Designer，双击 `PanelPlugin\Install_SRS3_E2E_PanelPlugin.bat`，确认显示 `[OK] Installed`。这一步只更新 WPF DLL。
2. 打开 `GEEA3.5_SRS3_CAN1_12.0.cfg`，在 Simulation Setup 中编译 `VectorSimulationNode.can`。如有 CAPL 编译错误，先截图完整错误列表，不要启动测量。
3. 编译通过后启动测量。面板应从`已绑定 / 等待数据`进入`控制就绪 / 未使能`。此时先观察Trace至少2秒，5个受控PDU都不得发送。
4. 勾选`发送使能`，等待`发送已使能 / 待命`；继续观察1秒仍不得出现受控PDU。使能只解锁命令，不自动发送。
5. 在面板选择目标PDU：`原值单帧`不覆盖应用值并只发送一次，`改值单帧`使用Panel值发送一次，`改值连续`使用Panel值按数据库周期发送。
6. 在中间信号表修改Physical/Enum；需要精确位模式时勾选`使用Raw`并填写`最终Raw`。所有该PDU应用信号均可编辑。
7. 保持`自动保护`勾选、`UB模式=自动 Auto`，点击`执行当前`。单帧模式只允许当前ID一个物理PDU事件；连续模式的周期必须与PDU表一致。
8. 连续模式点击`停止当前`后不得再出现该PDU，其他已运行ID必须继续发送；`停止全部`才统一停止5个ID。
9. 多ID发送：先让0x076连续发送，再选择0x040并执行连续发送；Trace中两者应分别按20 ms和15 ms运行。切换选择本身不会启动或停止任何ID，PDU表发送状态必须与实际一致。

当前静态验证结果：5 个 Tx PDU、16 个应用元素、15 组 Golden Vector 全部通过。Codex 未启动或操作 CANoe；CAPL 编译和总线发送由用户执行。

## 使用方式

严格按步骤执行。每一步必须满足“通过条件”后再进入下一步。发生异常时执行本步骤
的回退方法，不跨步骤同时修改Panel、CAPL、数据库和测试配置。

Codex只进行文件级修改和静态检查；涉及CANoe GUI、编译、测量和实车总线操作时，
由用户执行并反馈结果。

## Step 0：恢复并确认基线——已完成

已执行：

```powershell
git restore --source=HEAD --staged --worktree .
git fetch origin
git pull --ff-only
```

结果：`origin/main`已恢复，基线提交为`e4b692a`。

## Step 1：冻结静态规则和Golden Vector——已完成

运行：

```powershell
python Tools\verify_e2e_framework.py
python Tools\verify_e2e_golden_vectors.py
```

通过条件：

- 5个Tx PDU；
- 16个Tx应用元素；
- 15个Rx E2E Group；
- Manifest、DBC、ARXML零差异。
- 5个Tx PDU各有默认值、代表值和有效边界值3组向量，共15组；
- 独立校验器重建的Payload、CRC输入、Counter和CRC全部一致；
- ZXDoc已知答案锚点`0x076 / CRC=0xD4`通过。

如果失败，只修正Manifest或生成逻辑，不操作CANoe。

已生成`Test/GoldenVectors.json`。其输入来自ZXDoc提交
`ed93367f39cba6ab54becc0b547bc360e1ac86c0`；文件中同时保存参考代码、规则、锚点
脚本和本工程Manifest的SHA-256，便于复现与审计。

本步骤未启动或修改CANoe。下一步由用户在CANoe中导入System Variable。

## Step 2：导入System Variable——已完成静态验收

用户在CANoe中执行：

1. 备份当前 `.cfg`；
2. 打开System Variables配置；
3. 导入 `SyaVar/10_SRS3_E2E_Core_SystemVariables.xml`；
4. 确认原有 `IL` 和 `IL_CAN1` 命名空间仍存在；
5. 确认新增根命名空间为 `SRS3_E2E`；
6. 保存配置并关闭CANoe。

通过条件：

- 再次打开配置时无缺失System Variable提示；
- `GlobalEnable`初值为0；
- 原CAN1 PDU-IL基线仍能编译。

回退：使用备份CFG；删除导入的 `SRS3_E2E` 命名空间。

静态验收结果：配置同时引用 `SyaVar/01_CANoe_IL_SystemVariables.xml` 和
`SyaVar/10_SRS3_E2E_Core_SystemVariables.xml`；原 `IL`、`IL_CAN1` 命名空间保留；
E2E定义包含5个Tx Group和16个应用信号命名空间，`GlobalEnable`初值为0。

## Step 3：创建Panel静态外壳

用户在CANoe Panel Designer中执行：

1. 新建 `Panels/SRS3_E2E_Control.xvp`；
2. 按 `Panels/README.md` 创建4个页面；
3. 创建5个固定GroupBox和16组应用控件；
4. 绑定System Variable；
5. 先不加入任何故障执行逻辑；
6. 将Panel加入配置。

通过条件：

- 修改Panel输入时System Variable同步变化；
- 不启动测量也不会修改数据库或CAPL；
- 测量运行时原PDU内容保持不变，因为GlobalEnable=0。

## Step 4：以安全Panel许可门接入CAPL框架

在 `Nodes/VectorSimulationNode.can` 的includes中按顺序加入：

```capl
#include "..\CAPL\E2E\Generated\E2E_TxRules_Generated.cin"
#include "..\CAPL\E2E\E2E_ProfileG_Core.cin"
#include "..\CAPL\E2E\E2E_BitCodec.cin"
#include "..\CAPL\E2E\E2E_FaultInjection.cin"
#include "..\CAPL\E2E\E2E_TxController.cin"
```

在 `on start` 调用：

```capl
E2E_TxInitialize();
```

把当前TxPending的IPDU37日志体替换为：

```capl
return E2E_TxProcess(name, aPDULength, data);
```

`SRS3_E2E::Control::GlobalEnable=0` 时，函数只识别5个PDU并保持Payload不变。

通过条件：

- CAPL编译成功；
- 未获许可时5个PDU均被抑制；连续许可时周期与数据库一致；
- Tx Payload与接入前一致；
- 总线上没有重复ID帧。

回退：恢复TxPending旧实现并移除新增include。

## Step 5：完成IPDU37/VehSpdLgt Protect

只实现 `0x076`：

1. 从System Variable读取VehSpdLgtA和QF；
2. Physical或Raw转换；
3. 使用Motorola位编码写入应用字段；
4. 设置UB；
5. Counter按0～14循环；
6. 构造逻辑应用字节：VehSpdLgtA Little Endian、QF；
7. 计算DataID `0x0037`的CRC；
8. 写回物理CRC/Counter字段；
9. 发布Status和Payload。

测试顺序：

- 原值单帧；
- 改值单帧；
- 改值连续；
- Counter 14→0；
- 停止发送；
- Golden Vector。

在Golden Vector一致前，不实现其他PDU。

## Step 6：扩展全部5个Tx PDU

源码、Panel元数据和Golden Vector已覆盖全部5帧；当前动态证据仅覆盖 `0x076` 连续发送。其余4帧按下列顺序完成CANoe验收，不需要再复制一套CAPL发送器。

顺序建议：

1. VehMtnSt；
2. PreCrashFrontData；
3. PassAirbLampStsRec；
4. VehModMngtGlbSafe1。

每增加一个PDU，都执行：

- DBC默认值；
- 物理Min/Nominal/Max；
- Raw值；
- Counter回卷；
- Golden Vector；
- 停止发送；
- 验证非目标bit保持不变。

额外执行多PDU组合测试：逐个启动两个、三个直至全部5个PDU；停止当前只能影响所选ID，停止全部必须清除所有ID。验证每个PDU的Counter独立推进、CRC基于本PDU最终Raw、UB/自动保护不因切换编辑对象而改变。

## Step 7：故障注入

按风险从低到高实施：

1. Corrupt CRC；
2. Freeze Counter；
3. Counter 15；
4. Wrong DataID；
5. Payload Corruption；
6. UB 0；
7. Stop Tx。

每种故障必须定义注入阶段、持续帧数、期望Checker状态、ECU反应和清理步骤。
Stop Tx需先确认CANoe 12 PDU级抑制方式；未确认前不实现。

## Step 8：CAN1 Rx Monitor

1. 补全生成的Rx物理字段规则；
2. 在 `Nodes/E2E_RxMonitor.can` 增加CAN1消息处理；
3. 每个Group独立维护Counter和CRC状态；
4. 定义超时倍数；
5. 添加到Measurement Setup；
6. Panel显示选中对象状态。

重点验证 `0x142` 的两个Group互不影响。

## Step 9：CAPL Test Module

根据 `Test/E2E_Test_Catalog.csv` 创建正式Test Module：

- 正常、边界、负向、时间、恢复和KL15测试；
- 每个用例自动清理Override和Fault；
- 输出XML/HTML；
- 同步记录BLF和CSV；
- 报告写入规则/数据库/Git哈希。

## Step 10：CANFD3

最后增加：

1. CANFD3通道映射；
2. 仲裁和数据相位；
3. BRS配置；
4. 0x032四Group独立检查；
5. 0x03F检查；
6. 64字节DLC和周期压力测试。

CANFD3配置未验证前，不把CAN1通过结论外推到CANFD3。

## 全局提交建议

每一步单独提交：

```text
framework: add static E2E manifest and verifier
panel: import E2E system variables
panel: add SRS3 E2E control layout
tx: connect safe pass-through E2E hook
tx: implement VehSpdLgt profile G protection
tx: cover all SRS3 receive PDUs
fault: add staged E2E injection
rx: add CAN1 E2E monitor
test: add automated E2E test module
rx: add CANFD3 E2E monitor
```

不要把多个阶段合并为一次提交，否则问题发生时无法快速回退到最后一个已验证状态。
## Rx接收测试实施（v1.5.0）

本步骤的源代码与离线向量已经完成；以下操作需要用户在CANoe界面内完成。本轮没有自动
打开或修改`.cfg`，避免破坏当前可发送基线。

### A. CAN1动态接收测试

1. 关闭测量，在System Variables中导入独立接收定义
   `SyaVar/30_SRS3_E2E_RxBridge_SystemVariables.xml`，确认新增
   `SRS3_E2E::WpfBridge::RxBridge`且类型为`Int32[224]`。
2. 在CAN1 Simulation Setup新增Network Node，CAPL程序选择
   `Nodes/E2E_RxMonitor.can`；该节点只有`on message *`监视逻辑，没有`output()`。
3. 打开`Panels/SRS3 E2E Rx WPF Control.xvp`并加入配置；若现有页面中的Symbol丢失，
   将WPF控件重新绑定到`SRS3_E2E::WpfBridge::RxBridge`。
4. Compile All Nodes。出现任何CAPL编译错误时停止，不进行总线测试，保留完整错误文本。
5. 启动测量。Panel选择CAN1，顶部应显示`RX MONITOR READY`，表中应有10个Group。
6. 以`0x076`为首个冒烟对象：收到首个UB=1且CRC有效的帧应显示“初始帧”；下一个
   Counter+1帧显示“正常”；Panel Raw区必须与Trace的8字节完全一致。
7. 使用已有Tx对端或Replay分别输入`Test/RxGoldenVectors.json`中的CRC_ERROR和
   UB_INACTIVE向量，状态应分别显示“CRC错误”和“UB未激活”。不要用本Rx节点发测试帧。
8. 停止对端发送，等待`max(50 ms, 4×周期)`，已收到过帧的Group应进入“超时”。
9. 保存Trace/截图，记录CANoe版本、DBC、Group、DataID、Rx/Calc CRC、Counter/Delta、UB、
   Age和完整Payload。

### B. CANFD3动态接收测试前置

`Nodes/E2E_RxMonitor_CANFD3.can`和5个Group规则已实现，但当前CFG尚未配置CANFD3网络。
在未冻结Vector硬件通道、仲裁/数据位率、BRS和CANFD3 DBC映射前，Panel必须显示
“监听节点未配置/未启动”。完成网络配置后，把该CAPL节点放入CANFD3；`0x032`一次接收
必须独立更新4个Group，`0x03F`更新1个Group，Payload按`canGetDataLength(this)`读取完整
64/2字节。

### C. 离线回归命令

```powershell
python Tools\generate_e2e_rx_artifacts.py
python Tools\verify_e2e_rx.py
python Tools\verify_e2e_golden_vectors.py
python Tools\verify_e2e_framework.py
python Tools\verify_panel_send_gate.py
```

预期Rx输出为`PASS: 15 groups, 45 golden vectors, CAN1/CANFD3 receive-only nodes`。
