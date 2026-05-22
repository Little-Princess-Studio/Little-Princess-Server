# LPS.Server

Server-side core library (referenced by `LPS.Server.Demo` exe). 84 .cs files. Targets net8.0.

## STRUCTURE
```
StartupManager.cs        # Static. Switch on process type → spawn/init each instance kind.
ServerGlobal.cs          # Per-process singleton holder (Init once with HostManager/Gate/Server/...)
Instance/                # Process kinds (Gate, Server, HostManager, DbManager, ServiceManager, Service)
  HostConnection/        # 12-class combinatorial matrix: {Immediate,MessageQueue}×{Host,ServiceMgr}×{Gate,Server,Service,ServiceMgr}
  ConnectionManager/     # Tracks live connections per service-mgr
Rpc/                     # TcpServer/Client, MqConnection, Protobuf inner messages, server-side RpcStub generator
  Protocal/              # IProtocal + TCP/UDP wrappers (note: spelled "Protocal", not Protocol)
  InnerMessages/ProtobufDefs/  # *.proto + GENERATED *.cs (do not hand-edit .cs)
  RpcProperty/           # RpcPlaintProperty, RpcComplexProperty (server flavor)
Service/                 # BaseService, ServiceAttribute, ServiceHelper (reflection scan), HttpRpcMethodAttribute
Database/                # DbHelper (Redis init), GlobalCache/, Storage/ (MongoDb wrapper + Db*Api attributes)
Entity/                  # ServerEntity, GateEntity, DistributeEntity, CellEntity, ServerClientEntity, ServerDefaultCellEntity
MessageQueue/            # MessageQueueClient (RabbitMQ.Client), Consts, JsonBody
```

## WHERE TO LOOK
| Task | File |
|------|------|
| Add new instance kind | `StartupManager.cs` (FromConfig + StartUpAsync) + new `Instance/<Name>.cs` with `LoopAsync` |
| Wire up Gate↔Server connection variant | `Instance/HostConnection/` — pick correct {Immediate,MessageQueue} class pair |
| Modify host registration handshake | `Instance/HostManager.Register.cs` (split partial) |
| Inner Protobuf message | `Rpc/InnerMessages/ProtobufDefs/*.proto` → regen via `tools/protoc-*/gen_proto.*` |
| Service RPC dispatch | `Service/ServiceHelper.cs` (currently uses reflection — README marks for JIT optimization) |
| DB driver swap | `Database/Storage/IDatabase.cs` → `MongoDb/MongoDbWrapper.cs` |

## CONVENTIONS (delta from root)
- Each `Instance/*.cs` instance class exposes `Task LoopAsync()` as the run-forever entry. Called by `StartupManager.StartUp*Async`.
- `Instance/` files are heavily partial-split by concern (e.g. `Server.cs` + `Server.HostConnection.cs` + `Server.ServerMessage.cs` + `Server.ServiceManager.cs` + `Server.WebManager.cs`). Add new concerns as new partial files, never grow the base file.
- Connection classes follow strict naming: `{Transport}{Target}ConnectionOf{Source}` (e.g. `MessageQueueServiceManagerConnectionOfGate`).
- `RpcProtobufDefs.Initialize()` MUST be called first in every `StartUp*Async` before any RPC use.

## ANTI-PATTERNS
- Do NOT instantiate `TcpClient`/`MqConnection` directly from instances — go through `IManagerConnection` abstraction.
- Do NOT hand-write Protobuf C# under `InnerMessages/ProtobufDefs/*.cs` — regenerate.
- Do NOT add a 13th HostConnection variant without first checking the matrix is still {Immediate,Mq}×{Host,Svc}×{Source} — combinatorial blow-up is a smell.
- "Protocal" misspelling is load-bearing (folder + interface). Fixing it touches everything; don't unless coordinated.
