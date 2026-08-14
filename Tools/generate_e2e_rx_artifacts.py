#!/usr/bin/env python3
"""Generate deterministic Rx monitor artifacts from the ZXDoc rule baseline."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def load_groups(source: Path):
    raw = source.read_bytes()
    document = json.loads(raw.decode("utf-8"))
    groups = []
    index = 0
    for bus_index, bus_name in enumerate(("CAN1", "CANFD3")):
        for frame in document["buses"][bus_name]["frames"]:
            if not frame.get("rx_monitor_enabled", False):
                continue
            for group in frame["groups"]:
                elements = sorted(group["elements"], key=lambda value: value["application_element"].lower())
                physical_fields = [group["ub"], group["crc"], group["counter"]] + elements
                unsupported = [field["name"] for field in physical_fields if field["byte_order"] != "big_endian"]
                if unsupported:
                    raise ValueError(f"Rx generator currently requires Motorola fields: {bus_name} {frame['name']} {unsupported}")
                groups.append(
                    {
                        "index": index,
                        "bus_index": bus_index,
                        "bus": bus_name,
                        "can_id": frame["can_id"],
                        "can_id_hex": frame["can_id_hex"],
                        "frame": frame["name"],
                        "dlc": frame["dlc"],
                        "cycle_ms": frame["cycle_ms"],
                        "timeout_ms": max(50, 4 * frame["cycle_ms"]),
                        "group": group["name"],
                        "data_id": group["data_id"],
                        "data_id_hex": group["data_id_hex"],
                        "max_delta": group["max_delta"],
                        "ub_start_bit": group["ub"]["start_bit"],
                        "crc_start_bit": group["crc"]["start_bit"],
                        "counter_start_bit": group["counter"]["start_bit"],
                        "elements": [
                            {
                                "name": element["application_element"],
                                "signal": element["name"],
                                "start_bit": element["start_bit"],
                                "length": element["length"],
                                "signed": element.get("signed", False),
                                "byte_order": element["byte_order"],
                            }
                            for element in elements
                        ],
                    }
                )
                index += 1
    return document, groups, hashlib.sha256(raw).hexdigest().upper()


def capl_switch(name, groups, field):
    lines = [f"int {name}(int groupIndex)", "{", "  switch (groupIndex)", "  {"]
    for group in groups:
        lines.append(f"    case {group['index']}: return {group[field]};")
    lines.extend(["    default: return -1;", "  }", "}", ""])
    return lines


def generate_capl(groups, source_hash):
    lines = [
        "/*@!Encoding:1252*/",
        "",
        "/* GENERATED FILE - DO NOT EDIT.",
        f" * Source SHA-256: {source_hash}",
        " * Physical start bits use Vector/DBC Motorola numbering.",
        " */",
        "",
        f"int E2E_GetRxGroupCount() {{ return {len(groups)}; }}",
        "",
    ]
    for name, field in (
        ("E2E_GetRxBus", "bus_index"),
        ("E2E_GetRxCanId", "can_id"),
        ("E2E_GetRxDlc", "dlc"),
        ("E2E_GetRxCycleMs", "cycle_ms"),
        ("E2E_GetRxTimeoutMs", "timeout_ms"),
        ("E2E_GetRxDataId", "data_id"),
        ("E2E_GetRxMaxDelta", "max_delta"),
        ("E2E_GetRxUbStartBit", "ub_start_bit"),
        ("E2E_GetRxCrcStartBit", "crc_start_bit"),
        ("E2E_GetRxCounterStartBit", "counter_start_bit"),
    ):
        lines.extend(capl_switch(name, groups, field))

    lines.extend(["int E2E_GetRxElementCount(int groupIndex)", "{", "  switch (groupIndex)", "  {"])
    for group in groups:
        lines.append(f"    case {group['index']}: return {len(group['elements'])};")
    lines.extend(["    default: return 0;", "  }", "}", ""])

    for function_name, key in (("E2E_GetRxElementStartBit", "start_bit"), ("E2E_GetRxElementBitLength", "length")):
        lines.extend([f"int {function_name}(int groupIndex, int elementIndex)", "{", "  switch (groupIndex)", "  {"])
        for group in groups:
            lines.append(f"    case {group['index']}:")
            lines.append("      switch (elementIndex)")
            lines.append("      {")
            for element_index, element in enumerate(group["elements"]):
                lines.append(f"        case {element_index}: return {element[key]};")
            lines.extend(["        default: return -1;", "      }"])
        lines.extend(["    default: return -1;", "  }", "}", ""])

    return "\n".join(lines) + "\n"


def cs_string(value):
    return json.dumps(value, ensure_ascii=True)


def generate_csharp(groups, source_hash):
    lines = [
        "// GENERATED FILE - DO NOT EDIT.",
        f"// Source SHA-256: {source_hash}",
        "using System.Collections.ObjectModel;",
        "",
        "namespace SRS3.E2E.PanelControl.Models",
        "{",
        "    internal static class RxPanelMetadata",
        "    {",
        "        internal static ObservableCollection<RxGroupRow> CreateGroups()",
        "        {",
        "            return new ObservableCollection<RxGroupRow>",
        "            {",
    ]
    for group in groups:
        elements = ", ".join(
            "new RxElementDefinition(%s, %s, %d, %d, %s)"
            % (
                cs_string(element["name"]),
                cs_string(element["signal"]),
                element["start_bit"],
                element["length"],
                "true" if element["signed"] else "false",
            )
            for element in group["elements"]
        )
        lines.append(
            "                new RxGroupRow(%d, %s, %s, %s, %s, %d, %d, %d, %d, %d, %d, %d, %d, new RxElementDefinition[] { %s }),"
            % (
                group["index"], cs_string(group["bus"]), cs_string(group["can_id_hex"]),
                cs_string(group["frame"]), cs_string(group["group"]), group["dlc"],
                group["cycle_ms"], group["timeout_ms"], group["data_id"], group["max_delta"],
                group["ub_start_bit"], group["crc_start_bit"], group["counter_start_bit"], elements,
            )
        )
    lines.extend(["            };", "        }", "    }", "}", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--source-repository", required=True)
    parser.add_argument("--source-path", required=True)
    parser.add_argument("--source-commit", required=True)
    args = parser.parse_args()
    document, groups, source_hash = load_groups(args.source)
    source_reference = Path(args.source_path)
    if source_reference.is_absolute() or ".." in source_reference.parts:
        parser.error("--source-path must be a portable path inside the source repository")
    result = {
        "schema": "SRS3_E2E_RxRules_v1",
        "snapshot_scope": f"{len(groups)} enabled Rx groups required by this CANoe repository",
        "provenance": {
            "repository": args.source_repository,
            "source_path": source_reference.as_posix(),
            "source_commit": args.source_commit,
            "source_sha256": source_hash,
        },
        "profile": document["profile"],
        "timeout_policy": "max(50 ms, 4 * cycle_ms)",
        "groups": groups,
    }
    (ROOT / "Config" / "E2E_Rx_Rules.json").write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (ROOT / "CAPL" / "E2E" / "Generated" / "E2E_RxRules_Generated.cin").write_text(
        generate_capl(groups, source_hash), encoding="ascii"
    )
    (ROOT / "PanelPlugin" / "SRS3E2EPanel" / "Models" / "RxPanelMetadata.Generated.cs").write_text(
        generate_csharp(groups, source_hash), encoding="utf-8"
    )
    print(f"generated {len(groups)} Rx groups from {args.source}")
    print(f"source sha256 {source_hash}")


if __name__ == "__main__":
    main()
