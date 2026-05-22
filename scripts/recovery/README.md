# Reconnect Recovery Tests

Integration tests for `TcpClient`'s reconnect state machine. Both scripts
boot a real cluster (RabbitMQ/Redis/MongoDB must be running locally) and
crash specific processes via `Stop-Process -Force` to verify the
client-side reconnect + HostManager-side restart-registration flow.

## Scripts

### `kill_and_assert_reconnect.ps1` — default config

Uses `scripts/proc.ps1 start cluster` (which boots `Config/host0/`, where
gates and servers talk to HostManager via RabbitMQ). Exercises:

- **Gate ↔ Server** reconnect (always TCP regardless of MQ config). Kills
  `server0`, asserts Gate logs `Reconnected to 127.0.0.1:12001`.
- **DbManager ↔ HostManager** reconnect (always TCP, bypasses
  `IManagerConnection`). Kills `hostmanager`, asserts HostMgr logs
  `restart-registering Dbmanager dbmanager` — exercises the
  `RemoteType.Dbmanager` arm of `RemoveOldInstanceInfoAndUpdateNewInstanceInfo`.

### `kill_and_assert_reconnect_immediate.ps1` — immediate config

Boots `Config/host0_immediate/` via `dotnet run -- bydefault --config-dir`
(requires the `--config-dir` option added in `LPS.Server.Demo/Startup.cs`).
In this config `gate0` and `server0` have `use_mq_to_host: false`, so they
use `ImmediateHostManagerConnectionOfGate` / `OfServer` — the TCP path to
HostManager. Kills `hostmanager`, asserts HostMgr logs both
`restart-registering Gate ...` and `restart-registering Server ...` — the
Immediate path's `OnReconnected -> Control{Restart}` round-trip.

## Why two scripts?

The default `host0/` config uses MQ for inner mesh traffic (`use_mq_to_host`
is true everywhere), so the Immediate TCP→HostManager paths never run there.
A separate config + script is necessary to cover those code paths.

## Running

```pwsh
# From repo root, infra running (rabbitmq + redis + mongo):
pwsh scripts/recovery/kill_and_assert_reconnect.ps1            # default config
pwsh scripts/recovery/kill_and_assert_reconnect_immediate.ps1  # immediate config
```

Each script exits:
- `0` — all assertions pass
- `1` — at least one assertion failed (details printed)
- `2` — cluster failed to come up

## Known limitations

- Readiness gate is `all instances alive` + `Start-Sleep 8` settle. There's no
  `clusterStatus` field on `/supervisor/status` and no `HostStatus=Open` log
  emission to wait for. A future supervisor improvement could replace the
  settle delay with an explicit readiness signal.
- These tests cover crash + auto-restart only. They do NOT exercise:
  - Half-open TCP (no FIN/RST) — would need WinDivert/clumsy/`tc netem`.
  - In-flight RPC failure on disconnect — current behavior is "leak" per the
    reconnect plan; a separate per-await-site timeout follow-up is needed.
