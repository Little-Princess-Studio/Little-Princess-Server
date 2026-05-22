# LITTLE PRINCESS SERVER - KNOWLEDGE BASE

**Generated:** 2026-04-27 (Asia/Hong_Kong) | **Commit:** f8de22f | **Branch:** refactor/async-startup

## OVERVIEW
Distributed game server framework, pure C# (`net8.0`, `LangVersion 12`). Multi-process actor mesh: HostManager orchestrates Gate / Server / DbManager / ServiceManager / Service nodes; clients hit Gate over TCP, RPC propagates via Protobuf or RabbitMQ. Status `Alpha`.

## STRUCTURE
```
LPS.Common/             # Shared kernel: BaseEntity, RpcHelper, Protobuf defs, IPC primitives
LPS.Common.Demo/        # Demo RPC stub interfaces (shared client+server contracts)
LPS.Server/             # Server-side core: Instance/, Rpc/, Database/, Service/, MessageQueue/
LPS.Server.Demo/        # Executable entry (Main + bydefault config) + game Logic/
LPS.Server.WebManager/  # ASP.NET Core + React SPA admin (TypeScript blocked, npm-built)
LPS.Client/             # Pure-C# client SDK
LPS.Client.Demo/        # Console client demo
LPS.UnitTest/           # xUnit tests (3 files: RpcProperty, TimeCircle)
LPS.BenchMark/          # BenchmarkDotNet perf harness
docs/                   # ConnectFlow.md, RPCFlow.md (terse 5-liners)
scripts/                # start_deps.{sh,ps1} → docker run rabbitmq+redis+mongo
tools/protoc-3.19.1-*   # Vendored protoc per-OS (do NOT touch)
Default.ruleset         # StyleCop ruleset (most rules = Error)
stylecop.json           # company "Little Princess Studio"
LPS.sln                 # 9 projects
```

## WHERE TO LOOK
| Task | Location |
|------|----------|
| Add new process type | `LPS.Server/StartupManager.cs` switch + `LPS.Server.Demo/Startup.cs` |
| Modify RPC wire format | `LPS.Common/Rpc/InnerMessages/ProtobufDefs/*.proto` → run `tools/protoc-3.19.1-*/gen_proto.{bat,sh}` |
| Add RPC method on entity | Decorate with `[RpcMethod]` in `LPS.Server.Demo/Logic/Entity/` |
| Add stateful service | `LPS.Server.Demo/Logic/Service/` + `[Service]` attribute |
| Add DB API | `LPS.Server.Demo/Logic/DbApi/` + `[DbApi]` / `[DbInnerApi]` |
| Tweak host startup flow | `LPS.Server/Instance/HostManager.cs` (state machine in header comment) |
| Connection variants (mq vs immediate) | `LPS.Server/Instance/HostConnection/` — combinatorial: {Mq,Immediate} × {HostManager,Service} × {Gate,Server,Service,ServiceManager} |
| Property sync ticking | `LPS.Common/Ipc/TimeCircle*.cs` |
| Default startup configs | `LPS.Server.Demo/Config/host0/*.conf.json` |

## CONVENTIONS (PROJECT-SPECIFIC, deviate from C# norms)
- **File header MANDATORY**: `// <copyright file="X.cs" company="Little Princess Studio">` (SA1633 = Error). Do NOT omit.
- **`this.` prefix REQUIRED** on every local member access (SA1101 = Error). `SX1101` disabled - so do NOT remove `this.`.
- **All public elements documented** (SA1600 = Error). XML doc on every public class/method/property/enum value.
- **Partial classes split by concern**: `Server.cs` + `Server.HostConnection.cs` + `Server.ServerMessage.cs` + `Server.ServiceManager.cs` + `Server.WebManager.cs`. Same for `Gate.*`, `HostManager.*`, `RpcHelper.*`.
- **File-scoped namespaces** everywhere (`namespace X;`).
- **Nullable enabled** in every csproj. Use `!` only at JSON deserialization boundaries.
- **Logger**: `LPS.Common.Debug.Logger.{Init,Info,Warn,Error,Debug}`. Init once per process with instance name.
- **Configs are JSON with comments** — `JObject.Parse(..., CommentHandling.Ignore)` in `StartupManager.GetJson`. Keep comments intact.

## ANTI-PATTERNS (THIS PROJECT)
- Do NOT use regions (`SA1124` = Error - even though one `#region` slipped past in `StartupManager`, do not add new ones).
- Do NOT add `#pragma warning disable` casually — only the existing `SA8618` (uninitialized CommandLineParser DTOs) and `SA1602` (HostStatus enum) are tolerated.
- Do NOT bypass `StartupManager.OnGetStartupArgumentsString` — sub-process arg format is contractual (`subproc --type X --confpath Y --childname Z --restart 0|1`).
- Do NOT add new `Generated` protobuf C# by hand — regenerate via `tools/protoc-3.19.1-{win64,linux-x86_64,osx-x86_64}/gen_proto.{bat,sh}`.
- Do NOT call `Process.Start` directly — funnel through `StartupManager.StartSubProcess` (handles Unix vs Windows + hot-reload via `dotnet watch run`).
- Do NOT introduce `ConfigureAwait` style — codebase relies on `.Wait()` / `await` without it; LoopAsync is the canonical run loop.

## UNIQUE STYLES
- **Custom IPC over Tasks**: `LPS.Common/Ipc/SandBox.cs` wraps a Thread or Task; entities run inside a SandBox.
- **TimeCircle** (`LPS.Common/Ipc/TimeCircle.cs`): millisecond-bucketed ring for property-sync flush. `1000 % timeInterval == 0` enforced.
- **AsyncTaskGenerator** (`LPS.Common/Ipc/AsyncTaskGenerator.cs`): RPC request/response correlation via token sequence (`TokenSequence.cs`).
- **RpcStub source-gen-ish**: `RpcStubGeneratorManager.ScanAndBuildGenerator` builds proxies at startup via reflection + `Rougamo.Fody` (IL weaving on `LPS.Common`).
- **MailBox = (HostNum, IP, Port, ID)** quadruple — sole entity addressing primitive (`LPS.Common/Rpc/MailBox.cs`).
- **Two transports interchangeable**: `Immediate*` (TCP) vs `MessageQueue*` (RabbitMQ). Selected per-connection via `use_mq_to_*` config booleans.

## COMMANDS
```bash
# 1. Start infra (Docker required)
scripts\start_deps.ps1                # or scripts/start_deps.sh
# 2. Build + run all sub-processes from default config
dotnet build LPS.Server.Demo --configuration Release
dotnet run --project LPS.Server.Demo -- bydefault
# Or: scripts/start_up_default_server.sh
# 3. Test
dotnet test LPS.UnitTest
# 4. Bench
dotnet run -c Release --project LPS.BenchMark
# 5. Web admin (requires npm in LPS.Server.WebManager/ClientApp)
dotnet run --project LPS.Server.WebManager
# 6. Regenerate protobuf (Windows)
tools\protoc-3.19.1-win64\gen_proto.bat
```

## NOTES
- **Hardcoded ports** in `Config/host0/*.conf.json` (12001, 12011, etc.) — known issue (README TODO).
- **Auto-restart on non-zero exit**: `StartupManager.StartSubProcess` re-spawns crashed children. Don't fight it.
- `Thread.Sleep(10000)` at end of `Startup.Main` is intentional — gives spawned procs time to detach.
- `LPS.Server.WebManager/ClientApp/node_modules` is committed-by-accident-prone; gitignored but watch your PRs.
- Branch `refactor/async-startup` is in flight — many `LoopAsync` paths recently changed signatures.
- README is mostly Chinese; class-level XML docs are English.
