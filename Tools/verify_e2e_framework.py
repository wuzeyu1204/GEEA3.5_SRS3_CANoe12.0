#!/usr/bin/env python3
"""Read-only consistency checks for the CANoe E2E framework manifest.

The script does not start CANoe and does not modify project files.
"""

from __future__ import annotations

import json
import math
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Config" / "E2E_Rules_Manifest.json"


@dataclass
class DbcSignal:
    name: str
    start_bit: int
    length: int
    byte_order: str
    signed: bool
    factor: float
    offset: float
    minimum: float
    maximum: float
    unit: str


@dataclass
class DbcMessage:
    can_id: int
    name: str
    dlc: int
    signals: dict[str, DbcSignal] = field(default_factory=dict)
    cycle_ms: int | None = None


BO_RE = re.compile(r"^BO_\s+(\d+)\s+(\S+):\s+(\d+)\s+")
SG_RE = re.compile(
    r'^\s*SG_\s+(\S+)\s*:\s*(\d+)\|(\d+)@([01])([+-])\s*'
    r'\(([-+0-9.eE]+),([-+0-9.eE]+)\)\s*'
    r'\[([-+0-9.eE]+)\|([-+0-9.eE]+)\]\s*"([^"]*)"'
)
CYCLE_RE = re.compile(r'^BA_\s+"GenMsgCycleTime"\s+BO_\s+(\d+)\s+(\d+)\s*;')


def parse_dbc(path: Path) -> dict[int, DbcMessage]:
    messages: dict[int, DbcMessage] = {}
    current: DbcMessage | None = None
    cycles: dict[int, int] = {}

    for line in path.read_text(encoding="utf-8-sig", errors="replace").splitlines():
        match = BO_RE.match(line)
        if match:
            current = DbcMessage(int(match.group(1)), match.group(2), int(match.group(3)))
            messages[current.can_id] = current
            continue

        match = SG_RE.match(line)
        if match and current is not None:
            signal = DbcSignal(
                name=match.group(1),
                start_bit=int(match.group(2)),
                length=int(match.group(3)),
                byte_order="big_endian" if match.group(4) == "0" else "little_endian",
                signed=match.group(5) == "-",
                factor=float(match.group(6)),
                offset=float(match.group(7)),
                minimum=float(match.group(8)),
                maximum=float(match.group(9)),
                unit=match.group(10),
            )
            current.signals[signal.name] = signal
            continue

        match = CYCLE_RE.match(line)
        if match:
            cycles[int(match.group(1))] = int(match.group(2))

    for can_id, cycle in cycles.items():
        if can_id in messages:
            messages[can_id].cycle_ms = cycle
    return messages


def child_text(node: ET.Element, local_name: str) -> str | None:
    child = node.find(f"{{*}}{local_name}")
    return child.text.strip() if child is not None and child.text else None


def parse_e2e_profiles(path: Path) -> dict[str, dict[str, int | str]]:
    root = ET.parse(path).getroot()
    profiles: dict[str, dict[str, int | str]] = {}
    for protection in root.findall(".//{*}END-TO-END-PROTECTION"):
        name = child_text(protection, "SHORT-NAME")
        profile = protection.find("{*}END-TO-END-PROFILE")
        if not name or profile is None:
            continue
        data_id = profile.find("{*}DATA-IDS/{*}DATA-ID")
        profiles[name] = {
            "category": child_text(profile, "CATEGORY") or "",
            "data_id": int(data_id.text) if data_id is not None and data_id.text else -1,
            "max_delta": int(child_text(profile, "MAX-DELTA-COUNTER-INIT") or -1),
            "crc_offset": int(child_text(profile, "CRC-OFFSET") or -1),
            "counter_offset": int(child_text(profile, "COUNTER-OFFSET") or -1),
        }
    return profiles


def close_enough(left: float, right: float) -> bool:
    return math.isclose(left, right, rel_tol=1e-9, abs_tol=1e-9)


def check_signal(errors: list[str], message: DbcMessage, expected: dict, prefix: str) -> None:
    name = expected["dbc_signal"]
    actual = message.signals.get(name)
    if actual is None:
        errors.append(f"{prefix}: DBC signal not found: {name}")
        return
    for key in ("start_bit", "length", "byte_order"):
        if getattr(actual, key) != expected[key]:
            errors.append(
                f"{prefix}/{name}: {key} manifest={expected[key]!r}, dbc={getattr(actual, key)!r}"
            )
    if "signed" in expected and actual.signed != expected["signed"]:
        errors.append(f"{prefix}/{name}: signed manifest={expected['signed']}, dbc={actual.signed}")
    for key in ("factor", "offset", "minimum", "maximum"):
        if key in expected and not close_enough(getattr(actual, key), float(expected[key])):
            errors.append(
                f"{prefix}/{name}: {key} manifest={expected[key]!r}, dbc={getattr(actual, key)!r}"
            )
    if "unit" in expected and actual.unit != expected["unit"]:
        errors.append(f"{prefix}/{name}: unit manifest={expected['unit']!r}, dbc={actual.unit!r}")


def main() -> int:
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    can1 = parse_dbc(ROOT / manifest["sources"]["can1_dbc"])
    canfd3 = parse_dbc(ROOT / manifest["sources"]["canfd3_dbc"])
    profiles = parse_e2e_profiles(ROOT / manifest["sources"]["arxml"])
    buses = {"CAN1": can1, "CANFD3": canfd3}
    errors: list[str] = []

    tx_pdus = manifest["tx_pdus"]
    tx_elements = sum(len(pdu["elements"]) for pdu in tx_pdus)
    if len(tx_pdus) != 5:
        errors.append(f"Expected 5 Tx PDUs, found {len(tx_pdus)}")
    if tx_elements != 16:
        errors.append(f"Expected 16 editable Tx elements, found {tx_elements}")

    for pdu in tx_pdus:
        prefix = f"TX {pdu['can_id_hex']} {pdu['group']}"
        messages = buses.get(pdu["bus"])
        message = messages.get(pdu["can_id"]) if messages else None
        if message is None:
            errors.append(f"{prefix}: frame not found in {pdu['bus']} DBC")
            continue
        if message.name != pdu["frame"]:
            errors.append(f"{prefix}: frame name manifest={pdu['frame']}, dbc={message.name}")
        if message.dlc != pdu["dlc"]:
            errors.append(f"{prefix}: DLC manifest={pdu['dlc']}, dbc={message.dlc}")
        if message.cycle_ms != pdu["cycle_ms"]:
            errors.append(f"{prefix}: cycle manifest={pdu['cycle_ms']}, dbc={message.cycle_ms}")

        check_signal(errors, message, pdu["ub"], prefix)
        check_signal(errors, message, pdu["crc"], prefix)
        check_signal(errors, message, pdu["counter"], prefix)
        for element in pdu["elements"]:
            check_signal(errors, message, element, prefix)

        profile = profiles.get(pdu["e2e_object"])
        if profile is None:
            errors.append(f"{prefix}: ARXML E2E object not found: {pdu['e2e_object']}")
            continue
        expected_profile = {
            "category": "PROFILE_G",
            "data_id": pdu["data_id"],
            "max_delta": pdu["max_delta"],
            "crc_offset": pdu["crc_offset"],
            "counter_offset": pdu["counter_offset"],
        }
        for key, expected in expected_profile.items():
            if profile[key] != expected:
                errors.append(
                    f"{prefix}/{pdu['e2e_object']}: {key} manifest={expected!r}, arxml={profile[key]!r}"
                )

    rx_scope = manifest["rx_scope"]
    for item in rx_scope:
        prefix = f"RX {item['bus']} {item['can_id_hex']} {item['group']}"
        messages = buses.get(item["bus"])
        message = messages.get(item["can_id"]) if messages else None
        if message is None:
            errors.append(f"{prefix}: frame not found in DBC")
            continue
        if message.name != item["frame"]:
            errors.append(f"{prefix}: frame name manifest={item['frame']}, dbc={message.name}")
        if message.dlc != item["dlc"]:
            errors.append(f"{prefix}: DLC manifest={item['dlc']}, dbc={message.dlc}")
        if message.cycle_ms != item["cycle_ms"]:
            errors.append(f"{prefix}: cycle manifest={item['cycle_ms']}, dbc={message.cycle_ms}")
        profile = profiles.get(item["e2e_object"])
        if profile is None:
            errors.append(f"{prefix}: ARXML E2E object not found: {item['e2e_object']}")
        elif profile["data_id"] != item["data_id"] or profile["max_delta"] != item["max_delta"]:
            errors.append(
                f"{prefix}: ARXML profile differs: data_id={profile['data_id']}, max_delta={profile['max_delta']}"
            )

    print("SRS3 CANoe E2E framework static verification")
    print(f"  Tx PDUs:              {len(tx_pdus)}")
    print(f"  Tx application items: {tx_elements}")
    print(f"  Rx E2E groups:        {len(rx_scope)}")
    print(f"  ARXML profiles:       {len(profiles)}")

    if errors:
        print(f"\nFAILED: {len(errors)} issue(s)")
        for error in errors:
            print(f"  - {error}")
        return 1

    print("\nPASS: manifest, DBC and ARXML fields are consistent.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

