#!/usr/bin/env python3
"""Static delivery checks for the PDU-IG-owned Tx architecture."""

from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
TX = (ROOT / "CAPL/E2E/E2E_TxController.cin").read_text(encoding="ascii")
FAULT = (ROOT / "CAPL/E2E/E2E_FaultInjection.cin").read_text(encoding="ascii")
RX = (ROOT / "CAPL/E2E/E2E_RxMonitor.cin").read_text(encoding="ascii")
NODE = (ROOT / "Nodes/VectorSimulationNode.can").read_text(encoding="utf-8")
CS = (ROOT / "PanelPlugin/SRS3E2EPanel/E2EConsoleControl.xaml.cs").read_text(encoding="utf-8")
XAML = (ROOT / "PanelPlugin/SRS3E2EPanel/E2EConsoleControl.xaml").read_text(encoding="utf-8")
RXCS = (ROOT / "PanelPlugin/SRS3E2EPanel/E2ERxControl.xaml.cs").read_text(encoding="utf-8")
SYSVAR = (ROOT / "SyaVar/10_SRS3_E2E_Core_SystemVariables.xml").read_text(encoding="utf-8")
CFG = (ROOT / "GEEA3.5_SRS3_CAN1_12.0.cfg").read_text(encoding="utf-8")


def require(text, source, label):
    if text not in source:
        raise AssertionError(f"missing {label}: {text}")


# Hard architectural boundary: no CAPL-created Tx event.
for forbidden in ("E2E_TxBuildAndTrigger", "E2E_PanelSendMode", "gE2E_PendingTrigger"):
    if forbidden in TX:
        raise AssertionError(f"legacy active sender remains: {forbidden}")
if re.search(r"^\s*triggerPDU\s*\(", TX, re.I | re.M):
    raise AssertionError("legacy triggerPDU sender remains")
if re.search(r"^\s*output\s*\(", TX + "\n" + FAULT, re.I | re.M):
    raise AssertionError("raw CAN output() sender is present")

require("dword E2E_TxProcess", TX, "TxPending adapter")
require("E2E_FindTxPdu(pduName)", TX, "five-PDU lookup")
require("E2E_StoreInputPayload(pduIndex, data)", TX, "PDU IG input snapshot")
require("E2E_SetMotorolaRaw(data", TX, "in-place E2E field update")
require("return 0;", TX, "event suppression")
require("return 1;", TX, "same-event pass")
require("applPDUILTxPending", NODE, "CANoe PDU-IL callback")
require("PDU IG is the only producer and scheduler", TX, "source ownership comment")

# Per-PDU configuration and complete telemetry.
for fragment in (
    "gE2E_PanelProtectMode[5]", "gE2E_PanelUbMode[5]", "gE2E_ProtectedFrames[5]",
    "E2E_TX_B_INPUT_RAW = 14", "E2E_TX_B_FINAL_RAW = 22",
    "E2E_TX_B_CRC_CALC = 11", "E2E_TX_B_CRC_TX = 12",
    "E2E_TX_B_PROTECT_BASE = 32", "E2E_TX_B_UB_BASE = 37",
):
    require(fragment, TX, "per-PDU config/telemetry")

# Unified control: no signal editor or send scheduler controls.
require("保护与故障", XAML, "protection tab")
require("接收监控", XAML, "Rx tab")
require('BridgeOffset="96"', XAML, "unified Rx offset")
require("PDU Interactive Generator / CAN1", XAML, "explicit source")
for obsolete in ("执行发送", "停止发送", "改值连续", "应用信号"):
    if obsolete in XAML:
        raise AssertionError(f"obsolete Tx ownership control remains: {obsolete}")
require("UnifiedRxControl.Initialize(value)", CS, "shared symbol binding")
require("public int BridgeOffset", RXCS, "offset-capable Rx control")
require('arrayLength="320"', SYSVAR, "unified bridge size")
require("E2E_RX_UNIFIED_OFFSET = 96", RX, "Rx CAPL unified offset")
require("E2E_RxExportUnifiedBridge", RX, "Rx telemetry mirror")

# Saved config must contain all PDU IG objects. Their trigger/timing remain IG-owned.
targets = (
    "ZCUDZCUDCAN1SignalIPDU01", "ZCUDZCUDCAN1SignalIPDU47",
    "ZCUDZCUDCAN1SignalIPDU37", "ZCUDZCUDCAN1SignalIPDU04",
    "ZCUDZCUDCAN1SignalIPDU10",
)
missing_targets = [name for name in targets if name not in CFG]
if missing_targets:
    raise AssertionError(
        f"saved PDU IG registrations missing ({len(missing_targets)}/{len(targets)}): "
        + ", ".join(missing_targets)
    )

print("SRS3 E2E PDU-IG ownership static verification")
print("  PDU IG registrations:       5")
print("  CAPL active senders:         0")
print("  TxPending in-place adapter:  present")
print("  Per-PDU protect/UB state:    present")
print("  Input/final Raw telemetry:   present")
print("  Unified Tx/Rx WPF control:   present")
print("PASS (static only): Panel/CAPL cannot create or schedule a protected PDU.")
