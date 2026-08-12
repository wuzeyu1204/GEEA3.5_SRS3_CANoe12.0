#!/usr/bin/env python3
"""Independently verify checked-in E2E golden vectors.

This implementation does not import or execute ZXDoc code. It reconstructs the
physical payload and Profile-G wrapper from the CANoe manifest, then compares
all bytes with Test/GoldenVectors.json.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "Config" / "E2E_Rules_Manifest.json"
VECTORS_PATH = ROOT / "Test" / "GoldenVectors.json"


def parse_hex_bytes(text: str) -> bytearray:
    return bytearray(int(value, 16) for value in text.split())


def hex_bytes(data: bytes | bytearray) -> str:
    return " ".join(f"{value:02X}" for value in data)


def motorola_positions(start_bit: int, length: int):
    position = start_bit
    for _ in range(length):
        yield position
        position = position + 15 if position % 8 == 0 else position - 1


def set_raw(data: bytearray, signal: dict, value: int) -> None:
    length = int(signal["length"])
    raw = int(value) & ((1 << length) - 1)
    if signal["byte_order"] == "little_endian":
        positions = [int(signal["start_bit"]) + index for index in range(length)]
        shifts = range(length)
    else:
        positions = list(motorola_positions(int(signal["start_bit"]), length))
        shifts = range(length - 1, -1, -1)
    for position, shift in zip(positions, shifts):
        byte_index, bit_index = divmod(position, 8)
        if (raw >> shift) & 1:
            data[byte_index] |= 1 << bit_index
        else:
            data[byte_index] &= ~(1 << bit_index)


def get_raw(data: bytes | bytearray, signal: dict) -> int:
    length = int(signal["length"])
    if signal["byte_order"] == "little_endian":
        value = 0
        for index in range(length):
            position = int(signal["start_bit"]) + index
            value |= ((data[position // 8] >> (position % 8)) & 1) << index
        return value
    value = 0
    for position in motorola_positions(int(signal["start_bit"]), length):
        value = (value << 1) | ((data[position // 8] >> (position % 8)) & 1)
    return value


def serialize_raw(raw: int, bit_length: int) -> bytes:
    width = (bit_length + 7) // 8
    mask = (1 << bit_length) - 1
    return (raw & mask).to_bytes(width, byteorder="little", signed=False)


def crc8_j1850_zero(data: bytes | bytearray) -> int:
    crc = 0
    for value in data:
        crc ^= value
        for _ in range(8):
            crc = ((crc << 1) ^ 0x1D) & 0xFF if crc & 0x80 else (crc << 1) & 0xFF
    return crc


def main() -> int:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    golden = json.loads(VECTORS_PATH.read_text(encoding="utf-8"))
    rules = {int(item["can_id"]): item for item in manifest["tx_pdus"]}
    errors = []
    seen = set()

    for vector in golden["vectors"]:
        vector_id = vector["id"]
        seen.add((int(vector["can_id"]), int(vector["counter"])))
        rule = rules.get(int(vector["can_id"]))
        if rule is None:
            errors.append(f"{vector_id}: CAN ID missing from manifest")
            continue

        data = bytearray(int(rule["dlc"]))
        set_raw(data, rule["ub"], int(vector["ub"]))
        for element in rule["elements"]:
            app_name = element["application_element"]
            if app_name not in vector["application_raw"]:
                errors.append(f"{vector_id}: missing application value {app_name}")
                continue
            set_raw(data, element, int(vector["application_raw"][app_name]))

        if hex_bytes(data) != vector["input_payload_hex"]:
            errors.append(
                f"{vector_id}: input payload {hex_bytes(data)} != {vector['input_payload_hex']}"
            )

        counter = int(vector["counter"])
        set_raw(data, rule["counter"], counter)
        crc_input = bytearray((int(rule["data_id"]) & 0xFF, (int(rule["data_id"]) >> 8) & 0xFF, counter & 0x0F))
        for element in rule["elements"]:
            raw = get_raw(data, element)
            crc_input.extend(serialize_raw(raw, int(element["length"])))

        crc = crc8_j1850_zero(crc_input)
        set_raw(data, rule["crc"], crc)

        if hex_bytes(crc_input) != vector["crc_input_hex"]:
            errors.append(
                f"{vector_id}: CRC input {hex_bytes(crc_input)} != {vector['crc_input_hex']}"
            )
        if crc != int(vector["expected_crc"]):
            errors.append(f"{vector_id}: CRC 0x{crc:02X} != {vector['expected_crc_hex']}")
        if hex_bytes(data) != vector["expected_payload_hex"]:
            errors.append(
                f"{vector_id}: payload {hex_bytes(data)} != {vector['expected_payload_hex']}"
            )

    expected_seen = {(int(rule["can_id"]), counter) for rule in manifest["tx_pdus"] for counter in (0, 7, 14)}
    if seen != expected_seen:
        errors.append(f"Vector coverage differs: actual={sorted(seen)}, expected={sorted(expected_seen)}")
    if int(golden["vector_count"]) != len(golden["vectors"]) or len(golden["vectors"]) != 15:
        errors.append(f"Expected exactly 15 vectors, found {len(golden['vectors'])}")

    anchor = golden.get("external_anchor", {})
    anchor_input = parse_hex_bytes(anchor.get("crc_input_hex", ""))
    anchor_crc = crc8_j1850_zero(anchor_input)
    if anchor_crc != int(anchor.get("expected_crc", -1)) or anchor_crc != 0xD4:
        errors.append(
            f"ZXDoc external anchor: CRC 0x{anchor_crc:02X} != {anchor.get('expected_crc_hex')}"
        )

    print("SRS3 CANoe E2E independent golden-vector verification")
    print(f"  Tx PDUs:        {len(rules)}")
    print(f"  Golden vectors: {len(golden['vectors'])}")
    print("  Counters:       0, 7, 14")
    print(f"  ZXDoc anchor:   0x{anchor_crc:02X}")
    if errors:
        print(f"\nFAILED: {len(errors)} issue(s)")
        for error in errors:
            print(f"  - {error}")
        return 1
    print("\nPASS: all payloads, CRC inputs, counters and CRC values match.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
