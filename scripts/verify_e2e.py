import subprocess
import time
import os
import sys

# Configure UTF-8 encoding for standard output and error to avoid GBK UnicodeEncodeError on Windows
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

def stop_server():
    print("Stopping all LPS.Server.Demo processes...")
    if sys.platform == "win32":
        cmd = 'powershell -Command "Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like \'*LPS.Server.Demo.dll*\' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"'
        subprocess.run(cmd, shell=True)
    else:
        subprocess.run("pkill -9 -f LPS.Server.Demo.dll", shell=True)
    time.sleep(2)

def main():
    # Make sure we are in the root directory of the workspace
    # Root has LPS.sln, LPS.Server.Demo/, LPS.Client.Demo/
    os.chdir(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

    # Clean up any leftover processes first
    stop_server()

    # Build the project
    print("Building Solution...")
    build_res = subprocess.run(["dotnet", "build"], capture_output=True, encoding="utf-8", errors="replace")
    if build_res.returncode != 0:
        print("Build failed!")
        print(build_res.stdout)
        print(build_res.stderr)
        sys.exit(1)
    print("Build successful.")

    # Ensure log directory exists
    os.makedirs("LPS.Server.Demo/logs", exist_ok=True)
    log_file_path = "LPS.Server.Demo/logs/headless_run.log"
    if os.path.exists(log_file_path):
        os.remove(log_file_path)

    # Note: open output log file relative to script workspace
    print(f"Starting server headlessly. Output will be written to {log_file_path}...")
    with open(log_file_path, "wb") as f_log:
        server_proc = subprocess.Popen(
            ["dotnet", "run", "--no-build", "--", "bydefault", "--headless"],
            cwd="LPS.Server.Demo",
            stdout=f_log,
            stderr=subprocess.STDOUT
        )

    # Wait for server to boot (up to 60 seconds)
    print("Waiting for server cluster to initialize...")
    started = False
    for i in range(60):
        if os.path.exists(log_file_path):
            with open(log_file_path, "r", encoding="utf-8", errors="replace") as f:
                content = f.read()
                if "All other gates connected." in content:
                    started = True
                    break
        time.sleep(1)

    if not started:
        print("Error: Server cluster failed to start within 60 seconds.")
        # Print logs for diagnostics
        if os.path.exists(log_file_path):
            print("\n=== Log Diagnostics ===")
            with open(log_file_path, "r", encoding="utf-8", errors="replace") as f:
                print(f.read())
        stop_server()
        sys.exit(1)

    print("Server cluster initialized successfully. Running client scenario...")
    client_res = subprocess.run(
        ["dotnet", "run", "--no-build", "--", "--scenario", "Config/demo_scenario.json"],
        cwd="LPS.Client.Demo",
        capture_output=True,
        encoding="utf-8",
        errors="replace"
    )

    print("\n=== Client Output ===")
    print(client_res.stdout)
    if client_res.stderr:
        print("=== Client Stderr ===")
        print(client_res.stderr)

    # Shut down server
    stop_server()

    # Read and verify server log
    print("\n=== Verifying Server Logs ===")
    if not os.path.exists(log_file_path):
        print("Error: Headless run log file not found.")
        sys.exit(1)

    with open(log_file_path, "r", encoding="utf-8", errors="replace") as f:
        logs = f.read()

    # Log file content check
    has_gate0 = "[gate0]" in logs
    has_server0 = "[server0]" in logs or "[server1]" in logs
    has_dbmanager = "[dbmanager]" in logs
    has_hostmanager = "[hostmanager]" in logs

    print(f"Log has [hostmanager]: {has_hostmanager}")
    print(f"Log has [dbmanager]: {has_dbmanager}")
    print(f"Log has [gate0]: {has_gate0}")
    print(f"Log has [server0/1]: {has_server0}")

    errors = []
    # Check for unexpected errors
    for line in logs.splitlines():
        if "[Error]" in line or "[Fatal]" in line or "Unhandled Exception" in line:
            # Filter out known non-errors or exceptions if any, or collect them
            errors.append(line)

    if len(errors) > 0:
        print(f"Warning: Found {len(errors)} error/fatal logs in the output:")
        for err in errors[:10]:
            print("  ", err)

    # Assertions
    if not (has_hostmanager and has_dbmanager and has_gate0 and has_server0):
        print("E2E Test FAILED: Some instances did not log output or start properly.")
        sys.exit(1)

    if client_res.returncode != 0:
        print("E2E Test FAILED: Client scenario exited with non-zero code.")
        sys.exit(1)

    print("E2E Test PASSED successfully!")
    sys.exit(0)

if __name__ == "__main__":
    main()
