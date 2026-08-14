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
MODELS = (PLUGIN / "Models" / "PanelModels.cs").read_text(encoding="utf-8")
RX_XAML = (PLUGIN / "E2ERxControl.xaml").read_text(encoding="utf-8")
RX_CS = (PLUGIN / "E2ERxControl.xaml.cs").read_text(encoding="utf-8")
PROJECT = (PLUGIN / "SRS3E2EPanel.csproj").read_text(encoding="utf-8")
INSTALL_BAT = (ROOT / "PanelPlugin" / "Install_SRS3_E2E_PanelPlugin.bat").read_text(encoding="utf-8")
PANEL = ROOT / "Panels" / "SRS3 E2E Test Console - Manual Import.xvp"
SYSVAR = ROOT / "SyaVar" / "10_SRS3_E2E_Core_SystemVariables.xml"


def require(fragment: str, source: str, label: str) -> None:
    if fragment not in source:
        raise AssertionError(f"missing {label}: {fragment}")


def collect_sysvar_definitions(path: Path):
    definitions = []

    def visit(node, namespace):
        for child in node:
            tag = child.tag.rsplit("}", 1)[-1]
            if tag == "namespace":
                name = child.attrib.get("name", "")
                visit(child, namespace + ([name] if name else []))
            elif tag == "variable":
                definitions.append("::".join(namespace + [child.attrib["name"]]))

    visit(ET.parse(path).getroot(), [])
    return definitions


# Both markup files and the manual-import XVP must be well-formed.
ET.parse(PLUGIN / "E2EConsoleControl.xaml")
ET.parse(PLUGIN / "E2ERxControl.xaml")
ET.parse(PANEL)

# Architecture and visual structure.
for fragment, label in (
    ('x:Name="ConsoleTabs"', "unified tab control"),
    ("保护与故障", "protection/fault tab"),
    ("接收监控", "Rx monitor tab"),
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
    "ApplyConfigurationButton_Click",
    "UpdateConfigurationAcknowledgement",
    "pdu.RequestedProtectionEnabled",
    "pdu.RequestedUbMode",
    "pdu.ConfigurationPending = true",
    "if (!bridgeReady",
    "ResetSelectedTelemetry();",
    "values.Length >= UnifiedBridgeLength",
    "UnifiedRxControl.Initialize(value)",
):
    require(fragment, CS, "safe bridge behavior")

for fragment in (
    "public bool RequestedProtectionEnabled",
    "public int RequestedUbMode",
    "public bool ConfigurationPending",
    "public void AcceptReadbackConfiguration()",
):
    require(fragment, MODELS, "row-local staged configuration")

require('Header="配置"', XAML, "row-local configuration column")
require('Content="{Binding ConfigurationActionText}"', XAML, "row-local apply button")
require('IsChecked="{Binding RequestedProtectionEnabled', XAML, "editable row E2E checkbox")
require('SelectedValue="{Binding RequestedUbMode', XAML, "editable row UB selector")
require('IsReadOnly="False"', XAML, "editable configuration table")
for annotation in (
    "Profile G / P01 · 保护与故障 + 接收监控 · Panel不创建报文",
    "仅处理 PDU IG 后续 TxPending 事件",
    "先选行，再编辑并应用",
    "同一PDU IG事件经E2E/故障处理后",
    "UI v1.8.0 · Profile G/P01 · 只监听，不发送报文",
    "每个 E2E Group 独立判定",
    "最后接收 Raw 解码；CRC 输入元素按名称排序",
    "Panel本地事件（最多100条；用于定位，不替代CANoe Trace时间戳）",
    "FooterText",
):
    if annotation in XAML + RX_XAML:
        raise AssertionError(f"UI annotation remains: {annotation}")
if "Pdu_PropertyChanged" in CS or "WriteProtectionConfiguration" in CS:
    raise AssertionError("legacy auto-apply configuration path remains")

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

# Compare actual fully-qualified definitions, not CFG text references. Include
# the two delivery files and any historical SysVarDef.xml that may reappear.
sysvar_files = sorted((ROOT / "SyaVar").glob("*.xml"))
sysvar_files += sorted(path for path in ROOT.rglob("SysVarDef.xml") if path not in sysvar_files)
assert ROOT / "SyaVar" / "01_CANoe_IL_SystemVariables.xml" in sysvar_files
assert SYSVAR in sysvar_files
owners = {}
for path in sysvar_files:
    for qualified_name in collect_sysvar_definitions(path):
        owners.setdefault(qualified_name, []).append(path)
duplicates = {name: paths for name, paths in owners.items() if len(paths) > 1}
if duplicates:
    details = "; ".join(
        f"{name}: {', '.join(str(path.relative_to(ROOT)) for path in paths)}"
        for name, paths in sorted(duplicates.items())
    )
    raise AssertionError(f"duplicate system-variable definitions: {details}")
panel_bridge_owners = owners.get("SRS3_E2E::WpfBridge::PanelBridge", [])
assert panel_bridge_owners == [SYSVAR], f"unexpected PanelBridge definitions: {panel_bridge_owners}"

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

# Authored source and delivery documents must be portable. Runtime path discovery
# is allowed, but no drive-qualified path may be checked into scripts or metadata.
portable_extensions = {
    ".bat", ".can", ".cin", ".cs", ".csproj", ".json", ".md",
    ".ps1", ".py", ".sln", ".txt", ".xaml", ".xml", ".xvp",
}
generated_parts = {".git", "bin", "obj", "dist", "Log", "work"}
for path in ROOT.rglob("*"):
    if not path.is_file() or path.suffix.lower() not in portable_extensions:
        continue
    if any(part in generated_parts for part in path.parts):
        continue
    source = path.read_text(encoding="utf-8", errors="ignore")
    if re.search(r"(?i)(?<![%A-Z0-9_])[A-Z]:\\", source):
        raise AssertionError(f"drive-qualified path remains in authored file: {path.relative_to(ROOT)}")

markdown_files = {
    path.relative_to(ROOT).as_posix()
    for path in ROOT.rglob("*.md")
    if not any(part in generated_parts for part in path.parts)
}
if markdown_files != {"README.md", "ARCHITECTURE.md"}:
    raise AssertionError(f"unexpected Markdown files: {sorted(markdown_files)}")

build_info = ROOT / "PanelPlugin" / "dist" / "SRS3_E2E_Panel" / "BUILD_INFO.txt"
if build_info.exists():
    info = build_info.read_text(encoding="utf-8")
    match = re.search(r"^SHA256=([0-9A-F]{64})$", info, re.M)
    if not match:
        raise AssertionError("packaged DLL SHA256 is missing or malformed")

print("SRS3 E2E Panel delivery verification")
print("  unified protection/Rx UI:   present")
print("  row-local staged config:    present")
print("  send/editor controls:       absent")
print("  safe command gating:        present")
print("  canonical core SysVar:      Int32[320]")
print(f"  SysVar definitions/files:   {len(owners)}/{len(sysvar_files)}; duplicates 0")
print("  PanelBridge definitions:    1 (canonical core XML)")
print("  offline package script:     present")
print("  portable authored paths:    present")
print("  permanent Markdown files:   2")
print("PASS (offline/static only; CANoe integration and bus behavior not tested)")
