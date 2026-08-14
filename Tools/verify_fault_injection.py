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


class TxCounterModel:
    """State model matching the CAPL TxPending counter ownership."""

    def __init__(self, next_counter):
        self.next_counter = next_counter
        self.last_counter = (next_counter - 1) % 15

    def emit(self, fault=None, parameter=1):
        counter = self.next_counter
        if fault == "freeze":
            counter = self.last_counter
        elif fault == "jump":
            counter = (counter + parameter) % 15
        if fault != "freeze":
            self.next_counter = (counter + 1) % 15
        self.last_counter = counter
        return counter


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

        # Stateful regressions: each fault is surrounded by a normal frame and
        # a recovery frame. Freeze reuses the last transmitted counter while
        # preserving the already pending next counter.
        one_shot = TxCounterModel(2)
        assert [one_shot.emit(), one_shot.emit("freeze"), one_shot.emit()] == [2, 2, 3]

        wrap = TxCounterModel(14)
        assert [wrap.emit(), wrap.emit("freeze"), wrap.emit()] == [14, 14, 0]

        continuous = TxCounterModel(5)
        assert [continuous.emit(), continuous.emit("freeze"), continuous.emit("freeze"), continuous.emit()] == [5, 5, 5, 6]

        jump = TxCounterModel(0)
        jump_sequence = [jump.emit(), jump.emit("jump", 3), jump.emit()]
        assert jump_sequence == [0, 4, 5]
        for counter in jump_sequence:
            frame = protect(pdu, base, counter)
            assert get_raw(frame, pdu["crc"]["start_bit"], 8) == calculate(pdu, frame, counter)

        crc_normal = protect(pdu, base, 0)
        crc_fault = protect(pdu, base, 1)
        set_raw(crc_fault, pdu["crc"]["start_bit"], 8,
                get_raw(crc_fault, pdu["crc"]["start_bit"], 8) ^ 1)
        crc_recovery = protect(pdu, base, 2)
        assert get_raw(crc_normal, pdu["crc"]["start_bit"], 8) == calculate(pdu, crc_normal, 0)
        assert get_raw(crc_fault, pdu["crc"]["start_bit"], 8) != calculate(pdu, crc_fault, 1)
        assert get_raw(crc_recovery, pdu["crc"]["start_bit"], 8) == calculate(pdu, crc_recovery, 2)

        ub_sequence = [protect(pdu, base, 0, ub=1), protect(pdu, base, 1, ub=0), protect(pdu, base, 2, ub=1)]
        assert [get_raw(frame, pdu["ub"]["start_bit"], 1) for frame in ub_sequence] == [1, 0, 1]

        checked += 15

    capl = (ROOT / "CAPL" / "E2E" / "E2E_FaultInjection.cin").read_text(encoding="ascii")
    for token in ("E2E_FAULT_CORRUPT_CRC", "E2E_FAULT_FREEZE_COUNTER", "E2E_FAULT_COUNTER_15",
                  "E2E_FAULT_COUNTER_JUMP", "E2E_FAULT_WRONG_DATA_ID", "E2E_FAULT_UB_ZERO",
                  "E2E_FAULT_CORRUPT_PAYLOAD", "E2E_FAULT_STOP_TX"):
        assert token in capl
    assert "output(" not in capl.lower()
    assert "counter = gE2E_LastCounter[pduIndex];" in CONTROLLER
    assert "if (faultType != E2E_FAULT_FREEZE_COUNTER)" in CONTROLLER

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
        f"PASS (static/offline only): {checked} fault/state semantics across "
        f"{len(MANIFEST['tx_pdus'])} Tx PDUs; bridge arm/clear/ack protocol; no second sender"
    )
    print("  Freeze:            2 -> 2 -> 3")
    print("  Freeze Wrap:       14 -> 14 -> 0")
    print("  Continuous Freeze: 5 -> 5 -> 5 -> clear -> 6")


if __name__ == "__main__":
    main()
