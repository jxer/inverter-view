"""Decode the captured Techfine/VEVOR VPHB status protocol.

Offline inspection uses only Python's standard library. Explicit live monitoring
requires pyserial and sends only the fixed INV-HB status request. It cannot set
parameters or reset the inverter. No serial port is opened during import/inspect.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
import time
from datetime import datetime, timezone


STATUS_REQUEST = b"CMSG\x06INV-HB"
REPLY_MAGIC = b"VPHB"
MIN_PAYLOAD_LENGTH = 67  # 66 known data bytes plus the checksum


def decode_status(frame: bytes) -> dict:
    """Validate a complete VPHB frame, then apply Techfine 1.03's scaling."""
    if len(frame) < 5 or frame[:4] != REPLY_MAGIC:
        raise ValueError("Not a VPHB status frame")
    if frame[4] < MIN_PAYLOAD_LENGTH or len(frame) != 5 + frame[4]:
        raise ValueError("Invalid or incomplete status frame length")
    if sum(frame[5:-1]) & 0xFF != frame[-1]:
        raise ValueError("Status checksum mismatch")
    p = frame[5:-1]

    def u16(offset: int) -> int:
        return int.from_bytes(p[offset:offset + 2], "little")

    state_step = p[30]
    mode = {4: "Charging only", 5: "AC Mode", 6: "DC Mode"}.get(state_step, "Bypass Mode (Techfine label)")
    return {
        "header": "VPHB",
        "frame_bytes": len(frame),
        "payload_bytes_including_checksum": frame[4],
        "checksum_valid": True,
        "inverter_firmware": f"{p[9]}.{p[10]:02d}",
        "charger_firmware": f"{p[32]}.{p[33]:02d}",
        "machine_type_raw": p[11],
        "battery_12v_units": p[31],
        "machine_type_display": f"{p[11]}{p[31] * 12}",
        "ac_input_voltage_v": u16(12) / 10,
        "ac_input_frequency_hz": u16(14) / 10,
        "output_voltage_reported_v": u16(16) / 10,
        "output_frequency_hz": u16(18) / 10,
        "battery_voltage_v": u16(20) / 10,
        "battery_capacity_reported_percent": p[22],
        "ac_charge_current_a": p[23],
        "load_percent": p[24],
        "inverter_temperature_c": None if p[25] == 255 else p[25],
        "controller_code_raw": p[26],
        "controller_code_hex": f"0x{p[26]:02X}",
        "runtime_hours": round(u16(28) + p[27] / 10, 1),
        "state_step_raw": state_step,
        "operating_mode": mode,
        "pv_voltage_v": u16(34),
        "solar_charge_current_a": round(p[36] + (p[66] / 10 if len(p) > 66 else 0), 1),
        "charger_temperature_c": None if p[37] in (0, 255) else p[37],
        "charger_code_raw": p[38],
        "status_bytes_hex": p[0:3].hex(" "),
        "warning_bytes_hex": p[3:6].hex(" "),
        "fault_bytes_hex": p[6:9].hex(" "),
        "battery_full_flag": bool(p[0] & 1),
        "ac_charging_flag": bool(p[0] & 2),
        "solar_charging_flag": bool(p[0] & 4),
        "charger_connected_flag": bool(p[0] & 8),
        "controller_warning_flag": bool(p[2] & 1),
        "controller_fault_flag": bool(p[2] & 2),
        "charger_warning_flag": bool(p[2] & 4),
        "charger_fault_flag": bool(p[2] & 8),
        "work_mode_setting_raw": p[47],
        "work_mode_setting": {1: "Utility First", 2: "Solar First", 3: "DC First"}.get(p[47], "Unknown"),
        "charge_mode_setting_raw": p[48],
        "charge_mode_setting": {1: "Utility First", 2: "Solar First", 3: "Solar Only"}.get(p[48], "Unknown"),
        "ac_charge_limit_percent": p[49],
        "solar_charge_limit_percent": p[50],
        "bulk_voltage_per_12v_unit_v": p[51] / 10,
        "float_voltage_per_12v_unit_v": p[52] / 10,
        "low_lock_voltage_per_12v_unit_v": p[53] / 10,
        "dc_to_ac_voltage_per_12v_unit_v": p[54] / 10,
        "ac_to_dc_voltage_per_12v_unit_v": p[55] / 10,
        "output_voltage_setting_v": p[56],
        "output_frequency_setting_hz": {1: 50, 2: 60}.get(p[58]),
        "uart_setting_raw": p[65],
        "unmapped_extension_hex": p[67:].hex(" ") if len(p) > 67 else "",
        "raw_hex": frame.hex(" "),
    }


class StatusStream:
    """Buffer partial serial reads and recover valid replies amid other bytes."""

    def __init__(self) -> None:
        self.buffer = bytearray()
        self.rejected_candidates = 0

    def feed(self, chunk: bytes) -> list[bytes]:
        self.buffer.extend(chunk)
        frames = []
        while True:
            start = self.buffer.find(REPLY_MAGIC)
            if start < 0:
                self.buffer[:] = self.buffer[-3:]
                break
            del self.buffer[:start]
            if len(self.buffer) < 5:
                break
            length = self.buffer[4]
            if length < MIN_PAYLOAD_LENGTH:
                self.rejected_candidates += 1
                del self.buffer[0]
                continue
            end = 5 + length
            if len(self.buffer) < end:
                # A damaged length may precede a later, complete valid frame.
                # Resynchronize only when that later candidate passes validation.
                later = self.buffer.find(REPLY_MAGIC, 1)
                recovered = False
                while later >= 0:
                    if len(self.buffer) >= later + 5:
                        later_end = later + 5 + self.buffer[later + 4]
                        if later_end <= len(self.buffer):
                            try:
                                decode_status(bytes(self.buffer[later:later_end]))
                            except ValueError:
                                pass
                            else:
                                del self.buffer[:later]
                                self.rejected_candidates += 1
                                recovered = True
                                break
                    later = self.buffer.find(REPLY_MAGIC, later + 1)
                if recovered:
                    continue
                break
            frame = bytes(self.buffer[:end])
            try:
                decode_status(frame)
            except ValueError:
                self.rejected_candidates += 1
                del self.buffer[0]
            else:
                frames.append(frame)
                del self.buffer[:end]
        return frames


def extract_frames(data: bytes) -> tuple[list[tuple[int, bytes]], list[dict]]:
    """Find contiguous checksum-valid replies in this log or a raw byte stream.

    This is a signature extractor, not a general DMSLOG8 container parser. It
    cannot recover compressed/split payloads, infer IRP direction or deduplicate
    driver records. Offsets are file positions, not timestamps.
    """
    frames, rejected = [], []
    cursor = 0
    while True:
        start = data.find(REPLY_MAGIC, cursor)
        if start < 0:
            break
        if start + 5 > len(data):
            rejected.append({"source_offset": start, "reason": "Truncated header"})
            break
        end = start + 5 + data[start + 4]
        frame = data[start:end]
        try:
            decode_status(frame)
        except ValueError as exc:
            rejected.append({"source_offset": start, "reason": str(exc)})
            cursor = start + 1
        else:
            frames.append((start, frame))
            cursor = end
    return frames, rejected


def inspect_capture(source: Path, out_dir: Path | None) -> int:
    data = source.read_bytes()
    frames, rejected = extract_frames(data)
    records = [dict(capture_frame_index=i, source_offset=offset, **decode_status(frame))
               for i, (offset, frame) in enumerate(frames, 1)]
    varied_fields = {}
    if records:
        for key in records[0]:
            if key in {"capture_frame_index", "source_offset", "raw_hex"}:
                continue
            values = list(dict.fromkeys(r[key] for r in records))
            if len(values) > 1:
                varied_fields[key] = sorted(values, key=lambda v: (v is None, v))
    summary = {
        "source_file": str(source.resolve()),
        "source_sha256": hashlib.sha256(data).hexdigest(),
        "source_bytes": len(data),
        "valid_status_frames": len(frames),
        "rejected_status_candidates": rejected,
        "distinct_status_frames": len({f for _, f in frames}),
        "status_request_hex": STATUS_REQUEST.hex(" "),
        "status_request_copies_in_container": data.count(STATUS_REQUEST),
        "request_count_note": "Copies may include both submit/completion records; this is not a wire transaction count.",
        "extraction_note": "Contiguous signature extraction with checksums; not a complete DMSLOG8 parser. No container timestamps inferred.",
        "varying_fields": varied_fields,
        "first_reading": records[0] if records else None,
        "last_reading": records[-1] if records else None,
    }
    if out_dir is not None:
        out_dir.mkdir(parents=True, exist_ok=True)
        targets = [out_dir / name for name in (
            "capture-summary.json", "decoded-readings.jsonl", "recorded-replies.bin", "recorded-replies.txt")]
        if any(path.exists() for path in targets):
            raise ValueError("An output file already exists; choose a new --out directory")
        targets[0].write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
        targets[1].write_text("".join(json.dumps(r) + "\n" for r in records), encoding="utf-8")
        targets[2].write_bytes(b"".join(f for _, f in frames))
        targets[3].write_text("".join(f"{i:03d} source_offset={offset} {frame.hex(' ')}\n"
                                      for i, (offset, frame) in enumerate(frames, 1)), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0 if frames and not rejected else 2


def monitor(port_name: str, baud: int, count: int, interval: float, destination: Path | None) -> int:
    if count < 1 or interval < 1:
        raise ValueError("--count must be positive and --interval must be at least 1 second")
    try:
        import serial
    except ImportError as exc:
        raise ValueError("Live monitoring requires pyserial: python -m pip install pyserial") from exc

    stream = StatusStream()
    output = destination.open("x", encoding="utf-8") if destination else None
    port = serial.Serial()
    port.port = port_name
    port.baudrate = baud
    port.bytesize = serial.EIGHTBITS
    port.parity = serial.PARITY_NONE
    port.stopbits = serial.STOPBITS_ONE
    port.timeout = 0.2
    port.write_timeout = 1
    port.xonxoff = False
    port.rtscts = False
    port.dsrdtr = False
    port.dtr = False
    port.rts = False
    try:
        port.open()
        port.reset_input_buffer()
        for query in range(count):
            started = time.monotonic()
            sent = port.write(STATUS_REQUEST)  # The only serial write in this program.
            if sent != len(STATUS_REQUEST):
                raise ValueError("The status request was not fully written")
            port.flush()
            deadline = time.monotonic() + 3
            replies = []
            while time.monotonic() < deadline and not replies:
                replies = stream.feed(port.read(max(1, min(port.in_waiting, 4096))))
            if not replies:
                raise ValueError(f"No valid status reply after query {query + 1}; check port and baud rate")
            for frame in replies:
                record = dict(received_at_utc=datetime.now(timezone.utc).isoformat(), **decode_status(frame))
                line = json.dumps(record)
                print(line, flush=True)
                if output:
                    output.write(line + "\n")
                    output.flush()
            if query + 1 < count:
                time.sleep(max(0, interval - (time.monotonic() - started)))
    finally:
        port.close()
        if output:
            output.close()
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    inspect = commands.add_parser("inspect", help="Decode a saved capture without opening a serial port")
    inspect.add_argument("input", type=Path)
    inspect.add_argument("--out", type=Path, help="New directory for JSON, raw frames and a hex listing")
    live = commands.add_parser("monitor", help="Poll status only; close Techfine first to release the COM port")
    live.add_argument("--port", required=True)
    live.add_argument("--baud", type=int, choices=(2400, 4800, 9600), default=2400)
    live.add_argument("--count", type=int, default=10, help="Number of status queries; default 10")
    live.add_argument("--interval", type=float, default=1.0, help="Seconds between requests; minimum 1")
    live.add_argument("--jsonl", type=Path, help="Create a new live readings log")
    args = parser.parse_args()
    try:
        if args.command == "inspect":
            return inspect_capture(args.input, args.out)
        return monitor(args.port, args.baud, args.count, args.interval, args.jsonl)
    except KeyboardInterrupt:
        return 130
    except (OSError, ValueError) as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
