"""Independent N05 §§3–5 TCP ID/ping observation, not N01 joining or gameplay.

Start the unmodified original host through its normal UI first. This opt-in
driver only connects to an explicit IPv4 endpoint; ordinary tests never run it.
It derives bytes from this repository's wire contract, not a historical harness.
"""

import argparse
import hashlib
import ipaddress
import json
from pathlib import Path
import platform
import socket
import struct
import subprocess
import time
from datetime import datetime, timezone


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def frame(tag, payload):
    total = (12 + len(payload) + 63) & ~63
    return struct.pack("<III", total, tag, len(payload)) + payload + bytes(total - 12 - len(payload))


def observe(args, report):
    stream = bytearray()
    pending = bytearray()
    records = []
    report["records"] = records
    started = time.monotonic()
    deadline = started + args.timeout
    ping_payload = None
    with socket.create_connection((args.host, args.port), args.timeout) as peer:
        peer.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        report["connectedMs"] = (time.monotonic() - started) * 1000
        while time.monotonic() < deadline:
            peer.settimeout(max(0.001, deadline - time.monotonic()))
            chunk = peer.recv(4096)
            if not chunk:
                raise RuntimeError("Original closed before a complete ID/ping exchange")
            stream.extend(chunk)
            pending.extend(chunk)
            report["receiveChunks"].append({"bytes": len(chunk), "elapsedMs": (time.monotonic() - started) * 1000})
            if len(stream) > 8 * 1024 * 1024:
                raise RuntimeError("Capture exceeded the declared 8 MiB backing limit")
            report["receivedStreamHex"] = stream.hex()
            while len(pending) >= 12:
                total, tag, size = struct.unpack_from("<III", pending)
                if total < 12 or total > 8 * 1024 * 1024 or size > total - 12:
                    raise RuntimeError("Original record is outside admitted frame domain")
                if len(pending) < total:
                    break
                wire = bytes(pending[:total])
                del pending[:total]
                payload = wire[12:12 + size]
                records.append({"totalBytes": total, "tag": f"0x{tag:08x}", "payloadBytes": size})
                if tag == 0xFA000000 and ping_payload is None:
                    if len(payload) != 2:
                        raise RuntimeError("ID greeting payload is not two bytes")
                    connection_id = struct.unpack("<H", payload)[0]
                    if not 1 <= connection_id <= 6:
                        raise RuntimeError("ID is outside the nominal seven-slot host domain")
                    report["connectionId"] = connection_id
                    report["greetingMs"] = (time.monotonic() - started) * 1000
                    # Fully authored payload/padding. Three-byte writes exercise
                    # fragmented supply, without claiming OS receive boundaries.
                    ping_payload = payload + bytes(range(2, 64))
                    request = frame(0xFC000000, ping_payload)
                    report["pingRequestHex"] = request.hex()
                    for offset in range(0, len(request), 3):
                        peer.sendall(request[offset:offset + 3])
                elif tag == 0xFD000000:
                    if ping_payload is None or payload != ping_payload:
                        raise RuntimeError("Ping reply did not preserve the supplied payload")
                    report["pingPayloadEqual"] = True
            if report.get("pingPayloadEqual") and not pending:
                wire_path = args.output / "received-stream.bin"
                wire_path.write_bytes(stream)
                report["wireSha256"] = sha256(wire_path)
                result = subprocess.run([str(args.checker.resolve()), str(wire_path.resolve())],
                                        capture_output=True, text=True, timeout=args.timeout, check=True)
                report["cleanroomCodecCheck"] = json.loads(result.stdout)
                return
    raise TimeoutError("No complete ID/ping exchange before deadline")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", required=True, type=lambda value: str(ipaddress.IPv4Address(value)))
    parser.add_argument("--port", type=int, default=4993)
    parser.add_argument("--timeout", type=float, default=10)
    parser.add_argument("--original-exe", type=Path, required=True)
    parser.add_argument("--original-transport", type=Path, required=True)
    parser.add_argument("--checker", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="New capture directory; never overwritten")
    args = parser.parse_args()
    if not 1 <= args.port <= 65535 or not 0 < args.timeout <= 60:
        parser.error("Port must be 1..65535 and timeout must be greater than zero and at most 60 seconds")
    report = {"schemaVersion": 1, "evidence": "independent-original-program-observation",
              "scope": "N05 TCP ID greeting and ping echo; no N01 admission, discovery or gameplay",
              "utc": datetime.now(timezone.utc).isoformat(), "platform": platform.platform(),
              "endpoint": {"host": args.host, "port": args.port},
              "originalExeSha256": sha256(args.original_exe),
              "originalTransportSha256": sha256(args.original_transport),
              "driverSha256": sha256(__file__), "checkerSha256": sha256(args.checker),
              "receiveChunks": [], "passed": False}
    args.output.mkdir(parents=True, exist_ok=False)
    try:
        observe(args, report)
        report["passed"] = True
    except (OSError, RuntimeError, ValueError, subprocess.SubprocessError) as error:
        report["error"] = str(error)
        if report.get("receivedStreamHex"):
            wire_path = args.output / "received-stream.bin"
            wire_path.write_bytes(bytes.fromhex(report["receivedStreamHex"]))
            report["wireSha256"] = sha256(wire_path)
    (args.output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"passed": report["passed"], "report": str(args.output / "report.json"),
                      "error": report.get("error")}))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
