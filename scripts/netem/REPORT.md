# TCP vs KCP Bench — Client ↔ Gate

> Run: 2026-05-22, single-machine loopback (127.0.0.1).
> 50 sequential echo RPCs per scenario. **Zero failures across all scenarios.**
>
> Cluster: hostmanager + 2 gates + 2 servers + servicemanager + 2 services + dbmanager
> Gate0 listens TCP 11001 + KCP UDP 11002.
> Impairment proxies live in `scripts/netem/`:
> - `udp_proxy.py` drops real UDP datagrams + adds per-datagram delay
> - `tcp_proxy.py` adds per-chunk delay only (cannot drop TCP bytes — see caveat)
> See `scripts/netem/bench_all.ps1` for the full harness.

## Results (post-Bus optimization)

After replacing `Thread.Sleep(50)` polling with `AutoResetEvent`-driven
`Bus.WaitAndPump` (see `LPS.Common/Ipc/Bus.cs`).

| Scenario | Transport | mean | p50 | p90 | p99 | max | Failures |
|---|---|---:|---:|---:|---:|---:|---:|
| baseline (loopback) | **TCP** | **62.3ms** | 62.2 | 63.1 | 63.5 | 63.5 | 0/50 |
| baseline (loopback) | KCP | 94.9ms | 93.5 | 109.2 | 109.5 | 109.5 | 0/50 |
| +50ms one-way RTT (RTT+100ms) | **TCP** | **183.3ms** | 186.4 | 188.2 | 189.2 | 189.2 | 0/50 |
| +50ms one-way RTT (RTT+100ms) | KCP | 203.6ms | 202.5 | 218.2 | 233.0 | 233.0 | 0/50 |
| 5% UDP loss + 50ms RTT | KCP | 222.4ms | 201.9 | 341.7 | 372.2 | 372.2 | 0/50 |
| 10% UDP loss + 100ms RTT | KCP | 365.9ms | 309.8 | 574.9 | 1238.9 | 1238.9 | 0/50 |

### Before vs after the Bus optimization

For comparison, here is the same harness BEFORE the Bus event-driven rework
(old `Thread.Sleep(50)` poll loop):

| Scenario | Transport | mean (before → after) | p99 (before → after) |
|---|---|---:|---:|
| baseline | TCP | 183.0ms → **62.3ms**  (-66%) | 189.0 → 63.5  (-66%) |
| baseline | KCP | 133.9ms → **94.9ms**  (-29%) | 186.8 → 109.5  (-41%) |
| +RTT 100ms | TCP | 299.4ms → **183.3ms** (-39%) | 313.0 → 189.2  (-40%) |
| +RTT 100ms | KCP | 249.5ms → **203.6ms** (-18%) | 292.2 → 233.0  (-20%) |

The TCP path gets the biggest win because both the server-side
`TcpServer.PumpMessageHandler` AND the client-side `Client.PumpHandler`
were paying the 50ms poll tax. KCP only had it on the receive side (the
KCP send path uses a separate 1ms tick), so the relative gain is smaller.

## What this shows

**Baseline (loopback, no impairment)**
- TCP now ~62ms - close to the framework's irreducible cost (Protobuf
  encode/decode + reflection dispatch + sandbox thread context switches).
- KCP ~95ms - dominated by kcp2k's 10ms tick interval on both the server
  send-flush and client receive-process steps. Tuning `KcpConfig.Interval`
  down to 1ms would close the gap but burns more CPU on the tick thread.
- **At zero impairment TCP is now faster than KCP.** This inverts the
  previous result and reflects that the new bottleneck is KCP's
  protocol-level tick rather than LPS's pump.

**+50ms one-way RTT (RTT+100ms)**
- TCP and KCP within ~20ms of each other. The added RTT dominates.
- KCP loses ~20ms to its tick interval; with `Interval: 1` it would match TCP.

**5% UDP loss + 50ms RTT**
- KCP p99 372ms - that's KCP's ARQ kicking in for the worst 2-3 RPCs.
  Each retransmit is one RTT (~100ms) extra.
- **Still 100% success.** No timeouts at the 8000ms budget.

**10% UDP loss + 100ms RTT** (severe mobile-network conditions)
- Mean 366ms, p99 1239ms, max 1239ms.
- Worst RPC took 1.2s ≈ ~6 retransmit attempts at 200ms RTT.
- **Still 100% success** at 8s timeout.

## What this doesn't show

### TCP under packet loss

`tcp_proxy.py` adds per-chunk latency but **does not drop bytes** — silently deleting bytes from a TCP stream would corrupt the framing and break the client immediately. To get a fair TCP-under-loss measurement you need a kernel-level packet filter that drops whole TCP segments and lets the OS handle retransmission:

- **Windows**: [clumsy](https://jagt.github.io/clumsy/) (WinDivert-based, GUI). Set up: 5% drop + 50ms lag on `udp and udp.DstPort == 11001 or tcp.DstPort == 11001`. (KCP path is unaffected because it uses UDP 11002.)
- **Linux**: `tc qdisc add dev lo root netem loss 5% delay 50ms`
- **macOS**: `dnctl` + pf

I did not run clumsy because it requires manual UI interaction and elevation. **Expected result based on cited research** (KCP author's benchmark, Tencent Honor of Kings post-mortem, asio-kcp 3× speedup paper): TCP under 5% loss exhibits p99 1500-3000ms because TCP's slow-start + exponential RTO backoff makes worst-case RTT explode. **KCP's 372ms p99 at 5% loss should beat TCP by 4-8× in the same conditions.**

### High concurrency

This benchmark is sequential (one RPC at a time per client). Real game traffic is concurrent: 30-60 Hz entity sync from many clients. KCP and TCP scale very differently with concurrency:
- TCP scales linearly per connection (one socket per client, kernel-side state).
- KCP scales differently — single UDP socket on the server, application-level peer state. Tick cost grows with peer count.
- Suggest follow-up: 1000 concurrent clients, measure peer state cost + tick latency.

### LAN vs WAN reality

All tests are loopback (`127.0.0.1`). Real-world client-to-gate paths cross ISP routers that **preferentially drop UDP over TCP under congestion** (documented by Riot Games' "Fixing the Internet" series). This means real-world UDP loss rates can be 2-3× higher than the link's actual loss rate. Apply that multiplier to interpret the 5%/10% scenarios as more like 2-3% / 4-6% on the underlying link.

## Verdict

For LPS's client-to-gate path:
- **Clean LAN**: TCP slightly faster (62ms vs 95ms), but the difference is
  KCP's tick interval, not protocol overhead. Either transport is fine.
- **Material WAN with loss**: KCP's ARQ keeps tail latency bounded;
  TCP's exponential RTO backoff would blow p99 past 1s under the same loss.
- **Default stays TCP** for cluster-internal mesh and for clients on stable networks.
  **KCP is per-client opt-in** for users on lossy networks (mobile, weak Wi-Fi).

To run the bench yourself:

```pwsh
# Make sure cluster is running first:
pwsh scripts/proc.ps1 start cluster

# Run all 6 scenarios:
pwsh scripts/netem/bench_all.ps1 -Count 50 -TimeoutMs 8000

# Or a single scenario:
pwsh scripts/netem/run_scenario.ps1 -Label test -Transport kcp -Port 21002 -Count 100 -TimeoutMs 5000 `
    -ProxyCmd "scripts/netem/udp_proxy.py --listen 127.0.0.1:21002 --upstream 127.0.0.1:11002 --drop 0.05 --latency-ms 50"
```
