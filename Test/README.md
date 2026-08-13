# E2E test framework

`E2E_Test_Catalog.csv`定义首批自动化测试范围。正式CAPL Test Module在Tx
Golden Vector和CAN1 Rx Monitor通过后创建。

## Rx Monitor验证

接收侧规则来自ZXDoc生成基线`SRS3_E2E_Rules.json`，工程内冻结为
`Config/E2E_Rx_Rules.json`。CAN1覆盖10个Group，CANFD3覆盖5个Group；共享报文
`0x142`和`0x032`按Group分别维护状态，不能只给整帧一个结论。

运行：

```powershell
python Tools\generate_e2e_rx_artifacts.py
python Tools\verify_e2e_rx.py
```

检查器为15个Group分别验证INITIAL、OK、REPEATED、CRC_ERROR、UB_INACTIVE和
Counter=15，并生成45条可复用帧向量到`Test/RxGoldenVectors.json`。超时策略冻结为
`max(50 ms, 4 × cycle_ms)`。这些是离线算法/规则验收；`Ready-for-CANoe`仍要求把
对应Rx节点加入正确网络后，用Trace和Panel完成动态证据闭环。

## Golden Vector

`GoldenVectors.json`固定了5个Tx PDU、每个PDU 3组向量：DBC默认值/Counter 0、
代表值/Counter 7、有效边界值/Counter 14，共15组。每组均保存应用Raw值、未保护
Payload、逻辑CRC输入、期望CRC和最终Payload。

向量由外部ZXDoc仓库提交
`ed93367f39cba6ab54becc0b547bc360e1ac86c0`的Peer Tx实现生成；工程内校验器不导入
ZXDoc代码，独立重建DBC位打包、逻辑元素序列化和CRC-8/SAE-J1850-ZERO计算。

在仓库根目录运行：

```powershell
python Tools\verify_e2e_golden_vectors.py
```

通过时应同时显示15组向量通过，以及ZXDoc已知答案锚点`0x076 / CRC=0xD4`。
该检查不启动CANoe，也不修改CANoe配置。

每个正式用例必须记录：

- 用例ID和关联需求；
- ECU、数据库、CAPL和CANoe版本；
- 前置条件；
- System Variable刺激；
- 注入阶段和持续时间；
- 预期检测延迟；
- CANoe Checker结果；
- ECU可观测反应；
- BLF、XML/HTML、CSV证据；
- 清理和恢复步骤。

仅检测到总线CRC错误不能证明ECU已经执行安全反应。闭环测试至少需要一种ECU
可观测量，例如返回状态、DTC/DEM、诊断DID或XCP变量。
