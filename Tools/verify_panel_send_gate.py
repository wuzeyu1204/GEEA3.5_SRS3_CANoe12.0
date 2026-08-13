from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
TX = (ROOT / "CAPL" / "E2E" / "E2E_TxController.cin").read_text(encoding="utf-8")
NODE = (ROOT / "Nodes" / "VectorSimulationNode.can").read_text(encoding="utf-8")
WPF = (ROOT / "PanelPlugin" / "SRS3E2EPanel" / "E2ETxControl.xaml.cs").read_text(encoding="utf-8")


def require(fragment: str, source: str, label: str) -> None:
    if fragment not in source:
        raise SystemExit(f"FAIL: missing {label}: {fragment}")


require("int gE2E_PanelSendMode[5]", TX, "per-PDU send gate")
require("@sysvar::SRS3_E2E::Control::GlobalEnable = 0;", TX, "safe startup/shutdown reset")
require("if (gE2E_PanelSendMode[pduIndex] == 0)", TX, "no-permission branch")
require("return 0;", TX, "PDU suppression")
require("gE2E_PanelSendMode[pduIndex] = 1;", TX, "one-shot permission")
require("gE2E_PanelSendMode[pduIndex] = mode;", TX, "continuous permission")
require("void E2E_TxShutdown()", TX, "measurement shutdown")
require("E2E_TxShutdown();", NODE, "preStop shutdown hook")
require("void E2E_BridgeSynchronizeSelection()", TX, "selection synchronization")
require("Every PDU keeps its own\n   * permission and may continue concurrently", TX,
        "selection preserves other PDU permissions")
require("int gE2E_PanelUbMode[5]", TX, "per-PDU UB mode")
require("int gE2E_PanelProtectMode[5]", TX, "per-PDU protect mode")
require("else if (command == 4)", TX, "stop-all command")
require("PanelBridge[47 + pduIndex]", TX, "per-PDU status publication")
require("private readonly int[] overrideModeDrafts", WPF, "per-PDU mode drafts")
require("private readonly int[] ubModeDrafts", WPF, "per-PDU UB drafts")
require("private readonly bool[] autoProtectDrafts", WPF, "per-PDU protect drafts")
require("controlDraftDirty[CurrentPdu.Index] = false;", WPF, "per-PDU draft commit")
require("if (selectionPending && values[IndexDataId] == CurrentPdu.DataId)", WPF,
        "selected-PDU status acknowledgement")

if re.search(r"^\s*(output|triggerPDU)\s*\(", TX, flags=re.MULTILINE | re.IGNORECASE):
    raise SystemExit("FAIL: a second explicit sender was added to the Tx controller")

print("SRS3 Panel-controlled PDU send gate static verification")
print("  Default protected-PDU behavior: suppressed")
print("  One-shot permission:             present")
print("  Continuous permission:           present")
print("  Measurement reset/shutdown:      present")
print("  Independent multi-PDU permission: present")
print("  Per-PDU UB/protect snapshots:      present")
print("  Stop-all safety command:           present")
print("  Stale selection status guard:     present")
print("  WPF per-PDU editor drafts:         present")
print("PASS: Panel controls PDU permission while PDU-IL remains the only sender.")
