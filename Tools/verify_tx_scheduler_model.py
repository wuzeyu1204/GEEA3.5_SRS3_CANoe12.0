#!/usr/bin/env python3
"""Offline ownership model: the adapter consumes events but never creates them."""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PDUS = json.loads((ROOT / "Config/E2E_Rules_Manifest.json").read_text(encoding="utf-8"))["tx_pdus"]


def adapt(events, enabled, suppressed):
    output = []
    protected = [0] * len(PDUS)
    for pdu_index, time_ms, payload in events:
        if pdu_index in suppressed:
            continue
        output.append((pdu_index, time_ms, bytes(payload)))
        if enabled[pdu_index]:
            protected[pdu_index] += 1
    return output, protected


def main():
    source = []
    for index, pdu in enumerate(PDUS):
        for time_ms in range(0, 1001, pdu["cycle_ms"]):
            source.append((index, time_ms, bytes(8)))
    source.sort(key=lambda item: (item[1], item[0]))

    output, protected = adapt(source, [True] * len(PDUS), set())
    assert len(output) == len(source)
    assert [(x[0], x[1]) for x in output] == [(x[0], x[1]) for x in source]
    assert protected == [sum(1 for item in source if item[0] == i) for i in range(len(PDUS))]

    output, protected = adapt(source, [True, False, True, False, True], {2})
    assert all(item[0] != 2 for item in output)
    assert protected[1] == protected[3] == 0
    assert len(output) < len(source)
    # Every output is traceable to an input event; no independent scheduler exists.
    assert set((i, t) for i, t, _ in output) <= set((i, t) for i, t, _ in source)

    print("PASS (offline ownership model): output events are a subset of PDU IG input events; no new timing source")


if __name__ == "__main__":
    main()
