# E2E Tx Panel发送控制与验收

## 1. 修订后的发送架构

WPF Panel不直接发送CAN Frame，也不产生定时器。`PanelBridge`只是一个`Int32[64]`命令/状态邮箱：Panel写入PDU、发送模式、信号值和命令，CAPL读取后回写状态、Counter、CRC、UB和最终Payload。

5个受控PDU的底层发送仍走原AUTOSAR PDU-IL：

1. PDU-IL按数据库周期产生`applPDUILTxPending()`机会；
2. 未获得Panel许可时CAPL返回0，PDU不上总线；
3. 单帧许可只让下一次所选PDU的TxPending返回1；
4. 连续许可让后续TxPending持续返回1，因此周期仍由数据库的10/15/20/30/800 ms决定；
5. 放行前CAPL写入最终应用Raw，并按需要计算UB、Counter和CRC。

依据为本机CANoe 12.0官方帮助：`applPDUILTxPending()`在PDU-IL发送前调用，允许修改数据；返回0阻止发送，返回1执行发送。工程不增加`on timer + output()`第二发送源。

## 2. 控件语义

| 控件 | 桥接字段 | 行为 |
|---|---:|---|
| PDU | `[1]` | 选择当前配置、发送和状态观察对象；选择本身不发送 |
| 原值单帧 Pass Through | `[2]=0` | 不覆盖业务信号，只放行下一次所选PDU调度 |
| 改值单帧 One Shot | `[2]=1` | 使用Panel最终Raw覆盖业务信号，只放行一次 |
| 改值连续 Continuous | `[2]=2` | 使用Panel最终Raw覆盖业务信号，按数据库周期持续放行 |
| UB模式 | `[3]` | 0自动，1强制0，2强制1；执行发送时生效 |
| 自动保护 | `[4]` | 1时发送前计算UB/Counter/CRC；0时不执行本控制器保护，用于负向测试 |
| 发送使能 | `[5]` + Command 3 | 安全锁；勾选只解锁发送命令，不自动发帧 |
| 执行当前 | Command 1 | 提交当前配置并授予该PDU单帧或连续许可，不影响其他ID |
| 停止当前 | Command 2 | 撤销所选PDU许可并清除其业务信号覆盖 |
| 停止全部 | Command 4 | 撤销全部5个PDU许可和覆盖 |

Measurement启动和停止都会强制执行：发送使能=0、Command=0、全部PDU许可=停止、全部覆盖清除。避免上次测量的使能状态被保留。

## 3. 当前验证边界

| 项目 | 状态 |
|---|---|
| PDU/模式/UB/自动保护/使能/执行/停止到PanelBridge的字段映射 | 离线假接口通过 |
| 模式和UB下拉选择不再被250 ms回读弹回 | 离线假接口覆盖 |
| PDU切换拒绝旧DataID/Raw/Payload，同时保留其他PDU许可 | v1.4.0静态检查和WPF假接口通过；待CANoe复测 |
| 5个PDU、16个应用元素、DBC/ARXML一致性 | 静态验证通过 |
| 15组Golden Vector和ZXDoc锚点 | 独立验证通过 |
| 0x076连续发送、Counter和CRC变化 | 用户截图已提供动态证据 |
| Measurement启动零发送、单帧只一帧、停止后零发送、其余4帧 | 仍需用户在CANoe Trace动态验收 |

## 4. CANoe动态验收顺序

1. 启动Measurement，不操作Panel，按5个CAN ID过滤Trace，观察至少2秒：`0x040/0x050/0x076/0x0F0/0x390`均不得出现Tx。
2. 面板应显示`控制就绪 / 未使能`；发送使能未勾选，执行当前禁用。
3. 勾选发送使能，等待`发送已使能 / 待命`；继续观察1秒，仍不得出现这5个Tx。
4. 选择`0x076`和`原值单帧`，点击执行当前：最多等待20 ms，只能出现1个物理发送事件。Trace中的`CAN Frame Tx`与`AUTOSAR PDU Tx`若时间和Payload相同，是同一事件的两个显示层级。
5. 选择`改值单帧`，设置`VehSpdLgtA=10 m/s`、QF=`Accurate Data`，执行当前：只出现1帧，最终Raw约为2558，Counter只推进一次，CRC与该帧最终Raw一致。
6. 等待100 ms，不得继续出现0x076。
7. 选择`改值连续`并执行当前：连续观察至少5帧，间隔应为20 ms并允许正常调度抖动；Counter按0..14循环，CRC随Counter变化。
8. 点击停止当前：从命令被CAPL确认后，0x076不得再出现。
9. 重新让0x076连续发送，然后选择0x040并执行连续发送：0x076继续约20 ms，0x040约15 ms；DataID/信号详情显示当前选择，不串用旧Raw/Payload。
10. 在0x076和0x040同时运行时停止当前0x040：仅0x040停止，0x076继续。点击停止全部后两者均停止。
11. 对其余3个PDU重复单ID和组合测试；0x390单帧最坏等待800 ms。
12. 取消发送使能，确认所有PDU许可被清除；停止并重新启动Measurement，再次验证默认零发送。

## 5. 通过条件

- 未执行发送时5个目标PDU为零帧；
- 勾选发送使能本身不产生帧；
- 单帧模式每次命令恰好一个物理PDU发送；
- 连续模式周期等于数据库周期，不存在第二发送源；
- 停止发送和取消使能都能阻止后续帧；
- 任意组合PDU可以并行发送，切换选择不改变其他ID许可；
- 停止当前只影响所选ID，停止全部和取消使能清除所有ID；
- 发送Payload、应用Raw、UB、Counter和CRC与面板一致。

## 6. CAN1/CANFD3边界

当前Tx Panel只操作CAN1的5个受控PDU。CANFD3 DBC中的0x032和0x03F由SRS3发送，对本工程属于Rx Monitor范围；当前CFG没有CANFD3数据库/物理通道/接收节点。因此本阶段不在Tx工具栏增加CANFD3发送选项。单工程双通道将在Rx Monitor Panel中提供CAN1/CANFD3筛选，并以对应Rx节点的Ready状态作为启用条件。
