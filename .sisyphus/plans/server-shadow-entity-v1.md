# Plan: Server-Side Shadow Entities (v1 MVP)

**Owner**: Sisyphus
**Created**: 2026-05-23
**Status**: DRAFT — pending Momus review
**Branch target**: `main`
**Estimated commits**: 4-5

---

## Goal

Introduce a "shadow entity" concept on the server side, mirroring the existing client-side `ShadowClientEntity` model. An entity ("ori") lives on one server; other servers in the SAME Gate's reach can hold a read-only shadow that receives the ori's property updates. Shadows do NOT accept RPC calls and do NOT modify their own properties.

This realizes a long-standing TODO at `LPS.Server/Instance/Gate.ClientMessages.cs:105` and `LPS.Server/Instance/Server.cs:315`.

---

## Decisions (locked, from Q&A round)

| # | Decision | Rationale |
|---|---|---|
| D1 | **Shadow rejects RPC.** Any RPC call routed to a shadow's MailBox throws `RpcException` on the receiving server. Callers must address the ori MailBox directly. | Simplest semantics; no location transparency. No risk of accidental fan-out of side-effectful calls. |
| D2 | **Explicit-API creation.** Game logic calls `Server.CreateShadowEntity(oriMailBox, targetServerName)` (or similar). No eager broadcast, no lazy on-demand. | Per-instance subscription. No bandwidth surprises. Clearest test fixture. |
| D3 | **Shadow MailBox = derived Id.** Format: `"{oriId}@shadow@{shadowServerName}"` (subject to bikeshed during C1). RPC routing strips the `@shadow@...` suffix to find the ori; presence of the suffix marks a MailBox as a shadow handle. | Mechanical disambiguation. Local server can tell ori from shadow by string suffix. Backward-compatible: existing non-shadow Ids contain no `@`. |
| D4 | **Sync routes via existing Server↔Gate edges.** Server0 (ori) → Gate0 → Server2 (shadow). Gate fans out to both client AND any subscribed shadow-servers. **NO new server↔server channel.** | Reuses recently-stabilized TcpClient + reconnect. No N×N topology. |
| D5 | **Reuse `RpcPropertySetting.ServerToShadow` flag** for both client and server shadows. The flag means "this property syncs to any subscribed shadow (client OR server)". | Existing demo components (`BagComponent.Items`, `GamePropertyComponent.Hp/Sp`) automatically gain server-shadow sync without code changes. |
| D6 | **v1 MVP scope** — same-Gate shadows only. The following are explicitly **out of scope for v1**: |
|    | (a) Cross-Gate shadows | future work |
|    | (b) Shadow RPC forwarding | per D1, shadows reject |
|    | (c) Entity migration interaction (ori-server migration) | future work; v1 destroys + recreates manually |
|    | (d) Crash recovery (ori-server crash, shadow-server crash full-sync) | future work; v1 leaves shadows stale if a side crashes |
|    | (e) Component-level shadow declaration | v1 syncs whatever `ServerToShadow`-flagged props are on the entity; user already has the flag |
|    | (f) Multi-tenant/multi-ori shadow batching | v1: one shadow object per ori instance per target server |

---

## Derived decisions (from MVP cut + above)

| ID | Decision |
|---|---|
| D7 | **Per-instance shadow subscription.** Registry maps `oriMailBox.Id → Set<TcpClient to shadow-server>`. No "all servers" broadcast. |
| D8 | **Registry lives on Gate.** New field `Gate.shadowSubscriptions : ConcurrentDictionary<string, HashSet<TcpClient>>` keyed by ori MailBox.Id. HostManager is NOT involved in v1 (no cross-Gate, no central truth). |
| D9 | **Shadow lifecycle**: `CreateShadowEntity` API on ori-side server sends `RequireCreateShadowEntity` to Gate; Gate creates the shadow on the target server (via existing TcpClient to target) AND registers the subscription. `DestroyShadowEntity` symmetrically. |
| D10 | **v1 ships with manual destroy.** When ori entity is `Destroy()`d, ori-server sends `DestroyShadowEntity` for each known shadow. v1 does NOT handle ori-server crash → ori-server respawn → orphaned shadows; documented limitation. |

---

## Pre-existing surface (read-only inputs)

### Client-side reference implementation (to mirror, NOT touch)
- `LPS.Client/Entity/ShadowClientEntity.cs` (~250 lines) — exposes `ServerProxy`, `IsFrozen` until `OnLoaded`, `ApplySyncCommandList`, `FindContainerByPath`.
- `LPS.Client/Rpc/RpcProperty/RpcShadow{Plaint,Complex}Property.cs` — read-only property containers.
- `LPS.Client/Rpc/RpcStubGenerator*` — `RpcStubForShadowClient` attribute branches.

### Server-side surface to extend
- `LPS.Server/Entity/` — add `ShadowEntity` (server-side variant; mirrors most of `LPS.Client/Entity/ShadowClientEntity.cs`).
- `LPS.Server/Rpc/RpcProperty/` — add `RpcServerShadow{Plaint,Complex}Property<T>` mirroring client equivalents.
- `LPS.Server/Instance/Server.cs` — add `CreateShadowEntity` / `DestroyShadowEntity` public API; add `localShadowEntities` dict.
- `LPS.Server/Instance/Gate.ClientMessages.cs:105` — extend `HandlePropertySyncCommandListFromServer` to ALSO fan out to subscribed shadow-servers (in addition to the existing client redirect).
- `LPS.Server/Instance/Gate.HostConnection.cs` — handle new `RequireCreateShadowEntity` / `DestroyShadowEntity` messages from servers.

### New protobuf (must regen)
- `LPS.Server/Rpc/InnerMessages/ProtobufDefs/CreateShadowEntity.proto` —
  ```
  // ori-server -> Gate. Request creation of a shadow on a specific server.
  message RequireCreateShadowEntity {
    MailBox ori_mailbox = 1;
    string target_server_name = 2;          // e.g. "server1"
    string entity_class_name = 3;           // class name registered in entity_namespace
    uint32 request_id = 4;                  // correlates with RequireCreateShadowEntityRes
  }

  // Gate -> target shadow-server. Tells target to instantiate a shadow.
  message CreateShadowEntity {
    MailBox ori_mailbox = 1;
    MailBox shadow_mailbox = 2;             // pre-computed derived id (see D3)
    string entity_class_name = 3;
  }

  // target shadow-server -> Gate -> ori-server. Confirms construction.
  // Carries the shadow MailBox so ori-server's CreateShadowEntity API can
  // resolve its Task<MailBox> return. Also: when this arrives at ori-server,
  // ori-server fires PropertyFullSync (per R2 Option B) to seed the shadow.
  message RequireCreateShadowEntityRes {
    MailBox ori_mailbox = 1;
    MailBox shadow_mailbox = 2;
    uint32 request_id = 3;                  // matches the original RequireCreateShadowEntity
    bool ok = 4;
    string error = 5;
  }

  // ori-server -> Gate. Request destruction of a specific shadow.
  // Per-target: carries shadow_mailbox so Gate can remove exactly one
  // subscription from shadowSubscriptions[ori.Id] without affecting other
  // shadow servers that hold their own shadow of the same ori.
  message RequireDestroyShadowEntity {
    MailBox ori_mailbox = 1;
    MailBox shadow_mailbox = 2;
  }

  // Gate -> target shadow-server. Tells target to destroy its local shadow.
  message DestroyShadowEntity {
    MailBox shadow_mailbox = 1;             // includes ori reference via the derived-id suffix
  }
  ```
  Regen via `tools/protoc-3.19.1-win64/gen_proto.bat`. Register all five in `RpcProtobufDefs.Initialize`.

  > **Note on async correlation**: `request_id` on `RequireCreateShadowEntity`/`Res` integrates with `AsyncTaskGenerator<MailBox>` (already used by `Server.cs:58,119` for entity creation). Reuse the pattern.

### RpcProperty system existing surface
- `RpcPropertySetting.ServerToShadow` (`RpcProperty.cs:121`) + `ShouldSyncToShadow` (line 172) — already exists. **v1 redefines the semantic**: previously implicit "client shadow"; now "any subscribed shadow incl. server-side". The change is additive — properties currently flagged will start syncing to server shadows ONLY when a subscription exists, which requires an explicit `CreateShadowEntity` call. **Zero behavior change for existing demos until they call the API.**

### Shadow construction model (resolves Momus B1)

**Decision: instantiate the ORIGINAL entity class in shadow mode.**

Rationale:
- `LPS.Server/Rpc/RpcServerHelper.cs:35-57` already instantiates `DistributeEntity` subclasses by name from the configured `entity_namespace`. We reuse this factory.
- The same `Player.cs` / `Untrusted.cs` class declarations work as both ori and shadow because R5 made write-time property guards: `RpcProperty.Val` setter throws when `Owner.IsShadow == true`. No separate shadow-specific class hierarchy needed.
- `BaseEntity.IsShadow` (new virtual property, default false) is overridden during construction in the shadow path. Specifically:
  - Add `BaseEntity.IsShadow { get; protected set; }` (or constructor-only `init`).
  - Add `RpcServerHelper.CreateEntity` overload: `CreateEntity(string className, bool asShadow)`. When `asShadow=true`, the factory sets `entity.IsShadow = true` before any RPC-property registration, so the property setter guards (R5) kick in from the first mutation.
  - On the shadow side, the entity is added to `Server.localShadowEntities` (keyed by shadow `MailBox.Id`) instead of the normal `localEntityDict`.
- Components: `Player` has e.g. `BagComponent` declared with `RpcComplexProperty<RpcList<Item>>`. Same instances work on shadow side because R5 freezes ALL `RpcProperty` writes via the `IsShadow` check; reads are unaffected. No component-level changes needed for v1.

**What this excludes**:
- We do NOT need new "shadow class registrations" or a parallel namespace.
- We do NOT need users to declare shadow-specific entity subclasses.
- Method dispatch is governed by D1: any RPC arriving for a shadow MailBox throws `RpcException` at the server's entity-RPC dispatcher BEFORE the method is invoked. Local code reading `shadowPlayer.SomeProp.Val` still works (read-only).

**Implementation site**: extend `RpcServerHelper.CreateEntity` (or add `CreateShadowEntity` overload). C2 wires `Server.HandleCreateShadowEntity` to call this factory.

---

## Architecture: data flow

### Subscription creation
```
ori-Server0 game logic calls:
    var shadowMb = await Server.CreateShadowEntity(oriMailBox: player42, "server1", "Player");

ori-Server0:
  1. Allocate a request_id via AsyncTaskGenerator<MailBox>.
  2. Send RequireCreateShadowEntity{ori_mailbox=player42, target_server_name="server1",
     entity_class_name="Player", request_id=N} to Gate over existing socket.
  3. await the AsyncTaskGenerator task keyed by request_id.

Gate (HandleRequireCreateShadowEntity):
  1. Compute derived shadow MailBox: ori.Id + "@shadow@server1", target_server_name's ip/port.
  2. Look up TcpClient to "server1" (existing FindServerTcpClientFromMailBox pattern).
  3. Register subscription: shadowSubscriptions[player42.Id].Add(tcpClientToServer1).
  4. Send CreateShadowEntity{ori_mailbox, shadow_mailbox, entity_class_name="Player"} to server1.
  5. Remember (request_id, ori-server connection) in a pendingShadowCreates map so Gate
     can route the Res back when target acks. Use AsyncTaskGenerator on Gate too if desired.

target Server1 (HandleCreateShadowEntity):
  1. Use RpcServerHelper.CreateEntity("Player", asShadow=true) -> ShadowEntity-mode Player.
  2. Set entity.MailBox = shadow_mailbox; entity.IsShadow = true; entity.IsFrozen = true.
  3. localShadowEntities[shadow_mailbox.Id] = entity.
  4. Send RequireCreateShadowEntityRes{ori_mailbox, shadow_mailbox, request_id, ok=true} back to Gate.

Gate (HandleRequireCreateShadowEntityRes):
  1. Look up pendingShadowCreates[request_id] -> ori-server connection.
  2. Forward Res to ori-Server0.

ori-Server0 (HandleRequireCreateShadowEntityRes):
  1. AsyncTaskGenerator.ResolveAsyncTask(request_id, shadow_mailbox). Caller's await unblocks.
  2. ALSO: emit a PropertyFullSync for player42 immediately. This flows via the normal
     server->gate->fan-out path (existing PropertyFullSync emission). The Gate's fan-out (C3)
     sees the shadow subscription registered in step 3 above and forwards the full sync to
     server1. server1's shadow receives, applies, OnLoaded() unfreezes. (Per R2 Option B.)
```

> **Failure on shadow creation**: if target server returns ok=false (or times out per
> AsyncTaskGenerator default timeout), Gate sends back Res{ok=false, error=...} which
> the ori-server propagates as a task exception. Gate ALSO removes the speculative
> subscription added in step 3. C3 includes this rollback.

### Property sync fan-out (every TimeCircle tick)
```
ori-Server0 ticks, emits PropertySyncCommandList for player42 → Gate (existing).
Gate.HandlePropertySyncCommandListFromServer (the TODO line):
  1. Existing: RedirectMsgToClientEntity(player42.Id, ...) -> client.
  2. NEW: lookup shadowSubscriptions[player42.Id]; for each tcpClient, Send(syncList).
Each shadow-server receives → dispatches to local ShadowEntity.ApplySyncCommandList.
```

### Destruction (per-target)
```
ori-Server0 calls Server.DestroyShadowEntity(oriMailBox, shadowMailBox).
   (NOTE: API takes the SPECIFIC shadow MailBox returned from CreateShadowEntity,
    not just targetServerName, to keep destruction strictly per-target.)

ori-Server0:
  1. Send RequireDestroyShadowEntity{ori_mailbox, shadow_mailbox} to Gate.
     (Fire-and-forget; no Res. Gate logs any inconsistency.)
  2. Remove shadow from local tracking (per-Server "I created this shadow" set).

Gate (HandleRequireDestroyShadowEntity):
  1. shadowSubscriptions[ori.Id].Remove(tcpClientToShadowServer). If the set becomes
     empty, remove the key entirely.
  2. Forward DestroyShadowEntity{shadow_mailbox} to the target shadow-server.

target server (HandleDestroyShadowEntity):
  1. localShadowEntities.Remove(shadow_mailbox.Id, out var entity); entity.OnDestroy().
  2. No reply.

Cleanup on ori.Destroy(): ori-Server iterates its known set of "shadows of this ori on
which servers" (maintained server-side; not a Gate query) and emits one
RequireDestroyShadowEntity per known shadow. v1 doesn't ack; on ori-server crash the
shadows are leaked (documented limitation per D6(d)).
```

---

## Implementation order (commits)

### C1. Protobuf + ShadowEntity + RpcServerShadowProperty types
Files:
- `LPS.Server/Rpc/InnerMessages/ProtobufDefs/CreateShadowEntity.proto` (new) — five messages per "New protobuf" section above (incl. `RequireCreateShadowEntityRes` and `RequireDestroyShadowEntity`).
- Regen via `tools/protoc-3.19.1-win64/gen_proto.bat`
- `LPS.Server/Rpc/RpcProtobufDefs.cs` — register all five new package types
- `LPS.Server/Entity/ShadowEntity.cs` (NOT a separate class hierarchy — per "Shadow construction model" section above, we use the original entity classes in shadow mode). Actually NO new file here; instead:
  - `LPS.Common/Entity/BaseEntity.cs` — add `public bool IsShadow { get; protected internal set; }` with default false. Document semantics in xmldoc.
- `LPS.Server/Rpc/RpcServerHelper.cs` — extend `CreateEntity` (line 35-57) to accept `asShadow` bool; if true, set `entity.IsShadow = true` before any property registration runs.
- `LPS.Server/Rpc/RpcProperty/RpcPlaintProperty.cs` + `RpcComplexProperty.cs` — in the `Val` setter (or equivalent mutation entrypoint), guard with:
  ```csharp
  if (this.Owner?.IsShadow == true) {
      throw new InvalidOperationException(
          $"Cannot write to shadow entity property: {this.Path}");
  }
  ```
  No new shadow-specific property types are needed (was the original plan; superseded by the IsShadow-guard approach).

**Verification**: `dotnet build LPS.sln` clean. Existing demos still work (no IsShadow=true anywhere yet). New protobuf types registered.

### C2. Server-side API + local registry + per-server "my shadows" tracking
Files:
- `LPS.Server/Instance/Server.cs` + new partial `Server.ShadowEntity.cs` —
  - new `ConcurrentDictionary<string, BaseEntity> localShadowEntities` keyed by shadow `MailBox.Id`. Stores SHADOW-MODE entities living on this server (i.e. this server is acting as shadow for an ori elsewhere).
  - new `ConcurrentDictionary<string, List<MailBox>> myShadowsOf` keyed by ORI `MailBox.Id`, value = list of shadow MailBoxes this server has CREATED. Used at ori-side `Destroy()` to fan-out destroy messages per target.
  - new `AsyncTaskGenerator<MailBox> shadowCreateTaskGen` for correlating `RequireCreateShadowEntity` -> `RequireCreateShadowEntityRes`.
  - public `Task<MailBox> CreateShadowEntity(MailBox oriMailBox, string targetServerName, string entityClassName)`:
    1. Generate request_id via shadowCreateTaskGen.
    2. Send RequireCreateShadowEntity over the gate-bound socket (see R1; first verify, then implement).
    3. Return the awaitable.
    On Res arrival: `shadowCreateTaskGen.ResolveAsyncTask(request_id, shadow_mailbox)`. Also record `myShadowsOf[ori.Id].Add(shadow_mailbox)` and emit PropertyFullSync for ori.
  - public `Task DestroyShadowEntity(MailBox oriMailBox, MailBox shadowMailBox)` — sends RequireDestroyShadowEntity; updates myShadowsOf; no await on Res (v1 fire-and-forget).
  - private `HandleCreateShadowEntity(CreateShadowEntity msg)` — runs on target server; instantiates via `RpcServerHelper.CreateEntity(entity_class_name, asShadow: true)`; adds to `localShadowEntities`; sends `RequireCreateShadowEntityRes` back to Gate.
  - private `HandleDestroyShadowEntity(DestroyShadowEntity msg)` — runs on target server; removes from `localShadowEntities`.
  - private `HandlePropertySyncCommandList` extension: when a sync arrives whose target MailBox.Id is in `localShadowEntities`, route to the shadow entity (calling its `ApplySyncCommandList`) instead of the normal `localEntityDict` path. **This addresses the `Server.cs:315` TODO** in the same commit.
  - private `HandleEntityRpc` extension (D1 enforcement): before dispatching, check if `entityRpc.EntityMailBox.Id` matches a key in `localShadowEntities`. If yes, immediately send back an `EntityRpcCallBack` whose payload carries an `RpcException` with message "RPC to shadow entity is not allowed; address ori MailBox." Do NOT invoke the entity method. This makes D1 enforceable without users having to spread checks across method bodies.
- `LPS.Common/Entity/BaseEntity.cs` — extend `Destroy()` to walk `Server.myShadowsOf[this.MailBox.Id]` (need access via callback or DI; simplest: an `OnDestroy` event the Server subscribes to) and emit destroy messages. Or simpler: `Server.OnEntityDestroyed(entity)` hook called explicitly from `Server.RemoveEntity`.

**R1 verification (BEFORE writing C2 code)**:
- One-shot reconnaissance: write a Ping send from Server back to Gate over the inbound `SocketConnection` (use the connection stashed in `Server.GateConnections`). If it round-trips, R1 is resolved — Server→Gate via inbound socket is viable. Add to throwaway scratch commit, then delete.
- If R1 fails: escalate to user; alternative is to add a Server-initiated `TcpClient` to each Gate (mirrors Gate->Server pattern; bigger change, would push the plan to v1.5).

**Verification**: `dotnet build` clean. Cluster boots with no usage. Add a temporary `[RpcMethod]` on demo entity that calls `CreateShadowEntity` for sanity smoke. Real assertions land in C4.

### C3. Gate fan-out + subscription registry
Files:
- `LPS.Server/Instance/Gate.cs` — new field `ConcurrentDictionary<string, HashSet<TcpClient>> shadowSubscriptions`. (HashSet wrapped in lock; or use `ConcurrentDictionary<TcpClient, byte>` as a set.)
- `LPS.Server/Instance/Gate.ClientMessages.cs:105` — extend `HandlePropertySyncCommandListFromServer`:
  ```csharp
  this.RedirectMsgToClientEntity(propertySyncCommandList.EntityId, propertySyncCommandList);
  if (this.shadowSubscriptions.TryGetValue(propertySyncCommandList.EntityId, out var subs))
  {
      foreach (var sub in subs) { sub.Send(propertySyncCommandList, false); }
  }
  ```
- Same fan-out for `PropertyFullSync` (line ~127) and `ComponentSync` (line 83). NOT for `EntityRpc` (per D1 shadows reject RPC; we don't even deliver it).
- `LPS.Server/Instance/Gate.HostConnection.cs` (or new `Gate.ShadowMessages.cs` partial) — handle `RequireCreateShadowEntity` from ori-server: register subscription, forward `CreateShadowEntity` to target server. Symmetric for destroy.

**Verification**: integration test (C4). Manual: cluster boots, demo command creates shadow, log shows fan-out.

### C4. Integration test
Files:
- `scripts/recovery/assert_shadow_sync.ps1` (new) — boot cluster, drive a demo command that:
  1. Creates an entity on server0.
  2. Calls `CreateShadowEntity(ori, "server1")`.
  3. Mutates a `ServerToShadow`-flagged property on ori.
  4. Waits one TimeCircle tick (~25 ms? confirm).
  5. Queries server1's shadow value via a debug RPC or log assertion.
  6. Asserts shadow value == new ori value.
  7. Calls `DestroyShadowEntity`; mutates again; asserts shadow no longer receives.
- Requires adding a tiny demo command in `LPS.Server.Demo/Logic/` (e.g., a `[RpcMethod]` on Untrusted called `DebugCreateShadow(targetServer)`).

**Verification**: script exits 0.

### C5. AGENTS.md + docs
- `LPS.Server/Entity/AGENTS.md` (new or update) — document `ShadowEntity` lifecycle, RPC rejection rule, MailBox.Id derivation.
- `LPS.Server/Instance/AGENTS.md` — note Gate's `shadowSubscriptions` registry, fan-out rule, v1 limitations (no cross-Gate, no crash recovery, no migration).
- `LPS.Server/Rpc/AGENTS.md` — note new protobuf messages.
- Remove TODO at `Gate.ClientMessages.cs:105` and `Server.cs:315`.

---

## Risks & open questions

### R1. Server → Gate reverse-channel for `RequireCreateShadowEntity`
Servers receive Gate connections (Gate initiates). Today Server sends replies back over the SAME inbound SocketConnection (see `Server.SendEntityRpcCallBack` pattern). **Need to verify** during C2 that this socket is bidirectional and that we can spontaneously send a non-reply `RequireCreateShadowEntity` over it. If not, alternative: route through HostManager OR Server initiates a new TcpClient to Gate (mirrors `Gate.HostConnection.cs` to-server pattern).

**Mitigation**: bench by sending a Ping from Server → Gate over the inbound connection in a throwaway commit before C2; if successful, proceed; if not, escalate to user.

### R2. PropertyFullSync seeding the shadow
When shadow is just created, it needs the CURRENT ori state, not just future deltas. Mechanism:
- Option A: Gate, on `CreateShadowEntity` arrival, requests `PropertyFullSync` from ori-server before delivering to target. **Timing complexity.**
- Option B: ori-server, on receiving its own `RequireCreateShadowEntity` notification (a "shadow created for you" notice from Gate), emits `PropertyFullSync` immediately, which the gate fans out.
- **Plan choice: Option B** — simpler, parallels existing client-shadow create flow which already does this.

### R3. `IsFrozen` window
Shadow constructor sets `IsFrozen = true`. `ApplySyncCommandList` is no-op while frozen. First incoming `PropertyFullSync` calls `OnLoaded()` → `IsFrozen = false`. Subsequent incremental syncs land. **Risk**: if incremental sync arrives before full sync (out-of-order), data is silently dropped. Per D6(d), crash recovery is out of scope; document this risk but don't fix in v1.

### R4. `MailBox.Id` suffix collision
Format `"{oriId}@shadow@{server}"`. Confirm during C1 that `@` is unused in entity Id space. `LPS.Common/Rpc/MailBox.CompareOnlyID` uses string Equals on Id; suffix differs → distinct mailbox. RPC routing keyed by Id will route shadow-mailbox-targeted RPC to the LOCAL ShadowEntity (per D1) which throws `RpcException`. ✓ aligns with D1.

### R5. Component instances on shadow side
`BagComponent` is declared once with `RpcShadowComplexProperty<RpcList<Item>>` on the CLIENT side. The SERVER side declares it with `RpcComplexProperty<RpcList<Item>>`. A server-as-shadow needs the SHADOW container variant. Three options:
- (a) Reuse `RpcComplexProperty<T>` server-side; just freeze writes when the parent entity is `ShadowEntity` (check `entity.IsShadow` flag on every property write). **Plan choice.**
- (b) Force users to declare separate "shadow component" classes — bad UX.
- (c) Runtime swap container type — too magical.

Choice (a) means: existing `RpcPlaintProperty<T>.Val = x` setter checks `if (this.Owner.IsShadow) throw new InvalidOperationException(...)`. Add an `IsShadow` virtual property on `BaseEntity` (default false; `ShadowEntity` overrides true).

**Add IsShadow gate to property setters** — one focused commit (could be C1 or C2).

### R6. Test flakiness from TimeCircle phase
Property sync coalesces per tick. Shadow value visible after up to 1 tick latency. Use polling-with-timeout in C4 assertions (250 ms budget).

---

## Acceptance criteria

| ID | Verification | Method |
|---|---|---|
| V1 | `dotnet build LPS.sln` clean. | CI build. |
| V2 | Cluster boots normally without API calls (regression). | `proc.ps1 restart cluster` → 9/9 alive. |
| V3 | `scripts/recovery/assert_shadow_sync.ps1` exits 0. | Create shadow, mutate ori property, observe shadow updates, destroy shadow, confirm no further updates. |
| V4 | RPC to shadow MailBox throws `RpcException`. | Demo command attempts RPC against shadow MailBox; assert exception. |
| V5 | Shadow setter throws when business code attempts to write a shadow property locally. | Demo command tries `shadowEntity.SomeProp.Val = x`; assert `InvalidOperationException`. |
| V6 | No regression in existing client-side shadow. | Existing demo client tests pass (manual login + property change). |
| V7 | `proc.ps1` integration tests `kill_and_assert_reconnect*.ps1` still pass. | Re-run from #1. |
| V8 | StyleCop clean (`dotnet build` no new SA-warnings). |
| V9 | AGENTS.md updated; TODOs removed. |

---

## Estimated diff size

- 3 new `.proto` messages + regenerated `.cs`: +100 / 0 (generated).
- `ShadowEntity.cs`: +180.
- `RpcServerShadow{Plaint,Complex}Property.cs`: +140.
- `Server.cs` (+ partial): +120 (API + local registry + handlers).
- `Gate.ClientMessages.cs` + new `Gate.ShadowMessages.cs` partial: +90.
- `BaseEntity.IsShadow` + property setter guards: +30.
- Demo command for testing: +40.
- `scripts/recovery/assert_shadow_sync.ps1`: +120.
- AGENTS.md updates: +20.

**Total**: ~+840 lines, ~0 deletions (additive).

---

## Non-goals (explicit, v1)

- Cross-Gate shadows (ori behind Gate0, shadow behind Gate1).
- Shadow forwarding RPC to ori (per D1).
- Eager / lazy / declarative shadow creation (per D2).
- Migration integration (entity moves; shadows are NOT auto-rebound).
- Crash recovery (shadow stale after either side respawns; manual destroy + recreate).
- Component-level shadow declaration syntax (existing `ServerToShadow` flag is sufficient).
- Bandwidth throttling / batching across multiple oris.
- HostManager-level shadow registry (v1 keeps registry on Gate only).
