# SRS3 E2E Unified Panel Bridge Contract

统一 Panel 只绑定一个变量：

```text
SRS3_E2E::WpfBridge::PanelBridge  Int32[320]
```

所有整数均为有符号 32 位。协议版本固定为 `2`。`64..95` 为保留区；Rx 区以偏移 `96` 开始。

## Tx 保护与故障区：0..63

| 索引 | 方向 | 含义 |
|---:|---|---|
| 0 | 双向 | 协议版本，固定 2 |
| 1 | Panel → Adapter | 当前 PDU，0..4 |
| 3 | Panel → Adapter | 当前 PDU UB 策略：0 Auto、1 Force 0、2 Force 1 |
| 4 | Panel → Adapter | 当前 PDU 保护开关：0 Off、1 On |
| 6 | Panel → Adapter | 配置命令，1 表示应用当前 PDU 的保护/UB 设置 |
| 8 | Adapter → Panel | 当前 PDU 处理状态 |
| 9 | Adapter → Panel | 当前 Counter |
| 10 | Adapter → Panel | 当前 DataID |
| 11 | Adapter → Panel | CRC 计算值 |
| 12 | Adapter → Panel | CRC 实际发送值 |
| 13 | Adapter → Panel | 实际 UB |
| 14..21 | Adapter → Panel | PDU IG 输入 Raw Byte 0..7 |
| 22..29 | Adapter → Panel | 最终发送 Raw Byte 0..7 |
| 30 | Adapter → Panel | 当前 PDU 已保护帧数 |
| 31 | Adapter → Panel | Tx Bridge Active，1 表示可配置 |
| 32..36 | Adapter → Panel | PDU 0..4 独立保护开关回读 |
| 37..41 | Adapter → Panel | PDU 0..4 独立 UB 策略回读 |
| 42..46 | Adapter → Panel | PDU 0..4 最近处理状态 |
| 47..51 | Adapter → Panel | PDU 0..4 已保护帧数 |
| 52 | Panel → Adapter | 故障类型 1..8 |
| 53 | Panel → Adapter | 故障范围：0 下一帧、1 指定帧数、2 持续 |
| 54 | Panel → Adapter | 故障参数 |
| 55 | Panel → Adapter | 指定帧数 |
| 56 | Panel → Adapter | 故障命令：1 应用、2 清当前、3 清全部 |
| 57 | Panel → Adapter | 故障命令 Sequence |
| 58 | Adapter → Panel | 当前 PDU 活动故障类型 |
| 59 | Adapter → Panel | 剩余帧数；负数表示持续 |
| 60 | Adapter → Panel | 已作用次数 |
| 61 | Adapter → Panel | 最近命令结果 |
| 62 | Adapter → Panel | 5 个 PDU 的 Fault Active bit mask |
| 63 | Adapter → Panel | 故障命令 Ack |

## Rx 监控区：96..319

下表的相对索引需加 `96` 得到统一数组绝对索引。

| 相对索引 | 方向 | 含义 |
|---:|---|---|
| 0 | 双向 | Rx 协议版本，固定 2 |
| 1 | Panel → Adapter | 当前显示 Group，0..14 |
| 3 | Adapter → Panel | CAN1 Rx 节点 Ready |
| 4 | Adapter → Panel | CANFD3 Rx 节点 Ready |
| 8 | Adapter → Panel | 当前 Group Counter Delta |
| 19 | Adapter → Panel | 当前 Payload 所属 Group Ack |
| 20..34 | Adapter → Panel | 15 个 Group 判定状态 |
| 35..49 | Adapter → Panel | 15 个 Group 接收帧数 |
| 50..64 | Adapter → Panel | 15 个 Group Age/ms |
| 65..79 | Adapter → Panel | 15 个 Group Counter |
| 80..94 | Adapter → Panel | 15 个 Group CRC Rx |
| 95..109 | Adapter → Panel | 15 个 Group CRC Calc |
| 110..124 | Adapter → Panel | 15 个 Group UB |
| 125..139 | Adapter → Panel | 15 个 Group Valid Count |
| 140..154 | Adapter → Panel | 15 个 Group Error Count |
| 155..218 | Adapter → Panel | 当前 Group 最后接收 Raw Byte 0..63 |
| 219 | Panel → Adapter | Rx 命令：1 清通道、2 清当前 Group |
| 220 | Panel → Adapter | 命令通道：0 CAN1、1 CANFD3 |
| 221 | Panel → Adapter | 命令 Sequence |
| 222 | Adapter → Panel | 命令 Ack |

## 安全语义

- Panel 写桥接数组不等于发送报文。
- Tx 配置/故障只允许由 TxPending 适配器消费 PDU IG 已产生的事件。
- 未绑定、数组长度不足、协议版本不匹配或 Tx Bridge Active 不为 1 时，保护配置与故障按钮保持锁定。
- Rx 清零命令只清统计状态，不发送报文。
