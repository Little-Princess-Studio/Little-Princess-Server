# LPS.Common/Rpc

Wire protocol + RPC dispatch primitives. Neutral to client/server.

## STRUCTURE
```
MailBox.cs                       # (HostNum, IP, Port, ID) — sole entity address
Connection.cs / SocketConnection.cs  # Abstract wire base
RpcHelper.RpcCall.cs             # CallLocalEntity (reflection invoke + auth check)
RpcHelper.RpcInit.cs             # ScanRpcMethods, ScanRpcPropertyContainer (called at startup)
RpcHelper.Serialization.cs / .Deserialization.cs   # Protobuf↔CLR for RPC arg lists
RpcGenericArgTypeCheckHelper.cs
Attribute/        # [RpcMethod], [EntityClass], [RpcJsonType], Authority enum
RpcStub/          # Client-side proxy generator (interface + Mgr + Generator + RpcStubAttribute + IRpcStub + NotifyOnly)
RpcProperty/      # RpcProperty<T> base + Plaint/Complex bases + Container hierarchy
                  # Weaving/ holds Rougamo aspect classes (IL-woven into setters/getters)
RpcPropertySync/  # Diff messages flushed by TimeCircle (List/Dict/PlaintAndCostume × Message+Info)
InnerMessages/    # PackageHelper.cs (registry), Package.cs, MessageBuffer.cs + ProtobufDefs/*.cs (generated)
```

## CONVENTIONS (delta)
- `RpcHelper` is partial × 4 by phase: Call (dispatch), Init (reflection scan), Serialization, Deserialization. Don't merge.
- "Costume" in `RpcPlaintAndCostumePropertySync*` is misspelling of "Custom". Preserved.
- "Plaint" is misspelling of "Plain". Preserved across `RpcPlaintProperty*`.
- Auth check: `Authority` enum + `DoAuthorityCheck` in `RpcHelper.RpcCall.cs` — every RPC invocation passes through.

## ANTI-PATTERNS
- Do NOT call `MethodInfo.Invoke` directly for RPC — go through `RpcHelper.CallLocalEntity` so auth + arg conversion run.
- Do NOT add RPC attributes outside `Attribute/` subfolder.
- Do NOT bypass `PackageHelper` registry when adding a new inner message — receivers won't decode it.
- Comment `// todo: impl jit to compile methodInfo.invoke to expression.invoke` in RpcCall is a known perf gap, don't "quick fix" with another reflection layer.
