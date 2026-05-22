"""One-off smoke test for the WebManager HTTP API.

Launches the cluster headlessly + WebManager, hits each known endpoint
once, prints the JSON body, then tears everything down with a graceful
shutdown signal. Intentionally minimal — used while iterating on the
WebManager backend wiring.
"""
import subprocess, time, sys, os, urllib.request, json, urllib.parse

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)


def wait_for(log_path, marker, secs):
    for _ in range(secs):
        time.sleep(1)
        try:
            if marker in open(log_path, "r", encoding="utf-8", errors="replace").read():
                return True
        except OSError:
            pass
    return False


def hit(path, timeout=15):
    url = "http://localhost:7088/api/web-manager/" + path
    print(f"--- GET {path} ---")
    try:
        body = urllib.request.urlopen(url, timeout=timeout).read().decode("utf-8", "replace")
        print(body[:2000])
        return json.loads(body)
    except Exception as e:
        print(f"ERROR: {e}")
        return None


def main():
    print("Launching cluster...")
    log_fp = open("LPS.Server.Demo/logs/headless_run.log", "wb")
    srv = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--", "bydefault", "--headless"],
        cwd="LPS.Server.Demo",
        stdin=subprocess.PIPE, stdout=log_fp, stderr=subprocess.STDOUT,
    )

    if not wait_for("LPS.Server.Demo/logs/headless_run.log", "All other gates connected", 60):
        print("cluster not ready")
        srv.kill()
        return 1
    print("cluster ready")

    web_log = open("webmgr.log", "wb")
    web = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--no-launch-profile", "--urls", "http://localhost:7088"],
        cwd="LPS.Server.WebManager",
        stdout=web_log, stderr=subprocess.STDOUT,
    )
    if not wait_for("webmgr.log", "Now listening", 45):
        print("webmgr not ready")
        web.kill()
        srv.kill()
        return 1
    print("webmgr ready\n")

    try:
        basic = hit("server-basic-info")
        hit("all-server-ping-ping-info")

        if basic and basic.get("serverInfo", {}).get("serverMailBoxes"):
            mb = basic["serverInfo"]["serverMailBoxes"][0]
            sid = urllib.parse.quote(mb["id"])
            host = mb["hostNum"]
            hit(f"single-server-info?serverId={sid}&hostNum={host}")
            hit(f"all-entities?serverId={sid}&hostNum={host}")

        log_list = hit("logs/list")
        if log_list and log_list.get("logs"):
            for entry in log_list["logs"][:2]:
                hit(f"logs/tail?name={urllib.parse.quote(entry['name'])}&lines=5")
    finally:
        print("--- cleanup ---")
        try:
            web.kill()
        except Exception:
            pass
        try:
            srv.stdin.write(b"shutdown\n")
            srv.stdin.flush()
            srv.stdin.close()
            srv.wait(timeout=15)
        except Exception as e:
            print(f"shutdown err: {e}")
            srv.kill()
        print("done")
    return 0


if __name__ == "__main__":
    sys.exit(main())
