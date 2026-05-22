# LPS.Server/Rpc

Server-side RPC transport + protobuf message generation. Wraps lower-level `LPS.Common/Rpc`.

## STRUCTURE
```
TcpServer.cs / TcpClient.cs        # Inner-mesh TCP transport (Gate↔Server, Server↔Host, etc.)
MqConnection.cs                    # RabbitMQ alternative transport
RpcServerHelper.cs                 # Server-only RPC helpers (uses LPS.Common.RpcHelper underneath)
RpcProtobufDefs.cs                 # Initialize() registers all inner protobuf message types. CALL FIRST.
RpcStubForServerClientAttribute.cs + RpcStubForServerClientEntityGenerator.cs  # ServerClient (client-bound entity) stub gen
Protocal/                          # IProtocal + TCP.cs + UDP.cs (typo "Protocal" intentional, load-bearing)
RpcProperty/                       # RpcPlaintProperty, RpcComplexProperty (server-side concrete property types)
InnerMessages/ProtobufDefs/        # *.proto SOURCE + generated *.cs (HostCommand, Control, ExchangeMailbox(+Res),
                                   #   ServiceManagerCommand, ServiceControl, DatabaseManagerRpc(+Inner), CreateEntity)
```

## WHERE TO LOOK
| Task | File |
|------|------|
| New inner mesh message | Add `name.proto` here → run `tools/protoc-*/gen_proto.{bat,sh}` → register in `RpcProtobufDefs.Initialize` |
| TCP framing changes | `TcpServer.cs` + `TcpClient.cs` (sender queue redesign is a known TODO — README) |
| Add reliable RabbitMQ topic | `MqConnection.cs` + `LPS.Server/MessageQueue/Consts.cs` |

## ANTI-PATTERNS
- Do NOT skip `RpcProtobufDefs.Initialize()` at process boot — `PackageHelper` lookups will throw.
- Do NOT hand-edit generated `ProtobufDefs/*.cs` — they have no copyright header by design (regen overwrites).
- Do NOT rename `Protocal` → `Protocol` without coordinating: 3 files + interface name + many call sites.
- TODO from README: `TcpClient.cs` lacks reconnect + send-queue SandBox. Don't add half-fixes.
