#!/usr/bin/env python3
"""Build the optional stock Lua bridge; no gameplay code crosses this native ABI."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
BRIDGE = ROOT / "native/lua"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inputs():
    return {str(p.relative_to(ROOT)): sha(p) for p in [BRIDGE / "bridge.c", BRIDGE / "bridge.h", Path(__file__).resolve()]}


def runtime(path, version):
    library = ctypes.CDLL(str(path))
    library.oc2_lua_version.restype = ctypes.c_char_p
    library.oc2_lua_abi.restype = ctypes.c_uint32
    if library.oc2_lua_version() != ("Lua " + version).encode("ascii") or library.oc2_lua_abi() != 1:
        raise SystemExit("Lua runtime version/ABI mismatch")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path)
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--sanitize", action="store_true", help="Run standalone bridge tests with address/undefined sanitizers (Unix)")
    parser.add_argument("--output", type=Path, default=ROOT / "build/lua/native")
    parser.add_argument("--cc", default=os.environ.get("CC", "cl" if os.name == "nt" else "cc"))
    args = parser.parse_args()
    pin = json.loads((ROOT / "dependencies.json").read_text())["libraries"]["Lua"]
    system = platform.system()
    filename = {"Linux": "liboc2_lua.so", "Darwin": "liboc2_lua.dylib", "Windows": "oc2_lua.dll"}.get(system)
    if filename is None:
        raise SystemExit("Unsupported native Lua platform")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    installed = output / filename
    expected = {"format": "openciv2-managed-lua-build/1", "version": pin["version"], "abi": 1,
                "archiveSha256": pin["sha256"], "inputs": inputs(), "library": filename,
                "platform": system, "machine": platform.machine()}
    if args.verify:
        report = json.loads((output / "oc2-lua-build.json").read_text())
        if any(report.get(k) != v for k, v in expected.items()) or report.get("librarySha256") != sha(installed):
            raise SystemExit("Lua native receipt is stale or does not match pinned inputs/artifact")
        runtime(installed, pin["version"])
        print("Verified pinned Lua bridge: " + str(installed))
        return
    archive = (args.archive or output / Path(pin["url"]).name).resolve()
    if not archive.exists():
        if args.archive:
            raise SystemExit("Offline archive does not exist")
        with urllib.request.urlopen(pin["url"], timeout=60) as response:
            archive.write_bytes(response.read())
    if sha(archive) != pin["sha256"]:
        raise SystemExit("Lua archive does not match dependencies.json SHA-256")
    compiler = shutil.which(args.cc)
    if not compiler:
        raise SystemExit("Native C compiler not found: " + args.cc)
    with tempfile.TemporaryDirectory(prefix="lua-build-", dir=output) as directory:
        work = Path(directory)
        # Extract only regular, immediate source files into a flat directory. Never
        # trust archive paths, symlinks, or executable tools from the dependency.
        prefix = "lua-" + pin["version"] + "/src/"
        with tarfile.open(archive) as bundle:
            for entry in bundle.getmembers():
                if entry.isfile() and entry.name.startswith(prefix):
                    name = entry.name[len(prefix):]
                    if "/" not in name and name.endswith((".c", ".h")):
                        (work / name).write_bytes(bundle.extractfile(entry).read())
        sources = sorted(p for p in work.glob("*.c") if p.name not in {"lua.c", "luac.c"})
        if not sources or not (work / "lua.h").exists():
            raise SystemExit("Lua source archive is incomplete")
        source_hashes = {p.name: sha(p) for p in sorted(work.glob("*"))}
        target = work / filename
        common = [str(BRIDGE / "bridge.c"), *map(str, sources)]
        if system == "Windows":
            command = [compiler, "/nologo", "/O2", "/LD", "/std:c11", "/I" + str(work), *common, "/link", "/OUT:" + str(target)]
            version = subprocess.run([compiler], capture_output=True, text=True)
        else:
            command = [compiler, "-O2", "-std=c11", "-fPIC", "-fvisibility=hidden", "-dynamiclib" if system == "Darwin" else "-shared",
                       "-I" + str(work), *common, "-o", str(target), "-lm"]
            if system == "Darwin":
                command += ["-Wl,-install_name,@rpath/" + filename]
            version = subprocess.run([compiler, "--version"], capture_output=True, text=True, check=True)
        subprocess.run(command, cwd=work, check=True)
        if args.sanitize:
            if system == "Windows":
                raise SystemExit("Sanitizer runner requires a Unix compiler; native managed tests still apply on Windows")
            probe = work / "bridge-tests"
            test_command = [compiler, "-O1", "-g", "-std=c11", "-fsanitize=address,undefined", "-fno-omit-frame-pointer",
                            "-I" + str(work), "-I" + str(BRIDGE), *common, str(ROOT / "tests/lua_bridge.c"), "-o", str(probe), "-lm"]
            subprocess.run(test_command, cwd=work, check=True)
            subprocess.run([str(probe)], check=True, timeout=30, env={**os.environ, "ASAN_OPTIONS": "detect_leaks=1:abort_on_error=1", "UBSAN_OPTIONS": "halt_on_error=1"})
        shutil.copyfile(target, installed)
        # The pinned source header carries Lua's full copyright and permission notice.
        (output / "LUA-NOTICE.txt").write_bytes((work / "lua.h").read_bytes())
    runtime(installed, pin["version"])
    report = {**expected, "librarySha256": sha(installed), "sourceSha256": source_hashes,
              "compiler": compiler, "compilerVersion": (version.stdout + version.stderr).strip(), "command": command}
    (output / "oc2-lua-build.json").write_text(json.dumps(report, indent=2) + "\n")
    print("Built and verified stock Lua bridge: " + str(installed))


if __name__ == "__main__":
    main()
