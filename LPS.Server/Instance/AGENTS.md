# LPS.Server/Instance

The five process kinds. 17 files at this level (largest concentration in repo). Each kind = a class with `LoopAsync()` lifecycle.

## STRUCTURE
```
HostManager.cs + .Register.cs + .Control.cs + .WebManager.cs   # Mesh orchestrator + state machine (HostStatus None→Starting→...)
Server.cs + .HostConnection.cs + .ServerMessage.cs             # Game logic host (entities live here)
       + .ServiceManager.cs + .WebManager.cs
Gate.cs + .HostConnection.cs + .ClientMessages.cs              # Client-facing TCP front, dispatches to Server
      + .TcpClientMessages.cs + .ServiceManagerMessages.cs
DbManager.cs                                                    # Wraps MongoDb behind RPC
ServiceManager.cs                                               # Routes service RPCs across Service nodes
Service.cs                                                      # Stateful business logic instance
HostConnection/      # Connection abstraction matrix (see below)
ConnectionManager/   # ServiceManagerConnectionManager
```

## HostConnection MATRIX (15 files, the combinatorial heart)
```
ImmediateManagerConnectionBase.cs  +  MessageQueueManagerConnectionBase.cs
HostManagerConnection/      Immediate|Mq HostManagerConnectionOf {Gate,Server,ServiceManager}  → 6 files
ServiceConnection/          Immediate|Mq ServiceManagerConnectionOf {Gate,Server,Service}      → 6 files
IManagerConnection.cs       # Common interface
```
Naming = `{Transport}{TargetRole}ConnectionOf{SourceRole}`. Transport chosen per-connection by `use_mq_to_*` config flag.

## WHERE TO LOOK
| Task | File |
|------|------|
| Host startup state machine | header comment of `HostManager.cs` (8-step doc), then `HostManager.Register.cs` |
| Add a message handler to Gate from client | `Gate.ClientMessages.cs` |
| Add a message handler to Gate from server-side TcpClient | `Gate.TcpClientMessages.cs` |
| Server↔HostManager protocol | `Server.HostConnection.cs` + corresponding `HostConnection/HostManagerConnection/*Of Server.cs` pair |
| Open a 13th process kind | DON'T silently — this matrix balloons fast. Coordinate first. |

## CONVENTIONS
- Every instance kind: ctor stores config → `LoopAsync()` blocks forever → `ServerGlobal.Init(this)` once.
- Partials split by message-source/concern, never by visibility.
- `HostStatus` enum (`HostManager.cs`) gates broadcasts: None→Starting→...→Open. Don't fire client-accept until State2.

## ANTI-PATTERNS
- Do NOT instantiate `Immediate*` or `MessageQueue*` connection classes from outside `Instance/` — go through the instance class's HostConnection partial.
- Do NOT add direct field access between Gate↔Server — everything via Mailbox+RPC or InnerMessages.
- Do NOT block in message handlers — push to SandBox/dispatch queue.
- HostManager dedups `Control.Restart` per-mailbox-id within a 1 s window (`HostManager.cs` `RestartDedupWindow` + `lastRestartAt`). Concurrent restart-registrations from different instances (driven by `TcpClient.OnReconnected`) no longer serialize through a global bool.
