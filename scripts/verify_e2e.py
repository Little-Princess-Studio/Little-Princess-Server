"""End-to-end smoke test for the Little Princess Server cluster.

Workflow:
  1. Kill any leftover LPS.Server.Demo / LPS.Client.Demo processes.
  2. dotnet build the whole solution.
  3. Start the cluster headlessly (LPS.Server.Demo bydefault --headless),
     keeping stdin open so we can later send "shutdown\\n" for graceful exit.
  4. Wait until the cluster is fully wired (looking for the 'All other gates
     connected.' marker in the headless log).
  5. Run LPS.Client.Demo with a scenario file and capture its stdout/stderr.
  6. Send "shutdown\\n" to the launcher's stdin, then wait for it to exit
     gracefully. Force-kill anything left over.
  7. Verify:
       * the cluster log contains every expected process tag,
       * the scenario completed every step,
       * no [Error]/[Fatal]/Unhandled Exception was logged BEFORE the
         shutdown marker (post-shutdown socket-resets are filtered out).
"""

from __future__ import annotations

import os
import re
import subprocess
import sys
import time
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
SERVER_DIR = ROOT / "LPS.Server.Demo"
CLIENT_DIR = ROOT / "LPS.Client.Demo"
LOG_FILE = SERVER_DIR / "logs" / "headless_run.log"
SCENARIO = "Config/demo_scenario.json"

EXPECTED_TAGS = ["[hostmanager]", "[dbmanager]", "[gate0]", "[server0]"]
READY_MARKER = "All other gates connected."
SHUTDOWN_MARKER = "[StartupManager] Shutdown signal received"

ERROR_RX = re.compile(r"\[(Error|Fatal)\]|Unhandled Exception")
BENIGN_RX = re.compile(
    r"Read socket data failed.*forcibly closed"
    r"|Send msg .* failed"
)


def kill_leftovers() -> None:
    print("Stopping any leftover LPS processes...")
    if sys.platform == "win32":
        cmd = (
            'powershell -Command "Get-CimInstance Win32_Process | '
            "Where-Object { $_.CommandLine -like '*LPS.Server.Demo.dll*' "
            "-or $_.CommandLine -like '*LPS.Client.Demo.dll*' } | "
            'ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"'
        )
        subprocess.run(cmd, shell=True, check=False)
    else:
        subprocess.run("pkill -9 -f LPS.Server.Demo.dll", shell=True, check=False)
        subprocess.run("pkill -9 -f LPS.Client.Demo.dll", shell=True, check=False)
    time.sleep(2)


def build() -> None:
    print("Building solution...")
    res = subprocess.run(
        ["dotnet", "build"], cwd=str(ROOT),
        capture_output=True, encoding="utf-8", errors="replace", check=False,
    )
    if res.returncode != 0:
        print("Build failed!\n" + res.stdout + "\n" + res.stderr)
        sys.exit(1)
    print("Build OK.")


def start_cluster() -> subprocess.Popen:
    LOG_FILE.parent.mkdir(parents=True, exist_ok=True)
    if LOG_FILE.exists():
        LOG_FILE.unlink()

    print(f"Launching cluster headlessly, log -> {LOG_FILE}")
    log_fp = open(LOG_FILE, "wb")
    return subprocess.Popen(
        ["dotnet", "run", "--no-build", "--", "bydefault", "--headless"],
        cwd=str(SERVER_DIR),
        stdin=subprocess.PIPE,
        stdout=log_fp,
        stderr=subprocess.STDOUT,
    )


def wait_ready(timeout_s: int = 60) -> bool:
    for _ in range(timeout_s):
        if LOG_FILE.exists():
            try:
                content = LOG_FILE.read_text(encoding="utf-8", errors="replace")
            except OSError:
                content = ""
            if READY_MARKER in content:
                return True
        time.sleep(1)
    return False


def run_scenario(timeout_s: int = 90) -> subprocess.CompletedProcess:
    print(f"Running scenario {SCENARIO}...")
    try:
        return subprocess.run(
            ["dotnet", "run", "--no-build", "--", "--scenario", SCENARIO],
            cwd=str(CLIENT_DIR),
            capture_output=True, encoding="utf-8", errors="replace",
            check=False, timeout=timeout_s,
        )
    except subprocess.TimeoutExpired as e:
        print(f"  WARNING: scenario hit {timeout_s}s timeout, killing client.")
        # Best-effort kill of the client process tree.
        if sys.platform == "win32":
            subprocess.run(
                'powershell -Command "Get-CimInstance Win32_Process | '
                "Where-Object { $_.CommandLine -like '*LPS.Client.Demo.dll*' } | "
                'ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"',
                shell=True, check=False,
            )
        else:
            subprocess.run("pkill -9 -f LPS.Client.Demo.dll", shell=True, check=False)
        return subprocess.CompletedProcess(
            args=e.cmd, returncode=-1,
            stdout=(e.stdout or b"").decode("utf-8", errors="replace") if isinstance(e.stdout, bytes) else (e.stdout or ""),
            stderr=(e.stderr or b"").decode("utf-8", errors="replace") if isinstance(e.stderr, bytes) else (e.stderr or ""),
        )


def graceful_shutdown(proc: subprocess.Popen, timeout_s: int = 15) -> None:
    print("Sending shutdown signal to cluster launcher...")
    try:
        if proc.stdin and not proc.stdin.closed:
            proc.stdin.write(b"shutdown\n")
            proc.stdin.flush()
            proc.stdin.close()
    except Exception as e:
        print(f"  (stdin write failed: {e})")

    try:
        proc.wait(timeout=timeout_s)
        print(f"  Launcher exited cleanly with code {proc.returncode}")
    except subprocess.TimeoutExpired:
        print(f"  Launcher did not exit within {timeout_s}s, force killing.")
        proc.kill()
        try:
            proc.wait(timeout=5)
        except Exception:
            pass

    kill_leftovers()


def verify_logs(scenario_stdout: str) -> int:
    if not LOG_FILE.exists():
        print("ERROR: server log missing.")
        return 1

    logs = LOG_FILE.read_text(encoding="utf-8", errors="replace")

    shutdown_idx = logs.find(SHUTDOWN_MARKER)
    pre_shutdown_logs = logs if shutdown_idx == -1 else logs[:shutdown_idx]

    print("\n=== Verifying ===")

    issues: list[str] = []

    for tag in EXPECTED_TAGS:
        present = tag in logs
        print(f"  log contains {tag}: {present}")
        if not present:
            issues.append(f"missing tag {tag}")

    pre_errors = [
        line for line in pre_shutdown_logs.splitlines()
        if ERROR_RX.search(line) and not BENIGN_RX.search(line)
    ]
    if pre_errors:
        issues.append(f"{len(pre_errors)} pre-shutdown error log line(s)")
        print(f"  pre-shutdown errors: {len(pre_errors)}")
        for line in pre_errors[:10]:
            print("    " + line[:200])
    else:
        print("  pre-shutdown errors: 0")

    executed = re.findall(r"Scenario Executing: (.+)", scenario_stdout)
    scenario_path = CLIENT_DIR / SCENARIO
    scenario_text = scenario_path.read_text(encoding="utf-8")
    expected_count = scenario_text.count('"command"')
    print(f"  scenario steps executed: {len(executed)} / {expected_count}")
    if len(executed) < expected_count:
        issues.append(
            f"only {len(executed)}/{expected_count} scenario steps ran "
            "(client probably crashed mid-scenario)"
        )

    if issues:
        print("\nFAIL: " + "; ".join(issues))
        return 1

    print("\nPASS")
    return 0


def main() -> int:
    os.chdir(str(ROOT))
    kill_leftovers()
    build()

    cluster = start_cluster()
    scenario: subprocess.CompletedProcess | None = None
    try:
        if not wait_ready(60):
            print("ERROR: cluster failed to come up within 60s.")
            if LOG_FILE.exists():
                print(LOG_FILE.read_text(encoding="utf-8", errors="replace"))
            return 1

        print("Cluster ready.")
        scenario = run_scenario()
        print("\n=== Client stdout (tail) ===")
        print("\n".join(scenario.stdout.splitlines()[-30:]))
        if scenario.stderr.strip():
            print("\n=== Client stderr ===")
            print(scenario.stderr)
    finally:
        graceful_shutdown(cluster)

    rc = verify_logs(scenario.stdout if scenario else "")
    if scenario and scenario.returncode != 0 and rc == 0:
        print(f"  (client exit code was {scenario.returncode}, ignoring since logs are clean.)")
    return rc


if __name__ == "__main__":
    sys.exit(main())
