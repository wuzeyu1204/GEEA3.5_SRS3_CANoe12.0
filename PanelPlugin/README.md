# SRS3 E2E WPF Panel Control

## 1.6.1 当前实现

界面继续采用 PDU Interactive Generator 的上下表格逻辑：PDU列表、应用信号、完整8-byte Raw Payload依次自上而下排列。顶部使用浅灰工具栏，以灰阶作为主体；蓝色用于主操作/Counter，黄色用于UB或等待提示，红色用于CRC/故障，绿色只表示发送命令已解锁。

1.3.0把发送语义修订为Panel许可模式：5个目标PDU默认被TxPending返回0拦截；原值单帧、改值单帧只放行一次，改值连续才按数据库周期持续放行，停止发送撤销许可。Measurement启动/停止强制清零发送使能。覆盖模式与UB模式增加本地暂存，避免用户选择后被250 ms状态回读弹回。

1.3.1曾采用单活动PDU约束；实际联调需要相关报文组合发送，因此1.4.0改为5个PDU独立许可。选择变化只切换编辑和状态对象，不停止其他ID；`执行当前`、`停止当前`只作用于所选PDU，`停止全部`统一撤销5个许可。Panel在新DataID得到CAPL确认前仍拒绝旧状态和旧Payload回读。

每个PDU在执行时分别保存发送模式、UB模式、自动保护和应用Raw。因此可以让0x076按20 ms连续发送，再选择0x040按15 ms连续发送，两者由各自PDU-IL调度并行存在；随后停止0x040不会影响0x076。

安装仍使用 `Install_SRS3_E2E_PanelPlugin.bat`。为避免 DLL 被占用，执行复制前必须关闭 CANoe 和 Panel Designer；脚本会自行查找 CANoe 12 的 32/64 位 `ControlLibraries` 目录并进行二进制校验。

本目录提供CANoe 12 Panel Designer的WPF自定义控件，当前文件版本为`1.6.1.0`。它基于CANoe 12.0.75随安装包提供的官方ControlPlugin示例：

```text
C:\Users\Public\Documents\Vector\CANoe\Sample Configurations 12.0.75\
Programming\ControlPlugin\Demo
```

## 首版范围

- 一个 WPF 控件覆盖全部 5 个 Tx PDU 和 16 个应用信号；
- PDU、发送模式和UB模式均显示描述文本；
- 连续量支持 Physical 输入与 Raw 输入；
- 枚举量使用下拉文本，同时保留 Raw/保留值测试入口；
- 显示 State、Counter、DataID、CRC 和 Update Bit；
- 在信号表下方常驻显示物理 CAN Payload Byte 0..7，并用颜色标出 CRC、Counter 和 UB 所在字节；
- 未绑定桥接变量时为纯预览；
- 未连接CAPL Adapter或发送使能为0时，执行发送按钮保持禁用；
- WPF本身不直接发送报文或产生周期；CAPL在TxPending中决定拦截/放行并原地保护Payload。

## 构建

关闭 CANoe 和 Panel Designer 后，在工程根目录执行：

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' `
  'PanelPlugin\SRS3E2EPanel.sln' `
  /t:Rebuild /p:Configuration=Release /m
```

输出：

```text
PanelPlugin\SRS3E2EPanel\bin\Release\SRS3_E2E_PanelControl_1_2_0_0.dll
```

工程目标为 .NET Framework 4.0 / AnyCPU，并引用：

```text
C:\Program Files\Vector CANoe 12.0\Exec64\Components\
Vector.PanelControlPlugin\1.2.0.0\Vector.PanelControlPlugin.dll
```

## 安装、首次加载与截图步骤

CANoe 12 Panel Designer 不提供“浏览任意 DLL”的 Toolbox 按钮。Toolbox 左侧的小文件夹图标只用于切换已经加载的控件库。Panel Designer 启动时从以下目录扫描插件：

```text
C:\Program Files\Vector CANoe 12.0\Exec64\ControlLibraries
```

这个机制可由 `PanelDesigner.exe.config` 中的以下配置确认：

```xml
<probing privatePath="ControlLibraries" />
```

推荐安装步骤：

1. 关闭 CANoe 和 Panel Designer。
2. 双击 `PanelPlugin/Install_SRS3_E2E_PanelPlugin.bat`。
3. 接受 Windows 管理员权限提示。脚本会：

   - 从注册表或标准安装目录定位 CANoe 12；
   - 同时兼容 `Exec64` 和 `Exec32`；
   - 检查 CANoe/Panel Designer 是否已经关闭；
   - 用本机 CANoe 的 `Vector.PanelControlPlugin.dll` 编译 Release；
   - 复制并逐字节校验 `SRS3_E2E_PanelControl_1_2_0_0.dll`；
   - 将旧文件名 `SRS3.E2E.PanelControl.dll` 改名为 `.disabled`，不直接删除。

非标准安装目录可先设置环境变量再运行，例如：

```bat
set "CANOE12_ROOT=D:\Vector\CANoe 12.0"
PanelPlugin\Install_SRS3_E2E_PanelPlugin.bat
```

只检查定位和构建、不复制到 Program Files 时，可在命令行执行：

```bat
PanelPlugin\Install_SRS3_E2E_PanelPlugin.bat /check
```

如果脚本无法使用，可手工复制：

   ```text
   源：PanelPlugin\SRS3E2EPanel\bin\Release\SRS3_E2E_PanelControl_1_2_0_0.dll
   目标：C:\Program Files\Vector CANoe 12.0\Exec64\ControlLibraries\SRS3_E2E_PanelControl_1_2_0_0.dll
   ```

4. 重新启动 Panel Designer并打开Tx页面`Panels/SRS3 E2E WPF Control.xvp`或Rx页面`Panels/SRS3 E2E Rx WPF Control.xvp`。
5. Toolbox 左侧应新增一个控件库切换图标；选中后标题应为 `SRS3 E2E Controls`。
6. 将其中的 `E2E Tx WPF Control` 拖到一个新的空白 Panel 页面。
7. Tx页面使用`853 × 520`，Rx页面使用`900 × 640`；不要使用Viewbox整体缩放。
8. 第一轮不要设置 Symbol；此时右上角必须显示 `PREVIEW / NOT BOUND`，两个动作按钮必须禁用。
9. 保存、打开 Panel 预览并截图反馈。

如果 Toolbox 只显示 `Demo Library Name`，说明当前看到的是 Vector 官方 Demo DLL，而不是本工程插件。官方 Demo 的 `WPF Control` 不能替代 `E2E Tx WPF Control`。

### Panel Designer 启动即退出

先从 `ControlLibraries` 移走旧的 `SRS3.E2E.PanelControl.dll`，确认 Panel Designer 可恢复启动，再复制最新 Release DLL。首个 `1.0.0.0` 预览版本曾将只读 `GlobalEnable` 使用为默认双向 WPF 绑定，会触发 `System.InvalidOperationException / PropertyPathWorker.CheckReadOnly`；此问题已在 `1.0.0.1` 修正。`1.0.0.2` 进一步采用 Vector 官方示例的 `_1_2_0_0` 程序集命名与嵌入式 Toolbox 图标，以便 CANoe 12 扫描器识别。`1.0.0.3` 使用等比例 Viewbox 解决裁切，但在小窗口产生非整数缩放和文字模糊；`1.0.0.4` 改为原生 900×540 响应式布局，启用像素对齐与 ClearType；`1.1.0.0` 改为 PDU Generator 风格的原生表格；`1.2.0.0` 将信号、完整 8 字节 Raw Payload、CRC、Counter、UB 与 DataID 改为同屏显示，并按每个 PDU 的真实 DBC start bit 动态标记字节角色。

不要把 WPF 控件与当前五套 GroupBox 叠放在同一页。首轮建议新建空白 Panel 或删除页面上的旧 GroupBox 后再放置一个 WPF 控件。

## 桥接变量

WPF 插件接口的一个控件实例只接收一个 `IExchangeSymbolValue`。因此全部面板字段通过一个 `Int32[64]` 邮箱交换：

```text
SRS3_E2E::WpfBridge::PanelBridge
```

定义文件：`SyaVar/20_SRS3_E2E_TxBridge_SystemVariables.xml`。

| 索引 | 方向 | 含义 |
|---:|---|---|
| 0 | 双向 | Bridge protocol version，固定为 1 |
| 1 | Panel → CAPL | Selected PDU，0..4 |
| 2 | Panel → CAPL | 发送模式：0原值单帧，1改值单帧，2改值连续 |
| 3 | Panel → CAPL | UB：0 Auto，1 Force 0，2 Force 1 |
| 4 | Panel → CAPL | 自动保护：0不保护，1发送前计算E2E |
| 5 | 双向握手 | 发送使能请求值；CAPL处理后回写实际值 |
| 6 | Panel → CAPL | Command：0无，1执行当前，2停止当前，3设置发送使能，4停止全部 |
| 7 | 双向 | Sequence / acknowledgement，后续 CAPL 阶段定义 |
| 8 | CAPL → Panel | Protection state |
| 9 | CAPL → Panel | Counter |
| 10 | CAPL → Panel | DataID |
| 11 | CAPL → Panel | CRC |
| 12 | CAPL → Panel | 实际 UB |
| 13 | 双向 | 当前 PDU 的应用信号数量 |
| 14..21 | Panel → CAPL | 当前 PDU 最多 8 个应用信号的最终 Raw 值 |
| 22..29 | Panel → CAPL | 对应信号的 Use Raw 标志 |
| 30..37 | 双向 | 对应信号的 Input Valid 标志 |
| 38 | CAPL → Panel | BridgeReady；只有等于 1 才允许动作按钮 |
| 39..46 | CAPL → Panel | 当前所选 PDU 的最终 Payload Byte 0..7，值域 0..255 |
| 47..51 | CAPL → Panel | PDU 0..4独立发送状态：0停止、1单帧待发、2连续发送 |
| 52..63 | 保留 | 后续 Fault、Ack 扩展 |

`PanelBridge`只是WPF与CAPL之间的命令/状态邮箱，不发送PDU、不计算CRC、不产生周期。测量启动后CAPL把`BridgeReady`置1并强制发送使能为0。勾选发送使能只提交Command 3并解锁命令，不会产生帧。Command 1只更新当前PDU的一次或连续许可，Command 2只撤销当前许可，Command 4撤销全部许可。连续周期仍来自每个PDU的数据库周期。

当前Tx控件固定表示 `CAN1 Tx`，不提供虚假的CANFD3 Tx切换。静态规则中5个Tx PDU全部属于CAN1；CANFD3的 `0x032/0x03F` 是SRS3发送、本工程接收的E2E对象。后续Rx Monitor控件会提供 `CAN1 / CANFD3` 网络筛选，但必须在同一CFG中完成CANFD3数据库、物理通道和Rx节点配置后才启用CANFD3实时状态。

## Rx Monitor控件（v1.6.1）

Toolbox名称为`E2E Rx WPF Control`，预制页面为
`Panels/SRS3 E2E Rx WPF Control.xvp`。控件绑定：

```text
SRS3_E2E::WpfBridge::RxBridge   Int32[224]
```

接收邮箱可单独通过`SyaVar/30_SRS3_E2E_RxBridge_SystemVariables.xml`导入，
不会重新定义已经验证通过的Tx `PanelBridge`。

Rx控件没有发送使能、执行或停止按钮。它只写索引1选择当前显示的Group，其余实时字段由
CAPL只读发布；关闭、切换或打开Rx Panel均不会改变Tx许可。索引0为协议版本2，索引3/4
分别为CAN1/CANFD3节点Ready，20..154为15个Group的状态/帧数/Age/Counter/CRC/UB/
有效数/异常数，155..218为当前Group所属最后接收帧的完整0..64字节Payload。

CAN1显示10个Group；CANFD3显示5个Group。CANFD3节点未加入配置时仍允许查看静态定义，
但顶部明确显示“监听节点未配置/未启动”，状态不得作为实时测试证据。

## PDU 与信号覆盖

| PDU | 周期 | DataID | 应用信号数 |
|---|---:|---:|---:|
| `0x040 VehMtnSt` | 15 ms | `0x0036` | 1 |
| `0x050 PreCrashFrontData` | 10 ms | `0x0411` | 4 |
| `0x076 VehSpdLgt` | 20 ms | `0x0037` | 2 |
| `0x0F0 VehModMngtGlbSafe1` | 30 ms | `0x0074` | 8 |
| `0x390 PassAirbLampStsRec` | 800 ms | `0x0474` | 1 |

### E2E 字段物理位置

下表的 Byte 是物理 CAN Payload 顺序 0..7；start bit 使用 Vector DBC Motorola 编号。Counter 长度为 4 bit，UB 长度为 1 bit，CRC 长度为 8 bit。

| PDU | CRC | Counter | UB |
|---|---|---|---|
| `0x040 VehMtnSt` | Byte 1 / start 15 | Byte 0 / start 6 | Byte 0 bit 7 |
| `0x050 PreCrashFrontData` | Byte 2 / start 23 | Byte 5 / start 47 | Byte 5 bit 0 |
| `0x076 VehSpdLgt` | Byte 4 / start 39 | Byte 5 / start 47 | Byte 2 bit 7 |
| `0x0F0 VehModMngtGlbSafe1` | Byte 4 / start 39 | Byte 1 / start 15 | Byte 0 bit 7 |
| `0x390 PassAirbLampStsRec` | Byte 6 / start 55 | Byte 5 / start 43 | Byte 7 bit 7 |

信号的 factor、offset、范围、单位和枚举文本来自 `Config/E2E_Rules_Manifest.json` 与 CAN1 DBC。WPF 将 Physical 输入转换成最终 Raw；CRC 必须由后续 CAPL Tx hook 基于最终 Raw 计算，插件自身不伪造 Tx CRC。

## 安全分阶段

1. **已完成：WPF与桥接。** 未绑定Symbol时只预览；绑定后支持PDU选择、编辑保持和状态回读。
2. **已完成：CAPL Tx Adapter。** TxPending接入5个PDU，测量启动后声明BridgeReady。
3. **已完成：静态与Golden Vector。** 5个PDU共15组向量全部匹配ZXDoc。
4. **当前阶段：CANoe编译与发送门验收。** 首先验证启动零发送、使能零发送、单帧一次、连续周期和停止后零发送。
5. **已完成：Rx Monitor。** CAN1/CANFD3共15个Group已接入只读监控和黄金向量验证。
6. **后续阶段：故障注入和Test Module。** 当前故障函数仍为安全透传占位。

总线验收结束后应立即取消发送使能；Measurement Stop也会在CAPL中强制清零。
