# LPS.Server.Demo

Executable entry. Hosts game `Logic/` as exemplar of how to build on top of LPS.Server.

## STRUCTURE
```
Startup.cs                # Main(). CommandLineParser verbs: startup | bydefault | subproc
Config/host0/*.conf.json  # 7 configs: hostmanager, gate, server, dbmanager, service, mq, globalcache
Logic/
  Entity/                 # Player.cs, Untrusted.cs (initial pre-auth entity)
  Component/              # GamePropertyComponent, BagComponent (compose into entities)
  Service/                # PlayerRosterService, EchoService (with [Service] attr)
  DbApi/                  # DbApi.cs ([DbApi] / [DbInnerApi] handlers)
  RpcStub/                # IPlayerStub.cs (interface, generator builds proxy)
logs/                     # NLog runtime output (gitignored)
```

## WHERE TO LOOK
| Task | File |
|------|------|
| Add config to default boot | `Startup.StartupByDefault` (calls `StartupManager.FromConfig` per file) |
| New CLI verb | Add `[Verb]` class in `Startup.cs` + `MapResult` arm |
| New player RPC | Add method on `Logic/Entity/Player.cs` with `[RpcMethod]` |
| New service | `Logic/Service/<Name>.cs` : `BaseService` + `[Service("name")]` + register namespace in `service.conf.json` `service_namespace` |
| Wire new entity namespace | Set `entity_namespace` / `rpc_property_namespace` / `rpc_stub_interface_namespace` in `server.conf.json` |

## CONVENTIONS (delta)
- Configs are JSON-with-comments — `JObject.Parse` uses `CommentHandling.Ignore`. Comments are documentation, don't strip.
- `bydefault` verb hardcodes `Config/host0/*.conf.json` paths relative to CWD — run from `LPS.Server.Demo/` dir.
- `Untrusted.cs` is the initial entity created on auth — don't put privileged RPC methods there.

## ANTI-PATTERNS
- Do NOT add server-framework code here — this project should only contain entry + game logic. Move shared infra into `LPS.Server`.
- Do NOT write to `logs/` from code — use `Logger`, NLog config controls path.
- The `#pragma warning disable CS8618` around CLI option DTOs is intentional (CommandLineParser sets via reflection). Don't "fix" by initializing.
- `Thread.Sleep(10000)` at end of `Main` is intentional — gives subprocesses time to detach before parent exits.
