"""Opt-in N01 section 5 / N02 Appendix A / N05 section 6 UDP host observation.

Send one authored GetServers datagram to an explicit IPv4 host. Listen on the
classic discovery port for its broadcast advertisement. No N01 player admission
or proprietary harness, process injection or executable patching is involved.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import ipaddress
import json
from pathlib import Path
import platform
import socket
import struct
import subprocess
import time


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def observe(args, report):
    # Fully authored padding and empty target/exclusion strings. Header zero is
    # this probe's supplied N05 input, not an inferred original producer value.
    address = b"127.0.0.1" + bytes(23)
    message = struct.pack("<IIII", 0x66606660, 0, 116, 0) + address + bytes(64) + struct.pack("<I", 1)
    request = struct.pack("<I", 0) + message
    report["requestHex"] = request.hex()
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as listener, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sender:
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind(("", args.port))
        started = time.monotonic()
        deadline = started + args.timeout
        sender.sendto(request, (args.host, args.port))
        while time.monotonic() < deadline:
            listener.settimeout(max(0.001, deadline - time.monotonic()))
            wire, _ = listener.recvfrom(1024)
            report["observedLengths"].append(len(wire))
            if len(wire) != 412:
                continue
            header, magic, kind, size, sequence = struct.unpack_from("<IIIII", wire)
            if magic != 0x66606660 or kind != 0 or size != 408 or struct.unpack_from("<I", wire, 116)[0] != 2:
                continue
            # N02 Appendix A game-info starts at datagram offset 120; game and
            # host labels at its 0x70/0x90. Match the explicit UI test label.
            name = wire[232:264].split(b"\0", 1)[0].decode("ascii", errors="replace")
            if name != args.game_name:
                continue
            path = args.output / "advertisement.bin"
            path.write_bytes(wire)
            report.update({"replyHex": wire.hex(), "replySha256": digest(path),
                           "elapsedMs": (time.monotonic() - started) * 1000,
                           "outerHeader": header, "sequence": sequence, "gameName": name,
                           "version": wire[168:232].split(b"\0", 1)[0].decode("ascii", errors="replace")})
            result = subprocess.run([str(args.checker.resolve()), "--datagram", str(path.resolve())],
                                    capture_output=True, text=True, timeout=args.timeout, check=True)
            report["cleanroomCodecCheck"] = json.loads(result.stdout)
            return
    raise TimeoutError("No matching original advertisement before deadline")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", required=True, type=lambda value: str(ipaddress.IPv4Address(value)))
    parser.add_argument("--port", type=int, default=4994)
    parser.add_argument("--game-name", required=True)
    parser.add_argument("--timeout", type=float, default=10)
    parser.add_argument("--original-exe", type=Path, required=True)
    parser.add_argument("--original-transport", type=Path, required=True)
    parser.add_argument("--checker", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if not 1 <= args.port <= 65535 or not 0 < args.timeout <= 60:
        parser.error("Port must be 1..65535 and timeout in (0,60] seconds")
    report = {"schemaVersion": 1, "evidence": "independent-original-program-observation",
              "scope": "One UDP GetServers request and matching ServerInfo; no lobby admission",
              "utc": datetime.now(timezone.utc).isoformat(), "platform": platform.platform(),
              "endpoint": {"host": args.host, "port": args.port},
              "originalExeSha256": digest(args.original_exe),
              "originalTransportSha256": digest(args.original_transport),
              "driverSha256": digest(__file__), "checkerSha256": digest(args.checker),
              "observedLengths": [], "passed": False}
    args.output.mkdir(parents=True, exist_ok=False)
    try:
        observe(args, report)
        report["passed"] = True
    except (OSError, RuntimeError, ValueError, subprocess.SubprocessError) as error:
        report["error"] = str(error)
    (args.output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"passed": report["passed"], "report": str(args.output / "report.json"),
                      "error": report.get("error")}))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
