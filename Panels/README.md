# SRS3 E2E Panel specification

## 实现约束

Panel使用CANoe 12 Panel Designer创建，所有控件静态放置，不在运行时动态创建。
PDU选择后，由CAPL `setControlVisibility()` 切换5个GroupBox；测量状态改变时使用
`enableControl()` 控制编辑权限。

建议Panel名称：`SRS3 E2E Control`。

## 页面

1. `Communication`
2. `E2E Tx`
3. `Fault Injection`
4. `Monitor & Test`

## E2E Tx固定GroupBox

| GroupBox控件名 | PDU | 应用控件数量 |
|---|---|---:|
| `grpVehMtnSt` | `0x040 VehMtnSt` | 1 |
| `grpPreCrashFrontData` | `0x050 PreCrashFrontData` | 4 |
| `grpVehSpdLgt` | `0x076 VehSpdLgt` | 2 |
| `grpVehModMngtGlbSafe1` | `0x0F0 VehModMngtGlbSafe1` | 8 |
| `grpPassAirbLampStsRec` | `0x390 PassAirbLampStsRec` | 1 |

每个应用元素包括Physical/Enum输入、Raw输入、Use Raw开关和Input Valid显示。
具体系统变量路径来自 `SyaVar/E2E_Framework_SystemVariables.xml`。

## 公共控件名

| 控件 | 控件名 | 系统变量/作用 |
|---|---|---|
| PDU选择 | `cmbSelectedPdu` | `SRS3_E2E::Control::SelectedPdu` |
| 覆盖模式 | `cmbOverrideMode` | `SRS3_E2E::Control::OverrideMode` |
| UB模式 | `cmbUpdateBitMode` | `SRS3_E2E::Control::UpdateBitMode` |
| Auto Protect | `chkAutoProtect` | `SRS3_E2E::Control::AutoProtect` |
| Apply | `btnApplyNextCycle` | `SRS3_E2E::Control::ApplyOnce` |
| Restore | `btnRestoreNormal` | `SRS3_E2E::Control::RestoreNormal` |
| 全局使能 | `chkGlobalEnable` | `SRS3_E2E::Control::GlobalEnable` |

## 枚举约定

- OverrideMode：0=Pass Through，1=One Shot，2=Continuous。
- UpdateBitMode：0=Auto，1=Force 0，2=Force 1。
- Fault Type：0=None，1=CRC，2=Freeze Counter，3=Counter 15，
  4=Counter Jump，5=Wrong DataID，6=UB 0，7=Payload Corruption，8=Stop Tx。

## 创建原则

- 状态变量只绑定显示控件，不提供用户写入入口。
- 连续物理量使用Numeric Input；枚举使用ComboBox。
- VehSpdLgtA可在Panel显示km/h，但System Variable保持DBC单位m/s，单位转换由
  Panel输入侧或独立显示变量完成，不能改变CRC使用的Raw值。
- Payload建议使用8个两位十六进制显示框，避免依赖字符串System Variable。
- Panel按钮只产生请求变量；TxPending在下一PDU周期消费并清除请求。

本目录暂不包含手工伪造的 `.xvp`。Panel文件应由CANoe 12 Panel Designer创建，
避免生成不兼容的专有格式。完成后保存为 `Panels/SRS3_E2E_Control.xvp`。

