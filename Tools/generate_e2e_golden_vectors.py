#!/usr/bin/env python3
"""Generate CAN1 Tx golden vectors using the external ZXDoc peer protector.

The default output is stdout so the generated result can be reviewed before it
is checked in. This script does not start ZXDoc or CANoe.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "Config" / "E2E_Rules_Manifest.json"


NOMINAL_RAW = {
    "VehMtnSt": {
        "VehMtnSt": 4,
    },
    "PreCrashFrontData": {
        "ClosingVelocity": 72,
        "ObjectClass": 1,
        "OverLap": 100,
        "TimeToImpact": 125,
    },
    "VehSpdLgt": {
        "VehSpdLgtA": 7104,
        "VehSpdLgtQf": 3,
    },
    "VehModMngtGlbSafe1": {
        "CarModSts1": 0,
        "CarModSubtypWdCarModSubtyp": 0,
        "EgyLvlElecMai": 8,
        "EgyLvlElecSubtyp": 2,
        "FltEgyCnsWdSts": 0,
        "PwrLvlElecMai": 8,
        "PwrLvlElecSubtyp": 2,
        "UsgModSts": 13,
    },
    "PassAirbLampStsRec": {
        "PassAirbLampSts": 2,
    },
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    digest.update(path.read_bytes())
    return digest.hexdigest().upper()


def load_module(path: Path):
    spec = importlib.util.spec_from_file_location("zxdoc_peer_tx_golden", str(path))
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to import {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def git_commit(repo: Path) -> str:
    proc = subprocess.run(
        ["git", "-C", str(repo), "rev-parse", "HEAD"],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    return proc.stdout.strip()


def raw_maximum(element: dict) -> int:
    factor = float(element["factor"])
    offset = float(element["offset"])
    maximum = float(element["maximum"])
    value = round((maximum - offset) / factor)
    return min(value, (1 << int(element["length"])) - 1)


def find_zxdoc_element(group: dict, application_element: str) -> dict:
    for element in group["elements"]:
        key = element.get("application_element", element["name"])
        if key == application_element:
            return element
    raise KeyError(f"ZXDoc element not found: {group['name']}/{application_element}")


def hex_bytes(data: bytes | bytearray) -> str:
    return " ".join(f"{value:02X}" for value in data)


def build_vectors(manifest: dict, zxdoc, source_root: Path) -> dict:
    vectors = []
    case_definitions = (
        ("DEFAULT_C0", 0, "default"),
        ("NOMINAL_C7", 7, "nominal"),
        ("BOUNDARY_C14", 14, "boundary"),
    )

    for pdu in manifest["tx_pdus"]:
        can_id = int(pdu["can_id"])
        frame = zxdoc.RULES.get(can_id)
        if frame is None:
            raise KeyError(f"ZXDoc Tx frame not found: {pdu['can_id_hex']}")
        if frame["name"] != pdu["frame"] or int(frame["dlc"]) != int(pdu["dlc"]):
            raise ValueError(f"Frame baseline differs for {pdu['can_id_hex']}")
        if len(frame["groups"]) != 1:
            raise ValueError(f"Expected one Tx group in {pdu['can_id_hex']}")

        group = frame["groups"][0]
        if group["name"] != pdu["group"] or int(group["data_id"]) != int(pdu["data_id"]):
            raise ValueError(f"E2E group baseline differs for {pdu['can_id_hex']}")

        for case_name, counter, value_kind in case_definitions:
            input_payload = bytearray(int(pdu["dlc"]))
            zxdoc._set_raw(input_payload, group["ub"], 1)
            application_raw = {}
            application_physical = {}

            for element in pdu["elements"]:
                app_name = element["application_element"]
                if value_kind == "default":
                    raw = int(element["default_raw"])
                elif value_kind == "nominal":
                    raw = int(NOMINAL_RAW[pdu["group"]][app_name])
                else:
                    raw = raw_maximum(element)

                zx_element = find_zxdoc_element(group, app_name)
                zxdoc._set_raw(input_payload, zx_element, raw)
                application_raw[app_name] = raw
                application_physical[app_name] = round(
                    raw * float(element["factor"]) + float(element["offset"]), 8
                )

            initial_payload = bytes(input_payload)
            state = {(can_id, group["name"]): counter}
            protected = zxdoc.protect_frame(can_id, input_payload, state)
            crc_input = zxdoc.build_crc_input(protected, group, counter)
            expected_crc = zxdoc._get_raw(protected, group["crc"])
            actual_counter = zxdoc._get_raw(protected, group["counter"])
            actual_ub = zxdoc._get_raw(protected, group["ub"])

            if actual_counter != counter or actual_ub != 1:
                raise AssertionError(f"ZXDoc field round-trip failed: {pdu['can_id_hex']}/{case_name}")
            if expected_crc != zxdoc._crc8_j1850_zero(crc_input):
                raise AssertionError(f"ZXDoc CRC round-trip failed: {pdu['can_id_hex']}/{case_name}")

            vectors.append(
                {
                    "id": f"GV-{pdu['can_id_hex'][2:]}-{case_name}",
                    "case": case_name,
                    "can_id": can_id,
                    "can_id_hex": pdu["can_id_hex"],
                    "frame": pdu["frame"],
                    "pdu": pdu["pdu"],
                    "group": pdu["group"],
                    "data_id": int(pdu["data_id"]),
                    "counter": counter,
                    "ub": 1,
                    "application_raw": application_raw,
                    "application_physical": application_physical,
                    "input_payload_hex": hex_bytes(initial_payload),
                    "crc_input_hex": hex_bytes(crc_input),
                    "expected_crc": expected_crc,
                    "expected_crc_hex": f"0x{expected_crc:02X}",
                    "expected_payload_hex": hex_bytes(protected),
                }
            )

    zxdoc_rule_path = source_root / "E2E" / "SRS3_E2E_Rules.json"
    zxdoc_peer_path = source_root / "E2E" / "CAN1" / "SRS3_E2E_CAN1_PeerTx.py"
    zxdoc_anchor_path = source_root / "E2E" / "E2E_Verify.py"
    return {
        "schema_version": 1,
        "status": "GOLDEN_GENERATED_BY_EXTERNAL_ZXDOC_AND_INDEPENDENTLY_VERIFIED",
        "vector_count": len(vectors),
        "coverage": {
            "tx_pdus": len(manifest["tx_pdus"]),
            "vectors_per_pdu": 3,
            "counters": [0, 7, 14],
            "cases": ["default", "nominal", "valid-boundary"],
        },
        "source": {
            "repository": "https://github.com/wuzeyu1204/GEEA3.5_SRS3_E2E_ZXDoc",
            "commit": git_commit(source_root),
            "peer_tx": "E2E/CAN1/SRS3_E2E_CAN1_PeerTx.py",
            "peer_tx_sha256": sha256(zxdoc_peer_path),
            "rules": "E2E/SRS3_E2E_Rules.json",
            "rules_sha256": sha256(zxdoc_rule_path),
            "anchor": "E2E/E2E_Verify.py",
            "anchor_sha256": sha256(zxdoc_anchor_path),
            "canoe_manifest_sha256": sha256(MANIFEST_PATH),
        },
        "external_anchor": {
            "description": "ZXDoc legacy known-answer vector for 0x076",
            "can_id_hex": "0x076",
            "counter": 0,
            "application_raw": {"VehSpdLgtA": 0, "VehSpdLgtQf": 3},
            "crc_input_hex": "37 00 00 00 00 03",
            "expected_crc": 212,
            "expected_crc_hex": "0xD4",
        },
        "vectors": vectors,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--zxdoc-root", type=Path, required=True)
    args = parser.parse_args()

    source_root = args.zxdoc_root.resolve()
    peer_tx = source_root / "E2E" / "CAN1" / "SRS3_E2E_CAN1_PeerTx.py"
    if not peer_tx.is_file():
        parser.error(f"ZXDoc peer transmitter not found: {peer_tx}")

    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    zxdoc = load_module(peer_tx)
    result = build_vectors(manifest, zxdoc, source_root)
    json.dump(result, sys.stdout, indent=2, ensure_ascii=False)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
