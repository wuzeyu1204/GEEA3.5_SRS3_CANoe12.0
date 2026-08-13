#!/usr/bin/env python3
"""Offline/static verification for the WPF Panel delivery only.

This script deliberately does not open CANoe and does not inspect or modify CFG,
PDU IG, databases, CAPL nodes, or installed ControlLibraries.
"""

from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = ROOT / "PanelPlugin" / "SRS3E2EPanel"
XAML = (PLUGIN / "E2EConsoleControl.xaml").read_text(encoding="utf-8")
CS = (PLUGIN / "E2EConsoleControl.xaml.cs").read_text(encoding="utf-8")
RX_XAML = (PLUGIN / "E2ERxControl.xaml").read_text(encoding="utf-8")
RX_CS = (PLUGIN / "E2ERxControl.xaml.cs").read_text(encoding="utf-8")
PROJECT = (PLUGIN / "SRS3E2EPanel.csproj").read_text(encoding="utf-8")
INSTALL_BAT = (ROOT / "PanelPlugin" / "Install_SRS3_E2E_PanelPlugin.bat").read_text(encoding="utf-8")
PANEL = ROOT / "Panels" / "SRS3 E2E Test Console - Manual Import.xvp"
SYSVAR = ROOT / "SyaVar" / "10_SRS3_E2E_Core_SystemVariables.xml"


def require(fragment: str, source: str, label: str) -> None:
    if fragment not in source:
        raise AssertionError(f"missing {label}: {fragment}")


# Both markup files and the manual-import XVP must be well-formed.
ET.parse(PLUGIN / "E2EConsoleControl.xaml")
ET.parse(PLUGIN / "E2ERxControl.xaml")
ET.parse(PANEL)

# Architecture and visual structure.
for fragment, label in (
    ('x:Name="ConsoleTabs"', "unified tab control"),
    ("保护与故障", "protection/fault tab"),
    ("接收监控", "Rx monitor tab"),
    ("PDU Interactive Generator / CAN1", "explicit PDU IG source"),
    ("PDU IG输入 Raw", "input Raw comparison"),
    ("最终发送 Raw", "final Raw comparison"),
    ('BridgeOffset="96"', "Rx unified offset"),
    ('EmbeddedMode="True"', "embedded Rx layout"),
):
    require(fragment, XAML, label)

for forbidden in ("执行发送", "停止发送", "改值连续", "应用信号编辑"):
    if forbidden in XAML:
        raise AssertionError(f"obsolete sender/editor control remains: {forbidden}")

for color, label in (("#2E6690", "Counter blue"), ("#B33A32", "CRC red"), ("#8A6814", "UB yellow"), ("#247A45", "normal green")):
    require(color, XAML + CS, label)

for fragment, label in (
    ('Property="HorizontalContentAlignment" Value="Center"', "horizontal content centering"),
    ('Property="VerticalContentAlignment" Value="Center"', "vertical content centering"),
    ('Property="TextAlignment" Value="Center"', "TextBox text centering"),
):
    require(fragment, XAML, label)

# Safe command gating and stale telemetry reset.
for fragment in (
    "public bool CanConfigure",
    "public bool IsConfigurationLocked",
    "if (!bridgeReady",
    "ResetSelectedTelemetry();",
    "values.Length >= UnifiedBridgeLength",
    "UnifiedRxControl.Initialize(value)",
):
    require(fragment, CS, "safe bridge behavior")

require("public bool EmbeddedMode", RX_CS, "embedded Rx mode")
require("Visibility.Collapsed", RX_CS, "embedded header/footer removal")
require('x:Name="DetailTabs"', RX_XAML, "Rx lower workspace tabs")

# Exact bridge indexes used by the UI.
for name, index in {
    "SelectedPduIndex": 1,
    "InputRawBase": 14,
    "FinalRawBase": 22,
    "ProtectBase": 32,
    "UbBase": 37,
    "FaultTypeIndex": 52,
    "FaultAckIndex": 63,
}.items():
    require(f"private const int {name} = {index};", CS, f"bridge field {name}")

# The template is data only; validate length/count without referencing CFG.
sysroot = ET.parse(SYSVAR).getroot()
variable = next(node for node in sysroot.iter("variable") if node.attrib.get("name") == "PanelBridge")
assert variable.attrib["arrayLength"] == "320"
assert len(variable.attrib["startValue"].split(";")) == 320

require("<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>", PROJECT, ".NET 4.7.2 target")
require('Compile Include="E2EConsoleControl.xaml.cs"', PROJECT, "single exported console source")
require('Page Include="E2EConsoleControl.xaml"', PROJECT, "single exported console XAML")
if "E2ETxControl.xaml" in PROJECT:
    raise AssertionError("legacy Tx panel control is still compiled")
if "IPluginPanelControl" in RX_CS:
    raise AssertionError("embedded Rx view is still exported as a standalone panel control")
require("E2EConsoleControl", PANEL.read_text(encoding="utf-8"), "manual XVP console control")
require("This script NEVER starts CANoe", INSTALL_BAT, "one-click script boundary")
require("call :CheckProcesses", INSTALL_BAT, "CANoe process gate")
require("call :CleanWorkspace", INSTALL_BAT, "legacy cache cleanup")
require("call :BuildAndPackage", INSTALL_BAT, "offline build step")
require("call :StaticAudit", INSTALL_BAT, "static audit step")
require("call :InstallPackage", INSTALL_BAT, "verified install step")
require("01_CANoe_IL_SystemVariables.xml", INSTALL_BAT, "canonical IL system-variable package")
require("10_SRS3_E2E_Core_SystemVariables.xml", INSTALL_BAT, "canonical E2E system-variable package")
if "20_SRS3_E2E_UnifiedBridge_SystemVariables.xml" in INSTALL_BAT:
    raise AssertionError("installer still packages the obsolete standalone bridge")
if re.search(r"start\s+.*CANoe(?:64|32)?\.exe", INSTALL_BAT, re.I):
    raise AssertionError("one-click script attempts to start CANoe")
for forbidden in (".cfg", "VectorSimulationNode.can", "E2E_RxMonitor.can"):
    if re.search(rf"(?:copy|del|move|ren)\s+[^\r\n]*{re.escape(forbidden)}", INSTALL_BAT, re.I):
        raise AssertionError(f"one-click script modifies CANoe integration file: {forbidden}")

build_info = ROOT / "PanelPlugin" / "dist" / "SRS3_E2E_Panel" / "BUILD_INFO.txt"
if build_info.exists():
    info = build_info.read_text(encoding="utf-8")
    match = re.search(r"^SHA256=([0-9A-F]{64})$", info, re.M)
    if not match:
        raise AssertionError("packaged DLL SHA256 is missing or malformed")

print("SRS3 E2E Panel delivery verification")
print("  unified protection/Rx UI:   present")
print("  PDU IG ownership text:      present")
print("  send/editor controls:       absent")
print("  safe command gating:        present")
print("  canonical core SysVar:      Int32[320]")
print("  offline package script:     present")
print("PASS (offline/static only; CANoe integration and bus behavior not tested)")
