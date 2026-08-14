#!/usr/bin/env python3
"""Independent static/golden-vector verification for the generated Rx checker."""

from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "Config" / "E2E_Rx_Rules.json"
VECTORS = ROOT / "Test" / "RxGoldenVectors.json"


def position_sequence(start, length):
    position = start
    for _ in range(length):
        yield position
        position = position + 15 if position % 8 == 0 else position - 1


def set_motorola(payload, start, length, raw):
    for element_bit, position in enumerate(position_sequence(start, length)):
        byte_index, bit_index = divmod(position, 8)
        mask = 1 << bit_index
        source = (raw >> (length - 1 - element_bit)) & 1
        payload[byte_index] = (payload[byte_index] | mask) if source else (payload[byte_index] & ~mask)


def get_motorola(payload, start, length):
    raw = 0
    for position in position_sequence(start, length):
        byte_index, bit_index = divmod(position, 8)
        raw = (raw << 1) | ((payload[byte_index] >> bit_index) & 1)
    return raw


def crc_update(crc, value):
    crc ^= value
    for _ in range(8):
        crc = ((crc << 1) ^ 0x1D) & 0xFF if crc & 0x80 else (crc << 1) & 0xFF
    return crc


def calculate(group, payload, counter):
    crc = 0
    for value in (group["data_id"] & 0xFF, group["data_id"] >> 8, counter & 0x0F):
        crc = crc_update(crc, value)
    for element in group["elements"]:
        raw = get_motorola(payload, element["start_bit"], element["length"])
        for byte_index in range((element["length"] + 7) // 8):
            crc = crc_update(crc, (raw >> (8 * byte_index)) & 0xFF)
    return crc


def valid_payload(group, counter=0):
    payload = bytearray(group["dlc"])
    set_motorola(payload, group["ub_start_bit"], 1, 1)
    set_motorola(payload, group["counter_start_bit"], 4, counter)
    crc = calculate(group, payload, counter)
    set_motorola(payload, group["crc_start_bit"], 8, crc)
    return payload, crc


class Monitor:
    def __init__(self, group):
        self.group = group
        self.previous = None

    def process(self, payload):
        ub = get_motorola(payload, self.group["ub_start_bit"], 1)
        counter = get_motorola(payload, self.group["counter_start_bit"], 4)
        crc_rx = get_motorola(payload, self.group["crc_start_bit"], 8)
        if not ub:
            return "UB_INACTIVE"
        if crc_rx != calculate(self.group, payload, counter):
            return "CRC_ERROR"
        if counter > 14:
            return "COUNTER_ILLEGAL"
        if self.previous is None:
            self.previous = counter
            return "INITIAL"
        delta = (counter - self.previous + 15) % 15
        self.previous = counter
        if delta == 0:
            return "REPEATED"
        if delta == 1:
            return "OK"
        if delta <= self.group["max_delta"]:
            return "OK_SOME_LOST"
        return "WRONG_SEQUENCE"

    def timeout(self):
        self.previous = None
        return "TIMEOUT"


def main():
    rules = json.loads(RULES.read_text(encoding="utf-8"))
    groups = rules["groups"]
    assert len(groups) == 15
    assert sum(group["bus"] == "CAN1" for group in groups) == 10
    assert sum(group["bus"] == "CANFD3" for group in groups) == 5

    provenance = rules["provenance"]
    source_path = Path(provenance["source_path"])
    assert not source_path.is_absolute() and ".." not in source_path.parts
    assert re.fullmatch(r"[0-9a-f]{40}", provenance["source_commit"])
    assert re.fullmatch(r"[0-9A-F]{64}", provenance["source_sha256"])
    assert provenance["repository"] == "https://github.com/wuzeyu1204/GEEA3.5_SRS3_E2E_ZXDoc"
    assert "generated_from" not in rules

    vectors = []
    for group in groups:
        payload0, crc0 = valid_payload(group, 0)
        assert get_motorola(payload0, group["ub_start_bit"], 1) == 1
        assert get_motorola(payload0, group["counter_start_bit"], 4) == 0
        assert get_motorola(payload0, group["crc_start_bit"], 8) == crc0
        assert calculate(group, payload0, 0) == crc0

        monitor = Monitor(group)
        assert monitor.process(payload0) == "INITIAL"
        payload1, _ = valid_payload(group, 1)
        assert monitor.process(payload1) == "OK"
        assert monitor.process(payload1) == "REPEATED"

        recovery = Monitor(group)
        assert recovery.process(payload0) == "INITIAL"
        assert recovery.process(payload1) == "OK"
        assert recovery.timeout() == "TIMEOUT"
        payload7, _ = valid_payload(group, 7)
        payload8, _ = valid_payload(group, 8)
        assert recovery.process(payload7) == "INITIAL"
        assert recovery.process(payload8) == "OK"

        corrupted = bytearray(payload1)
        set_motorola(corrupted, group["crc_start_bit"], 8, get_motorola(corrupted, group["crc_start_bit"], 8) ^ 1)
        assert monitor.process(corrupted) == "CRC_ERROR"

        inactive = bytearray(payload1)
        set_motorola(inactive, group["ub_start_bit"], 1, 0)
        assert monitor.process(inactive) == "UB_INACTIVE"

        illegal, _ = valid_payload(group, 15)
        assert monitor.process(illegal) == "COUNTER_ILLEGAL"

        vectors.extend(
            [
                {"group_index": group["index"], "bus": group["bus"], "can_id": group["can_id_hex"], "group": group["group"], "case": "INITIAL_VALID", "expected": "INITIAL", "payload_hex": payload0.hex(" ").upper()},
                {"group_index": group["index"], "bus": group["bus"], "can_id": group["can_id_hex"], "group": group["group"], "case": "CRC_ERROR", "expected": "CRC_ERROR", "payload_hex": corrupted.hex(" ").upper()},
                {"group_index": group["index"], "bus": group["bus"], "can_id": group["can_id_hex"], "group": group["group"], "case": "UB_INACTIVE", "expected": "UB_INACTIVE", "payload_hex": inactive.hex(" ").upper()},
                {"group_index": group["index"], "bus": group["bus"], "can_id": group["can_id_hex"], "group": group["group"], "case": "TIMEOUT_RECOVERY_INITIAL", "expected": "INITIAL", "payload_hex": payload7.hex(" ").upper()},
                {"group_index": group["index"], "bus": group["bus"], "can_id": group["can_id_hex"], "group": group["group"], "case": "TIMEOUT_RECOVERY_OK", "expected": "OK", "payload_hex": payload8.hex(" ").upper()},
            ]
        )

    capl = (ROOT / "CAPL" / "E2E" / "E2E_RxMonitor.cin").read_text(encoding="ascii")
    for required in ("canGetDataLength", "E2E_ProfileGCalculate", "E2E_RX_STATE_TIMEOUT", "gE2E_RxSequenceCounter",
                     "PanelBridge", "E2E_RX_UNIFIED_OFFSET = 96", "E2E_RxExportUnifiedBridge",
                     "E2E_RxPollCommand", "E2E_RX_B_COMMAND_ACK", "gE2E_RxErrorCount[groupIndex]++"):
        assert required in capl or required in (ROOT / "Nodes" / "E2E_RxMonitor.can").read_text(encoding="ascii")
    assert "RxBridge" not in capl, "legacy Rx bridge must not remain"
    assert "output(" not in capl.lower(), "Rx monitor must not transmit"
    timeout_transition = capl[capl.index("gE2E_RxState[groupIndex] = E2E_RX_STATE_TIMEOUT;"):]
    timeout_transition = timeout_transition[:400]
    assert "gE2E_RxHasCounter[groupIndex] = 0;" in timeout_transition
    assert "gE2E_RxSequenceCounter[groupIndex] = 0;" in timeout_transition
    for node in ("E2E_RxMonitor.can", "E2E_RxMonitor_CANFD3.can"):
        text = (ROOT / "Nodes" / node).read_text(encoding="ascii")
        assert "on message *" in text and "canGetDataLength(this)" in text
        assert "output(" not in text.lower(), f"{node} must remain receive-only"
        pre_start = text.split("on preStart", 1)[1].split("on start", 1)[0]
        assert "setTimer" not in pre_start, f"{node} must not start a timer before CAPL START"
        start_block = text.split("on start", 1)[1].split("on message", 1)[0]
        assert "setTimer(gE2E_RxWatchdog, 10);" in start_block, f"{node} must start watchdog in on start"

    VECTORS.write_text(json.dumps({"schema": "SRS3_E2E_RxGoldenVectors_v1", "vectors": vectors}, indent=2) + "\n", encoding="utf-8")
    print(f"PASS: {len(groups)} groups, {len(vectors)} golden vectors, CAN1/CANFD3 receive-only nodes")
    print("  Timeout recovery: OK -> TIMEOUT -> INITIAL -> OK")
    print("  Rx rule snapshot: repository-local with source path/commit/SHA256 provenance")


if __name__ == "__main__":
    main()
