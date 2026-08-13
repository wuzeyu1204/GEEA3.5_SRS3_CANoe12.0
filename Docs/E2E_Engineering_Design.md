# SRS3 E2E 工程设计

## 1. 发送所有权

PDU Interactive Generator 是五个 Tx PDU 的唯一事件源。数据库周期、Manual/Event based/Cyclic 设置均属于 PDU IG。VectorSimulationNode 只在 `applPDUILTxPending` 中接收已生成的待发送事件，调用 `E2E_TxProcess` 原地修改 payload 或返回 0 抑制同一事件。

目标 PDU：0x040、0x050、0x076、0x0F0、0x390。DataID、DLC、Motorola 起始位和应用元素序列由 `Config/E2E_Rules_Manifest.json` 与生成规则约束。

## 2. 统一桥接

`PanelBridge Int32[320]` 的协议版本为 2：

- 0..63：Tx 配置、输入/最终 Raw、每 PDU 状态和故障命令；
- 64..95：保留；
- 96..319：15 个 Rx Group 的状态、计数、Age、CRC、UB、Raw 和清零命令。

字段级定义见 `PanelPlugin/Bridge_Contract.md`。CFG 使用内嵌变量，统一 XML 仅作为新工程导入模板。

## 3. Tx 后处理

每个匹配 TxPending 事件依次执行：

1. 保存 PDU IG 输入 Byte 0..7；
2. 消费当前保护/UB 配置及匹配 PDU 的故障命令；
3. 保护关闭时保持 E2E 字段原样；
4. 保护开启时按最终应用 Raw 计算 Counter、DataID/CRC 和 UB；
5. 应用一次、指定帧数或持续故障；
6. 保存最终 Byte 0..7 并发布五 PDU 独立遥测；
7. 返回 1 放行同一事件，或由“抑制发送事件”故障返回 0。

八类故障为：CRC 破坏、Counter 冻结、Counter=15、Counter 跳变、错误 DataID 参与 CRC、UB=0、Payload 破坏、事件抑制。

## 4. Rx 监控

CAN1 Rx 节点接收所有帧，只对规则表中匹配的 Group 做 DLC、UB、CRC、Counter 序列和超时判定。它只写统一桥接偏移 96，不发送报文。CANFD3 源码保留，但当前 CFG 不加载该节点。

## 5. 当前限制

磁盘 CFG 的 PDU IG 只保存了 0x076 对象；另四个对象必须在 CANoe 中补齐。CANoe 编译、运行和总线行为尚未实测。
