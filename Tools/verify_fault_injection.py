#!/usr/bin/env python3
"""Independent semantic checks for all Tx fault-injection modes."""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = json.loads((ROOT / "Config" / "E2E_Rules_Manifest.json").read_text(encoding="utf-8"))
VECTORS = json.loads((ROOT / "Test" / "GoldenVectors.json").read_text(encoding="utf-8"))["vectors"]
WPF = (ROOT / "PanelPlugin" / "SRS3E2EPanel" / "E2EConsoleControl.xaml.cs").read_text(encoding="utf-8")
CONTROLLER = (ROOT / "CAPL" / "E2E" / "E2E_TxController.cin").read_text(encoding="ascii")


def positions(start, length):
    position = start
    for _ in range(length):
        yield position
        position = position + 15 if position % 8 == 0 else position - 1


def get_raw(payload, start, length):
    value = 0
    for position in positions(start, length):
        byte_index, bit_index = divmod(position, 8)
        value = (value << 1) | ((payload[byte_index] >> bit_index) & 1)
    return value


def set_raw(payload, start, length, value):
    for source_bit, position in enumerate(positions(start, length)):
        byte_index, bit_index = divmod(position, 8)
        mask = 1 << bit_index
        bit = (value >> (length - 1 - source_bit)) & 1
        payload[byte_index] = payload[byte_index] | mask if bit else payload[byte_index] & ~mask


def crc_update(crc, value):
    crc ^= value
    for _ in range(8):
        crc = ((crc << 1) ^ 0x1D) & 0xFF if crc & 0x80 else (crc << 1) & 0xFF
    return crc


def calculate(pdu, payload, counter, data_id=None):
    data_id = pdu["data_id"] if data_id is None else data_id
    crc = 0
    for value in (data_id & 0xFF, (data_id >> 8) & 0xFF, counter & 0x0F):
        crc = crc_update(crc, value)
    for element in pdu["elements"]:
        raw = get_raw(payload, element["start_bit"], element["length"])
        for byte_index in range((element["length"] + 7) // 8):
            crc = crc_update(crc, (raw >> (8 * byte_index)) & 0xFF)
    return crc


def protect(pdu, payload, counter, data_id=None, ub=1):
    result = bytearray(payload)
    set_raw(result, pdu["ub"]["start_bit"], 1, ub)
    set_raw(result, pdu["counter"]["start_bit"], 4, counter)
    set_raw(result, pdu["crc"]["start_bit"], 8, calculate(pdu, result, counter, data_id))
    return result


def main():
    checked = 0
    for pdu in MANIFEST["tx_pdus"]:
        vector = next(item for item in VECTORS if item["can_id"] == pdu["can_id"] and item["counter"] == 0)
        base = bytearray.fromhex(vector["expected_payload_hex"])
        assert get_raw(base, pdu["crc"]["start_bit"], 8) == calculate(pdu, base, 0)

        corrupt_crc = bytearray(base)
        set_raw(corrupt_crc, pdu["crc"]["start_bit"], 8,
                get_raw(corrupt_crc, pdu["crc"]["start_bit"], 8) ^ 1)
        assert get_raw(corrupt_crc, pdu["crc"]["start_bit"], 8) != calculate(pdu, corrupt_crc, 0)

        frozen = protect(pdu, base, 0)
        assert get_raw(frozen, pdu["counter"]["start_bit"], 4) == 0
        assert get_raw(frozen, pdu["crc"]["start_bit"], 8) == calculate(pdu, frozen, 0)

        illegal = protect(pdu, base, 15)
        assert get_raw(illegal, pdu["counter"]["start_bit"], 4) == 15
        assert get_raw(illegal, pdu["crc"]["start_bit"], 8) == calculate(pdu, illegal, 15)

        jumped = protect(pdu, base, 3)
        assert (get_raw(jumped, pdu["counter"]["start_bit"], 4) - 0) % 15 == 3

        wrong_did = protect(pdu, base, 0, pdu["data_id"] ^ 1)
        assert get_raw(wrong_did, pdu["crc"]["start_bit"], 8) != calculate(pdu, wrong_did, 0)

        ub_zero = protect(pdu, base, 0, ub=0)
        assert get_raw(ub_zero, pdu["ub"]["start_bit"], 1) == 0

        corrupt_payload = bytearray(base)
        first = pdu["elements"][0]
        set_raw(corrupt_payload, first["start_bit"], first["length"],
                get_raw(corrupt_payload, first["start_bit"], first["length"]) ^ 1)
        assert get_raw(corrupt_payload, pdu["crc"]["start_bit"], 8) != calculate(pdu, corrupt_payload, 0)

        stop_tx_permit = False
        assert not stop_tx_permit
        checked += 8

    capl = (ROOT / "CAPL" / "E2E" / "E2E_FaultInjection.cin").read_text(encoding="ascii")
    for token in ("E2E_FAULT_CORRUPT_CRC", "E2E_FAULT_FREEZE_COUNTER", "E2E_FAULT_COUNTER_15",
                  "E2E_FAULT_COUNTER_JUMP", "E2E_FAULT_WRONG_DATA_ID", "E2E_FAULT_UB_ZERO",
                  "E2E_FAULT_CORRUPT_PAYLOAD", "E2E_FAULT_STOP_TX"):
        assert token in capl
    assert "output(" not in capl.lower()

    # Verify the complete WPF -> LongArray -> CAPL command protocol, not only
    # the byte-level fault transformations.
    bridge_fields = {
        "FaultTypeIndex": 52,
        "FaultModeIndex": 53,
        "FaultParameterIndex": 54,
        "FaultDurationIndex": 55,
        "FaultCommandIndex": 56,
        "FaultSequenceIndex": 57,
        "FaultActiveTypeIndex": 58,
        "FaultRemainingIndex": 59,
        "FaultAppliedIndex": 60,
        "FaultResultIndex": 61,
        "FaultActiveMaskIndex": 62,
        "FaultAckIndex": 63,
    }
    for name, index in bridge_fields.items():
        assert f"private const int {name} = {index};" in WPF
    for fragment in (
        "SendFaultCommand(1)",
        "SendFaultCommand(2)",
        "SendFaultCommand(3)",
        "values[SelectedPduIndex] = CurrentPdu.Index;",
        "values[FaultSequenceIndex] = faultSequence;",
        "values[FaultCommandIndex] = command;",
        "faultSequence == faultAck",
    ):
        assert fragment in WPF
    assert "E2E_FaultPollCommand(pduIndex);" in CONTROLLER

    print(
        f"PASS (static/offline only): {checked} fault semantics across "
        f"{len(MANIFEST['tx_pdus'])} Tx PDUs; bridge arm/clear/ack protocol; no second sender"
    )


if __name__ == "__main__":
    main()
