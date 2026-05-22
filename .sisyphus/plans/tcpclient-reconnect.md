# Plan: TcpClient Reconnect (inner-mesh TCP)

**Owner**: Sisyphus
**Created**: 2026-05-22
**Status**: DRAFT — pending Momus review
**Branch target**: `main`
**Estimated commits**: 3–4 (infrastructure → per-call-site wiring → tests → docs)

---

## Goal

When a `TcpClient` (used between LPS server processes — Gate↔HostMgr, Gate↔Server, Gate↔Gate, Server↔HostMgr, DbMgr↔HostMgr) loses its socket connection, it must automatically re-establish the TCP connection with exponential backoff and re-run the call-site-specific handshake, instead of the current behavior of calling `Stop()` and relying on the supervisor to restart the whole process.

Out of scope: client↔gate KCP/TCP path, MQ transport, send-queue redesign (separate TODO), in-flight RPC replay.

---

## Decisions (locked, from Q&A round)

| # | Decision | Implication |
|---|---|---|
| D1 | **Scope = full peer-restart semantics.** Reconnect drops in-flight state and re-runs registration handshake. No peer-side session table, no resume protocol, no epoch handshake. | Peer treats every reconnect as a brand-new instance with the same name. HostMgr's "dedup duplicate restart instances" TODO becomes load-bearing — we must fix that latent bug as part of this work. |
| D2 | **In-flight RPCs fail with `RpcException`.** No replay. | Owners of `AsyncTaskGenerator` (BaseEntity, Server, ServiceManager, DbClient) must be notified to fail their pending tokens. See risks §"In-flight RPC fate" — this is partially aspirational; see Acceptance §V3. |
| D3 | **Add `OnReconnected` callback.** `OnConnected` fires once on first connect; `OnReconnected` fires on every subsequent successful reconnect. Each of 6 call sites updates explicitly. | No `OnConnected` is fired twice. `OnDispose` semantics change: today it fires on disconnect (final). New: `OnDispose` fires only on terminal stop; new `OnDisconnected` fires per drop. |
| D8 | **Reconnect-after-peer-restart uses the existing `Control{Restart}` flow**, not `RequireCreateEntity`. HostManager already has a `RestartInstance` path (`HostManager.cs:536-548` → `HostManager.Register.cs:223-238`) that evicts the old `mailboxIdToConnection[mb.Id]`, removes old MailBox entries via `RemoveOldInstanceInfoAndUpdateNewInstanceInfo`, and broadcasts the new connection. We are NOT inventing dedup — we are wiring our reconnect callback into the machinery the codebase already has for restart handling. | No new protobuf. No new HostManager code path. The latent "todo: filter duplicate restart instance" at `HostManager.cs:549` becomes the only HostManager-side change. |
| D4 | **Send-queue redesign deferred.** Messages enqueued but not yet sent during outage are dropped. Callers may see RPC failures and must handle them. | Acceptable because D2 already says in-flight fails. Outbox/replay tackled separately. |
| D5 | **MQ untouched.** `IManagerConnection` does NOT gain reconnect-related members. Reconnect is a property of `TcpClient` and `ImmediateManagerConnectionBase` only. | RabbitMQ.Client handles its own auto-recovery below the abstraction. No abstraction leak. |
| D6 | **Infinite exponential backoff, capped at 30 s.** Schedule: 1 s, 2 s, 4 s, 8 s, 16 s, 30 s, 30 s, … Never gives up while `stopFlag == false`. | Supervisor-driven restart remains the escape hatch (operator kills the instance). |
| D7 | **Verification = integration test + process-kill script.** PowerShell script that boots cluster, kills a Server process, asserts via Gate logs / supervisor status that Gate reconnects to the restarted Server within N seconds. | No fake-socket unit tests; no netem half-open tests in this PR (half-open detection via Ping is deferred to a follow-up). |

---

## Pre-existing surface (read-only inputs)

### Transport configuration (CRITICAL to QA design)

Default cluster (`LPS.Server.Demo/Config/host0/*.conf.json`) routes Gate↔HostMgr and Server↔HostMgr over **RabbitMQ**, not TCP (`use_mq_to_host: true` for every gate and server). This means in the default config, the only `TcpClient`-using paths are:

- **DbMgr → HostMgr** (`DbManager.cs:102`) — uses TCP directly, bypasses `IManagerConnection` abstraction. Always TCP regardless of config.
- **Gate → other Gate, Gate → Server, Gate ↔ Server cell-migration** — 4 sites in `Gate.HostConnection.cs` use `TcpClient` directly. Always TCP regardless of `use_mq_to_host`.

The two Immediate*HostManagerConnection paths (`ImmediateHostManagerConnectionOfGate.cs:47`, `ImmediateHostManagerConnectionOfServer.cs:46`) are unreachable in the default config. To QA those, the integration test (C4) MUST first stamp out a **test config variant** (`Config/host0_immediate/`) with `use_mq_to_host: false` on at least one gate (e.g. `gate0`) and one server, and start the cluster against THAT config.

The plan keeps all 6 sites in scope, but C4 splits into two phases:
- C4a — default-config test (kills server0, verifies Gate→Server reconnect via the 4 always-TCP sites).
- C4b — immediate-config test (boots a separate cluster against `Config/host0_immediate/`, verifies Immediate*Connection reconnect).

### Files that hold the 6 `new TcpClient(...)` call sites
1. `LPS.Server/Instance/DbManager.cs:102` — DbMgr→HostMgr.
   `OnConnected` sends `Control{From=Dbmanager, Message=Ready, Args=[mailbox]}` (lines 111-122).
2. `LPS.Server/Instance/HostConnection/HostManagerConnection/ImmediateHostManagerConnectionOfGate.cs:47` — Gate→HostMgr.
   `OnConnected` sends `RequireCreateEntity{GateEntity, ConnectionID=onGenerateAsyncId()}` + `ManagerConnectedEvent.Signal()` (lines 62-73).
3. `LPS.Server/Instance/HostConnection/HostManagerConnection/ImmediateHostManagerConnectionOfServer.cs:46` — Server→HostMgr (symmetric to #2 with `ServerDefaultCellEntity`).
4. `LPS.Server/Instance/Gate.HostConnection.cs:192` `ReconnectServer` — Gate→Server (singleton reconnect, NotifyServerReady only).
5. `LPS.Server/Instance/Gate.HostConnection.cs:229` `ReconnectGate` — Gate→other Gate (Control.Ready only).
6. `LPS.Server/Instance/Gate.HostConnection.cs:342` `SyncOtherGatesMailBoxes` initial — `OnConnected = _ => this.allOtherGatesConnectedEvent.Signal()` (CountdownEvent — **NOT** reconnect-safe).
7. `LPS.Server/Instance/Gate.HostConnection.cs:374` `SyncServersMailBoxes` initial — `OnConnected` calls both `NotifyServerReady(self)` AND `allServersConnectedEvent.Signal()` (CountdownEvent — **NOT** reconnect-safe).

> Sites 6 and 7 fire CountdownEvent.Signal in OnConnected. Reconnect MUST NOT re-Signal these.

### Files that own pending RPC tokens (D2 surface)
- `LPS.Common/Entity/BaseEntity.cs:67-68` — entity RPC await/return tokens
- `LPS.Server/Instance/Server.cs:58,119` — mailbox-creation tokens
- `LPS.Server/Instance/ServiceManager.cs:61-62` — service/entity RPC callbacks
- `LPS.Server/Database/Storage/DbClient.cs:33-34` — DB RPC tokens

These dictionaries are NOT keyed by TCP connection — we cannot generically fail "RPCs for this client." See Risks §1.

### TcpClient internals
- `LPS.Server/Rpc/TcpClient.cs`
  - `ConnectRetryMaxTimes = 10` (line 62) — currently used ONLY for initial connect.
  - `IoHandler` (line 232) — single-shot: connect → loop on messages → finally dispose. Reconnect requires a state machine here.
  - `SendQueueMessageHandler` (line 196) — `Stop()` on send error (line 223). Must change to mark "disconnected" and wait for reconnect, not stop.
  - `Socket` exposed as public property (line 30) — callers may stash it; need to audit (`git grep "\.Socket"`).

---

## Architecture changes

### A1. Public surface of `TcpClient`

Add:
```csharp
public Action<TcpClient>? OnReconnected { private get; init; }
public Action<TcpClient>? OnDisconnected { private get; init; }   // per drop; before reconnect attempts begin
public bool IsConnected { get; }                                  // observability
public int ReconnectAttempt { get; }                              // observability (resets on success)
```

Behavior change:
- `OnInit`: fires once at `Run()` (unchanged).
- `OnConnected`: fires **once**, on first successful connect (changed: was per IoHandler invocation; previously only one happened anyway since IoHandler ran once).
- `OnDisconnected`: fires when an existing connection is lost (new).
- `OnReconnected`: fires on every successful re-establishment (new). Callers wire registration RPC + idempotency checks here.
- `OnDispose`: fires once at terminal stop (semantic clarified: was tied to IoHandler return — same effective trigger).

### A2. `IoHandler` state machine

Replace single-shot connect with:

```
state := Initial
loop while !stopFlag:
    switch state:
        Initial:          attempt connect (existing retry logic) → Connected | Failed
        Connected:        OnConnected / OnReconnected fires here; run message loop until socket throws
                          on socket error → Disconnected
        Disconnected:     fire OnDisconnected; wait backoff; → Reconnecting
        Reconnecting:     attempt single connect; on success → Connected (fire OnReconnected)
                          on fail → increment ReconnectAttempt, longer backoff; stay Reconnecting
        Failed (initial connect exhausted ConnectRetryMaxTimes): → terminate (preserve current behavior)
```

Backoff: `Math.Min(30_000, 1000 * (1 << Math.Min(ReconnectAttempt, 5)))` ms with ±20% jitter. Resets to 0 on successful reconnect.

### A3. SendQueueMessageHandler: drop-on-disconnect

```csharp
catch (Exception e) {
    Logger.Warn(e, $"Send msg {msg} failed; will drop and rely on reconnect.");
    // do NOT this.Stop()
    // do NOT requeue
    // socket will be detected dead by IoHandler's receive loop; reconnect kicks in there.
}
```

Add fast path: if `!IsConnected`, dequeue + log + drop (avoid Socket.Send on null).

### A4. Connection identity reset

Each successful (re)connect creates a fresh `SocketConnection` (already happens at TcpClient.cs:277). Two existing references to `connection` field (`Stop()` line 152) keep working.

`counterOfId` (line 75, used by `GenerateMsgId`) is NOT reset on reconnect — preserves monotonic uniqueness even across reconnects (avoids accidental id collision with peer's lingering buffers).

### A5. Per-call-site wiring (the manual work)

**Key insight**: `RequireCreateEntityRes` handlers signal `localEntityGeneratedEvent` (`Gate.HostConnection.cs:296`, `Server.HostConnection.cs:295,313`), which is a one-shot CountdownEvent. Reconnect MUST NOT re-fire `RequireCreateEntity` — that handshake is startup-only. Reconnect uses `Control{Restart, From=<role>, Args=[currentMailBox]}` instead, hitting the existing `HostManager.Register.cs:223` `RestartInstance` flow which already evicts old `mailboxIdToConnection[mb.Id]` and re-broadcasts.

The Gate/Server caches its MailBox (from the original `RequireCreateEntityRes`) — reconnect re-sends THAT cached MailBox in the Restart message. Same `MailBox.Id`, same `Ip`/`Port` (config-driven), same `HostNum`. From HostManager's perspective: "the gate0 socket got replaced — update the connection pointer."

| Site | Current `OnConnected` | New `OnReconnected` |
|---|---|---|
| `DbManager.cs:111` (Ctl.Ready) | unchanged | Send `Control{Restart, From=Dbmanager, Args=[this.MailBox]}` — but see A6 below: HostManager's `RemoveOldInstanceInfoAndUpdateNewInstanceInfo` currently has an empty `Dbmanager` arm. C3 must fill that arm (re-key `mailboxIdToConnection` only, since there is no `dbManagerMailBoxes` list to scrub). Until the arm is filled, DbMgr reconnect would update `mailboxIdToConnection[mb.Id]` via `RestartInstance` (line 231) but leave `instanceStatusManager` stale — acceptable only if C3 lands in the same PR. Plan: C3 lands `Dbmanager` arm together. |
| `ImmediateHostManagerConnectionOfGate.cs:60` | KEEP `RequireCreateEntity`. KEEP `ManagerConnectedEvent.Signal()` — but **guard it** to fire only once (replace the inline CountdownEvent.Signal with a `Interlocked.CompareExchange(ref signaled, 1, 0)`-guarded call, or use the fact that ManagerConnectedEvent has count 1 → `if (!signaled) Signal()`). | Send `Control{Restart, From=Gate, Args=[this.gateEntity.MailBox]}`. This requires `ImmediateHostManagerConnectionOfGate` to obtain the Gate's MailBox — passed in via a new `Func<MailBox> getGateMailBox` ctor param (the Gate fills it after `RequireCreateEntityRes` arrives). The first reconnect occurs only AFTER initial registration completed, so the MailBox is guaranteed populated. |
| `ImmediateHostManagerConnectionOfServer.cs:46` | symmetric guarded Signal | Send `Control{Restart, From=Server, Args=[this.serverMailBox]}` — same pattern. |
| `Gate.HostConnection.cs:192` `ReconnectServer` | NotifyServerReady (no CountdownEvent here) | `NotifyServerReady` is idempotent on Server side because Server's `HandleControl{Ready}` from a Gate just (re)registers the gate connection. **Verify this** during C2 by reading `Server.HostConnection.cs` Control handler. If non-idempotent, switch to `Control{Restart}` from Gate's perspective too — but Server doesn't currently handle `Control{Restart}` from a Gate; would need handler. **Default plan: re-NotifyServerReady; fall back if review shows it's unsafe.** |
| `Gate.HostConnection.cs:229` `ReconnectGate` | Control.Ready to other Gate | identical re-Ready (other Gate just keeps a peer-Gate registry; re-Ready overwrites). Verify in C2. |
| `Gate.HostConnection.cs:342` Initial-sync-to-other-gate | `allOtherGatesConnectedEvent.Signal()` | Empty lambda OR symmetric re-Ready to peer Gate. DO NOT re-Signal CountdownEvent. |
| `Gate.HostConnection.cs:374` Initial-sync-to-server | NotifyServerReady + `allServersConnectedEvent.Signal()` | NotifyServerReady only. DO NOT re-Signal. |

**Symmetry check**: in the initial-sync paths (sites 6, 7), if the socket drops AFTER the CountdownEvent has already been Signal'd-and-disposed (CountdownEvent is short-lived; recreated in each `SyncOtherGatesMailBoxes` call), reconnect must NOT touch the event. Mitigation: don't capture it in the OnReconnected lambda at all — it's safe by construction.

### A6. Peer-side: HostManager already supports restart-registration; only minor fixes

We do NOT add new dedup. The existing `Control{Restart}` flow at `HostManager.cs:536-548` → `HostManager.Register.cs:223-238` `RestartInstance` already:
- Evicts `mailboxIdToConnection[mb.Id]` (re-keys to new `conn`).
- Calls `RemoveOldInstanceInfoAndUpdateNewInstanceInfo` which removes the old MailBox from `gatesMailBoxes` / `serversMailBoxes` by `CompareOnlyAddress`.
- Calls `NotifyRestart` which broadcasts the new MailBox to peers.

What this plan changes:
- **Remove the `isInstanceRestarting` global guard** at `HostManager.cs:537-548` — currently it accepts only the FIRST restart from any source and ignores the rest (see existing TODO at line 549: *"filter duplicate restart instance by mailbox"*). With reconnects, multiple instances may restart-register independently and concurrently. Replace the global bool with per-`mailBox.Id` filtering: use a `ConcurrentDictionary<string, DateTime> lastRestartAt`; reject restart messages received within a 1 s window for the same `mailBox.Id` (covers TCP duplicate-delivery edge cases, not concurrent peer restarts). This addresses `AGENTS.md:46` TODO precisely.
- **Fill the empty `RemoteType.Dbmanager` arm** at `HostManager.Register.cs:284-285`. Currently `RemoveOldInstanceInfoAndUpdateNewInstanceInfo` for DbManager just `break;`s — meaning `RestartInstance` for DbManager re-keys `mailboxIdToConnection[mb.Id]` (line 231) but leaves `instanceStatusManager` showing the stale entry. Add the symmetric logic: `this.instanceStatusManager.Unregister(oldMb)` if we can locate the old DbManager MailBox. Source for `oldMb`: query `instanceStatusManager` by type=DbManager — confirm during C3 that this API exists; if not, add a `dbManagerMailBoxes` list mirror of `gatesMailBoxes` to track them. Then `UpdateInstanceStatus(RemoteType.Dbmanager, mailBox)` re-registers.
- **Audit `UpdateInstanceStatus` switch arms for `Dbmanager` and `ServiceManager`** — confirm both branches exist and re-register properly when called from `RestartInstance`. If `Dbmanager` arm of `UpdateInstanceStatus` is also empty/missing, add `this.instanceStatusManager.Register(mailBox, InstanceType.DbManager)`.
- **Update `RestartInstance` to also handle the no-prior-record case gracefully** (Gate's first reconnect after a HostManager restart — HostManager has no record of the Gate yet, but the Gate thinks it's restarting). Inspect `RemoveOldInstanceInfoAndUpdateNewInstanceInfo`: if `gatesMailBoxes.FindIndex` returns -1, just register fresh (effectively == `RegisterInstance`). Current code's switch arms already have `if (index != -1)` guards for Gate/Server — verify all paths in the switch are safe with no match. The `ServiceManager` arm (line 276-283) currently does NOT check for null/uninitialized `serviceManagerInfo.ServiceManagerMailBox` — this needs a guard too.

**The dedup key is `mailBox.Id`** (e.g. `"gate0"`, `"server0"`) — this is unique by instance name across the cluster, NOT (HostNum, EntityType, IP) as the rejected draft proposed. The IP-based key fails because `gate0`/`gate1` share `127.0.0.1` in `Config/host0/gate.conf.json`.

### A7. Gate-side handling when its TcpClient to Server detects disconnect

The Gate has a `MailBox` cached on the `TcpClient` (site 4/7: `client.MailBox = mb`). When Server restarts:
- Server gets a new ephemeral port? **NO** — Server port comes from `Config/host0/server*.conf.json` and is stable across restarts.
- Same MailBox.Id (since Name is deterministic)? **YES** for supervisor-driven restart.
- Therefore Gate can reconnect to the same `serverIp:serverPort` and resume routing. No mailbox table rewrite needed.

---

## Implementation order (commits)

### C1. Infrastructure: TcpClient state machine + callbacks
Files: `LPS.Server/Rpc/TcpClient.cs` only.
- Add OnReconnected / OnDisconnected / IsConnected / ReconnectAttempt.
- Refactor `IoHandler` to loop with state machine.
- Replace send-error `Stop()` with log+drop.
- No call-site changes. Build must pass (existing call sites should compile unchanged because new members are optional `init`).
**Verification**: `dotnet build LPS.sln` clean. Cluster boots normally (no Server kills yet).

### C2. Per-call-site wiring
Files: 3 call-site files (DbMgr, two HostManagerConnections, Gate.HostConnection.cs).
- Add `OnReconnected` to each.
- Move CountdownEvent.Signal out of `OnConnected` lambdas that re-fire (sites 6, 7).
- Verify SignalOnce semantics by reading existing event lifecycle (e.g. `allOtherGatesConnectedEvent` is created fresh on each `SyncOtherGatesMailBoxes` call, line 327 — safe to Signal once; the danger is *double*-Signal within the same sync session, which a reconnect during initial sync would cause).
**Verification**: Cluster boots normally; manual `pwsh scripts/proc.ps1 restart cluster` cycle.

### C3. HostManager restart-message hardening
Files: `LPS.Server/Instance/HostManager.cs` (the `Control` switch), `LPS.Server/Instance/HostManager.Register.cs` (`RestartInstance` + `RemoveOldInstanceInfoAndUpdateNewInstanceInfo` + `UpdateInstanceStatus`).
- Replace the `isInstanceRestarting` global bool guard with a `ConcurrentDictionary<string, DateTime>` keyed by `mailBox.Id`, 1 s suppression window.
- Fill the empty `RemoteType.Dbmanager` arm at `HostManager.Register.cs:284-285`: unregister old mailbox from `instanceStatusManager` (locate via the manager's API; add a `dbManagerMailBoxes` mirror list if needed).
- Confirm `UpdateInstanceStatus` has a `Dbmanager` arm; add it if missing.
- Audit `RemoveOldInstanceInfoAndUpdateNewInstanceInfo` for the "no prior record" case in each switch arm; route to plain `RegisterInstance` when `index == -1`. Add null-guard around `ServiceManager` arm (currently dereferences `serviceManagerInfo.ServiceManagerMailBox` unconditionally).
- Log at Info: `"[host] restart-registering {role} {mailbox.Id} replacing connection {old_conn_id} -> {new_conn_id}"`.

**Verification** (uses `Stop-Process -Force` for crash simulation — NOT `/supervisor/instance/{name}/stop`, which marks the stop as `DeliberatelyStopping` in `StartupManager.cs:911` and the auto-restart branch then deliberately skips that name):
```pwsh
$status = Invoke-RestMethod http://localhost:7090/supervisor/status
$pid = ($status.instances | Where-Object name -eq 'server0').pid
Stop-Process -Id $pid -Force
# Supervisor's existing non-zero-exit handler will respawn server0.
```
HostMgr log must contain `"restart-registering Server server0"`. Equivalent crash simulation for `dbmanager` (no numeric suffix per `StartupManager.cs:285`) and a Gate.

### C4. Integration test (process kill scripts)
Files:
- `scripts/recovery/kill_and_assert_reconnect.ps1` (new) — default-config (C4a).
- `scripts/recovery/kill_and_assert_reconnect_immediate.ps1` (new) — immediate-config (C4b).
- `LPS.Server.Demo/Config/host0_immediate/*.conf.json` — copy of `host0/` with `use_mq_to_host=false` on gate0 and server0. Other instances stay on MQ to keep the test focused.

**Instance naming reality check** (`LPS.Server/StartupManager.cs:285,307`):
- `hostmanager` (no suffix)
- `dbmanager` (no suffix)
- `gate0`, `gate1` (suffix from `gates` dict keys)
- `server0`, `server1` (suffix from `servers` dict keys)
- `servicemanager`, `service0`, `service1` (verify during C4 implementation)

**Readiness probe** (same as before): poll `/supervisor/status` until all configured instances report `alive=true` AND `hasExited=false`, then `Start-Sleep 8`. There is no `HostStatus` log line to wait for; `HostStatus.Running` is assigned silently at `HostManager.Register.cs:95`.

**Crash simulation** (CRITICAL — supervisor HTTP `/supervisor/instance/{name}/stop` marks the name as `DeliberatelyStopping` at `StartupManager.cs:911` and the exit handler will NOT auto-restart it). Use `Stop-Process -Force` on the PID instead, which produces a non-zero exit that the supervisor's normal auto-restart branch will pick up.

#### C4a — default config (Gate↔Server, Gate↔Gate, DbMgr↔HostMgr)

Steps:
- Boot default cluster (`proc.ps1 start cluster`); readiness as above.
- Use `LPS.Client.Demo --bench` minimal probe RPC to confirm baseline path.
- Identify target Server PID: `($status.instances | Where-Object name -eq 'server0').pid`.
- `Stop-Process -Id $pid -Force` — this is a real crash.
- Wait up to 30 s, polling `/supervisor/status` for `server0` to reappear with a NEW `pid` and `alive=true`.
- Send another RPC. **Assertion**: succeeds within 10 s of new PID being seen.
- Tail Gate log: assert at least one `"OnReconnected"` log line (TcpClient will emit it at Info — added in C1).
- Tail HostMgr log: must contain `"restart-registering Server server0"` (uses MQ for Server↔HostMgr in this config but the restart-registration handler is the same code path on the receiving end).
- Negative case: between kill and Server-back-up, send an RPC and confirm it fails fast with timeout/`RpcException`, does NOT hang past the configured RPC timeout.

**Additional C4a steps for DbMgr** (DbMgr↔HostMgr is always TCP per A5 site 1):
- Kill HostMgr via `Stop-Process -Id <hostmanager-pid> -Force`.
- Wait for HostMgr respawn (new pid, alive=true).
- Tail DbMgr log: must contain `"OnReconnected"` and successful `Control{Restart}` round-trip.
- Tail new HostMgr log: must contain `"restart-registering Dbmanager dbmanager"` (no numeric suffix).

#### C4b — immediate config (Gate↔HostMgr, Server↔HostMgr via TCP)

**Prerequisite source change** (part of this PR, lands with C4b):
- Add `[Option("config-dir", Default = "Config/host0/")] public string ConfigDir { get; set; }` to `ByDefaultOptions` in `LPS.Server.Demo/Startup.cs:95-108`.
- Change `StartupByDefault(bool hotreload)` to `StartupByDefault(bool hotreload, string configDir)` — replace each hardcoded `"Config/host0/hostmanager.conf.json"` etc. with `Path.Combine(configDir, "hostmanager.conf.json")`. Keep default behavior identical when `--config-dir` omitted (Default attribute provides `"Config/host0/"`).
- Verify `Supervisor.Start()` still fires (it does — line 144, unchanged).
- This is a ~10 line change in Startup.cs + one Default attribute.

**Why not use `startup -p`?** `Startup.cs:81-93` `StartUpOptions` path does NOT call `Supervisor.Start()`, so `/supervisor/status` would 404. Crash-and-recover QA depends on the supervisor HTTP. Therefore `bydefault --config-dir` is the only viable boot mode.

Steps:
- Create `LPS.Server.Demo/Config/host0_immediate/` by copying `host0/` and flipping `use_mq_to_host: false` on `gate0` and `server0` (other instances stay on MQ to keep the test focused; gate1/server1 stay on MQ to confirm mixed-transport coexistence).
- Launch: `dotnet run --project LPS.Server.Demo -- bydefault --headless --config-dir Config/host0_immediate/`. Optionally add a sibling helper `scripts/proc_immediate.ps1` that wraps this (clones `proc.ps1` and parameterizes the args).
- Readiness probe identical to C4a.
- Crash `gate0` via `Stop-Process -Id <pid> -Force`. Wait for respawn.
- Assert: `gate0` log contains `OnReconnected` AND HostMgr log contains `"restart-registering Gate gate0"` (this exercises `ImmediateHostManagerConnectionOfGate.OnReconnected`).
- Repeat for `server0`. Assert HostMgr log contains `"restart-registering Server server0"` (exercises `ImmediateHostManagerConnectionOfServer.OnReconnected`).
- `gate1` and `server1` remain on MQ throughout — confirms the Immediate vs MQ paths coexist without interference.

**Verification**: both scripts exit 0; documented in `scripts/recovery/README.md` (new).

**Known limitation**: a future improvement (out of scope) would add a `clusterStatus` field to `/supervisor/status` so QA doesn't need the `Start-Sleep 8` settle.

### C5. AGENTS.md updates
- `LPS.Server/Rpc/AGENTS.md`: remove "TcpClient.cs lacks reconnect" TODO; mention OnReconnected lifecycle.
- `LPS.Server/Instance/AGENTS.md:46`: remove "HostManager doesn't filter duplicate restart instances" TODO (now done).
- Root `AGENTS.md`: bump generated date + commit.

---

## Risks & open questions

### R1. In-flight RPC fate is partially aspirational
D2 says "fail with RpcException", but the AsyncTaskGenerator dictionaries are not keyed by TCP connection. We have no generic way to enumerate "tokens that depended on this client" without each caller maintaining its own (token → client) mapping. Realistic interpretation:
- Pending tokens leak (never complete) — caller's await hangs forever unless there's a timeout.
- Recommend: add a documented note "in-flight RPCs during reconnect leak today; add per-call timeouts at the await site as a follow-up."
- Alternative: add a coarse callback `OnDisconnected` that owners listen to and fail ALL their pending tokens (acceptable but blunt; may fail tokens belonging to other unaffected connections).

> Resolution proposal: **document the leak; do not block this PR on a fix**. Add a TODO with a clear scope ("per-RPC timeout at await site").

### R2. Concurrency hazards in `OnConnected`/`OnReconnected` lambdas
Lambdas capture `this` of the calling instance. With reconnect, these may now run on the IoHandler thread DURING normal operation (not just at startup). Audit each lambda for thread-safety:
- `RegisterMessageHandler` / `Unregister` — uses Dispatcher, internally locked (need to verify).
- `ManagerConnectedEvent.Signal` — moved out; non-issue.
- `tcpClientsToServer.Remove(self)` — `List<T>.Remove` is NOT thread-safe. Currently safe because `OnDispose` only fires on a single shutdown thread. With reconnect, `OnDisconnected` fires from the IoHandler thread mid-run. **Need a lock or ConcurrentBag**.

> Resolution: add `lock(this.tcpClientsToServerLock)` around list mutations OR switch to `ConcurrentDictionary<int,TcpClient>` keyed by tmpIdx.

### R3. Gate's CountdownEvent.Signal on initial sync site (6, 7) re-firing
A worst-case interleaving: initial sync starts → site 6 fires CountdownEvent.Signal → Server crashes mid-sync → reconnect kicks in → OnReconnected fires WITHOUT re-Signal. ✅ safe.

But the OPPOSITE: initial connect FAILS (Server not yet up) → ConnectRetryMaxTimes=10 exhausts → IoHandler throws → BUT new state machine never reaches "Disconnected" because we never reached "Connected". This must terminate the client gracefully and let the existing `Failed` path proceed. State machine handles this.

### R4. `ConnectRetryMaxTimes = 10` semantics
Current: used for initial connect. New: D6 says infinite for reconnect. Decision: initial connect keeps ConnectRetryMaxTimes (boot-time fail-fast is desirable; HostMgr might genuinely not exist), reconnect uses infinite backoff (mid-run, peer might be restarting). State machine differentiates by which path it's on.

### R5. Stop() race during reconnect
If `Stop()` is called while in Reconnecting state:
- `stopFlag = true` (line 150) is set.
- `connection?.TokenSource.Cancel()` may be null (no live connection) — null-safe.
- `Socket?.Shutdown` may throw on a half-constructed socket — wrapped in try/catch already.
State machine's main loop checks `!stopFlag` each iteration; will exit promptly. Need to also wake the backoff sleep — use `CancellationToken` instead of `Task.Delay(backoffMs)` so Stop() cancels the wait.

### R6. KCP / Bus interaction
KCP path is independent — different `KcpConnection` class, doesn't use `TcpClient`. ✅ orthogonal.
Bus pump: TcpClient owns its own bus (line 94). Reconnect creates a new SocketConnection but reuses the same Bus. Messages received post-reconnect flow into the same bus → fine. ✅ no interaction.

---

## Acceptance criteria

| ID | Verification | Method |
|---|---|---|
| V1 | `dotnet build LPS.sln` clean, 0 errors. | CI build. |
| V2 | Cluster boots normally without kills (regression). | `proc.ps1 restart cluster`; poll `/supervisor/status` until all configured instances report `alive=true` AND `hasExited=false`; then `Start-Sleep 8` settle. (`/supervisor/status` does NOT expose `HostStatus`; HostManager has no "Running"/"Open" log line today. See C4 note.) |
| V4 | Gate + DbManager survive a HostMgr supervisor-restart. | `$hm = ($status.instances | Where name -eq 'hostmanager').pid; Stop-Process -Id $hm -Force` (crash, supervisor auto-restarts; the `DeliberatelyStopping` path would NOT auto-restart). Within 30 s of HostMgr back-up: (a) DbMgr log shows `OnReconnected` (DbMgr is always TCP to HostMgr per A5 site 1); (b) new HostMgr log shows `"restart-registering Dbmanager dbmanager"` — note bare name without numeric suffix (`StartupManager.cs:285`). Gate↔HostMgr uses MQ in default config so no TcpClient reconnect there; the Gate `Immediate*Connection` reconnect path is exercised in C4b's immediate-config test only. No process crashes. |
| V3 | `scripts/recovery/kill_and_assert_reconnect.ps1` exits 0. | New integration test. Kills server0, waits, asserts Gate logs "OnReconnected" + a probe RPC succeeds within 5 s of Server back-up. |
| V5 | No double-Signal of CountdownEvents. | Code review; verify sites 6, 7 OnReconnected lambdas don't call .Signal(). |
| V6 | HostMgr restart-registers reconnecting instance. | Inspect HostMgr log during V3 — must contain `"restart-registering Server server0"`. The `isInstanceRestarting` global guard is replaced; concurrent restart-registrations from two different instances must not block each other. |
| V7 | StyleCop clean. | `dotnet build` produces no new SA-warnings. |
| V8 | AGENTS.md TODOs removed. | `git grep "lacks reconnect"` returns empty. `git grep "doesn't filter duplicate restart instances"` returns empty. |

---

## Estimated diff size

- `TcpClient.cs`: +120 / −30 lines (state machine + new callbacks + ctx).
- Per-call-site files: ~+15 lines each × 5 files = +75.
- `HostManager.Register.cs`: +25 / −5 (dedup logic).
- `LPS.Server.Demo/Startup.cs`: +10 (new `--config-dir` option for C4b boot).
- `LPS.Server.Demo/Config/host0_immediate/*.conf.json`: +60 (copy of host0/ with `use_mq_to_host=false`).
- `scripts/recovery/kill_and_assert_reconnect.ps1` + `..._immediate.ps1`: +160 (new).
- AGENTS.md edits: +5 / −3.

**Total**: ~+460 lines, ~−40 lines.

---

## Non-goals (explicit)

- Send-queue redesign (separate TODO; D4).
- Half-open detection via SO_KEEPALIVE or Ping heartbeats (deferred follow-up; mentioned for R1 timeout extension).
- MQ transport changes (D5).
- Resurrecting `TokenSequence` / reentry-flag replay (D2 says no replay).
- Per-call-site retry budgets (D6 says uniform).
- Reconnect for client↔gate (TCP or KCP) — that's a different layer, not inner-mesh.
