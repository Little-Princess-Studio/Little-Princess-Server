# LPS.Common

Shared kernel — referenced by every other project. 72 .cs files. Pure abstractions + IPC primitives + RPC protocol; no server/client specifics.

## STRUCTURE
```
Debug/Logger.cs              # NLog wrapper. Init(name) per process.
Entity/                      # BaseEntity, ShadowEntity, RpcTimeOutException, Component/{ComponentBase,ComponentAttribute}
Ipc/                         # IN-PROCESS concurrency, NOT inter-process
  SandBox.cs                 # Thread/Task wrapper - one entity = one sandbox
  TimeCircle.cs + TimeCircleSlot.cs + TimerManager.cs   # ms-bucketed ring for property sync ticks
  Dispatcher.cs              # Generic key→handlers callback registry
  Bus.cs                     # In-proc pub/sub
  AsyncTaskGenerator.cs + TokenSequence.cs              # RPC request/response correlation tokens
  Message.cs
Rpc/
  MailBox.cs                 # (HostNum,IP,Port,ID) entity address primitive
  Connection.cs SocketConnection.cs                     # Wire-level base
  RpcHelper.*.cs (4 partials: RpcCall, RpcInit, Serialization, Deserialization)
  RpcGenericArgTypeCheckHelper.cs
  Attribute/                 # RpcMethodAttribute, EntityClassAttribute, RpcJsonTypeAttribute, Authority enum
  InnerMessages/             # PackageHelper, Package, MessageBuffer + ProtobufDefs/*.cs (generated)
  RpcStub/                   # IRpcStub, RpcStubAttribute, RpcStubGenerator(+Manager), RpcStubNotifyOnlyAttribute
  RpcProperty/               # RpcProperty<T>, Plaint/Complex bases, RpcPropertyAttribute
    Weaving/                 # IL-weaving Mo classes (Rougamo.Fody runs on this assembly)
    RpcContainer/            # RpcList, RpcDictionary, RpcPropertyContainer + attrs
  RpcPropertySync/           # SyncMessage/ + SyncInfo/ subdirs (List, Dict, PlaintAndCostume — yes "Costume")
Util/                        # TypeIdHelper, TypeExtensions, AttributeHelper, ITypeIdSupport
```

## WHERE TO LOOK
| Task | File |
|------|------|
| Define new RPC-callable method semantics | `Rpc/Attribute/RpcMethodAttribute.cs` + `Authority.cs` (auth check enforced in `RpcHelper.RpcCall.cs`) |
| Custom property type for sync | Subclass `RpcPropertyContainer` + `[RpcPropertyContainer]` + `[RpcPropertyContainerDeserializeEntry]` |
| New inner protobuf message | `Rpc/InnerMessages/ProtobufDefs/*.proto` (regen) |
| Tweak entity lifecycle | `Entity/BaseEntity.cs` |
| Property sync timing | `Ipc/TimeCircle.cs` (interval must divide 1000 evenly) |

## CONVENTIONS (delta)
- **Rougamo.Fody** weaves IL on this assembly only — `Weaving/*Mo.cs` are aspect classes. Don't move them out.
- All RPC `*Attribute` classes live under `Rpc/Attribute/` or `Rpc/RpcStub/` or `Rpc/RpcProperty/` — NOT a flat `Attributes/` dir.
- `RpcHelper` is partial across 4 files by responsibility (Call/Init/Serialization/Deserialization). Keep that split.
- Note misspellings preserved as identifiers: **"Costume"** (should be "Custom"), **"Plaint"** (should be "Plain"). Do not silently rename.

## ANTI-PATTERNS
- Do NOT add server/client-specific code here — this assembly must stay neutral (referenced by both LPS.Server and LPS.Client).
- Do NOT take dependencies beyond: Google.Protobuf, Newtonsoft.Json, NLog, Rougamo.Fody, System.IO.Pipelines.
- Do NOT use `Task.Run` for long-running work — use `SandBox.Create(Action)` so thread ownership is observable.
- Do NOT bypass `MailBox` for entity addressing.
