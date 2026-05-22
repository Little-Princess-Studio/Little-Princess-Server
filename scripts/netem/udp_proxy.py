"""
Bidirectional UDP proxy with configurable drop/latency/jitter.

Usage:
    python udp_proxy.py --listen 127.0.0.1:21002 --upstream 127.0.0.1:11002 \
        --drop 0.05 --latency-ms 50 --jitter-ms 5

Drops datagrams (KCP sees lost packets and runs ARQ retransmit).
Adds per-datagram latency (uniform jitter around mean).
Symmetric: same impairment applied to both directions.

Implementation notes:
- One UDP socket "listen_sock" bound to --listen accepts client packets.
- Per-client upstream socket created on first packet (so multiple clients
  could in principle share, though for a single test client this is just
  the simplest 1:1 forward).
- Latency is implemented via asyncio.sleep(uniform()) before forwarding.
- We log a single-line summary every 5s so we can correlate with bench
  output (received / dropped / forwarded counters).
"""

import argparse
import asyncio
import random
import time


class Stats:
    """Atomic-ish counters; single asyncio loop so no locking needed."""

    def __init__(self) -> None:
        self.c2s_recv = 0
        self.c2s_drop = 0
        self.c2s_fwd = 0
        self.s2c_recv = 0
        self.s2c_drop = 0
        self.s2c_fwd = 0
        self.started = time.time()


class UdpProxy:
    def __init__(
        self,
        listen_host: str,
        listen_port: int,
        upstream_host: str,
        upstream_port: int,
        drop_rate: float,
        latency_ms: float,
        jitter_ms: float,
    ) -> None:
        self.listen = (listen_host, listen_port)
        self.upstream = (upstream_host, upstream_port)
        self.drop_rate = drop_rate
        self.latency_ms = latency_ms
        self.jitter_ms = jitter_ms
        self.stats = Stats()
        # Map: client addr -> upstream-socket protocol so server-bound replies
        # come back on a known channel.
        self.upstream_proto_for_client: dict[tuple[str, int], "UpstreamProtocol"] = {}
        self.listen_transport: asyncio.DatagramTransport | None = None

    def _impair(self) -> tuple[bool, float]:
        """Return (drop?, delay_seconds)."""
        if random.random() < self.drop_rate:
            return True, 0.0
        delay = max(
            0.0,
            (self.latency_ms + random.uniform(-self.jitter_ms, self.jitter_ms)) / 1000.0,
        )
        return False, delay

    async def _delayed_send(
        self, transport: asyncio.DatagramTransport, data: bytes, addr, delay: float
    ) -> None:
        if delay > 0:
            await asyncio.sleep(delay)
        try:
            transport.sendto(data, addr)
        except OSError:
            pass

    async def run(self) -> None:
        loop = asyncio.get_running_loop()
        # Listener side - receives from the game client.
        self.listen_transport, _ = await loop.create_datagram_endpoint(
            lambda: ListenProtocol(self),
            local_addr=self.listen,
        )
        print(f"[proxy] listening on udp://{self.listen[0]}:{self.listen[1]} -> udp://{self.upstream[0]}:{self.upstream[1]}")
        print(f"[proxy] drop={self.drop_rate*100:.1f}% latency={self.latency_ms}ms (+-{self.jitter_ms}ms)")

        # Periodic stats print.
        while True:
            await asyncio.sleep(5)
            s = self.stats
            print(
                f"[proxy] c->s recv={s.c2s_recv} drop={s.c2s_drop} fwd={s.c2s_fwd} | "
                f"s->c recv={s.s2c_recv} drop={s.s2c_drop} fwd={s.s2c_fwd}"
            )


class ListenProtocol(asyncio.DatagramProtocol):
    """Accepts datagrams from the game client. Spawns one upstream socket
    per distinct client address so server-side replies can be routed back."""

    def __init__(self, proxy: UdpProxy) -> None:
        self.proxy = proxy

    def datagram_received(self, data: bytes, addr) -> None:
        self.proxy.stats.c2s_recv += 1
        drop, delay = self.proxy._impair()
        if drop:
            self.proxy.stats.c2s_drop += 1
            return

        # Ensure upstream socket exists for this client.
        upstream_proto = self.proxy.upstream_proto_for_client.get(addr)
        if upstream_proto is None:
            loop = asyncio.get_running_loop()

            async def _setup() -> None:
                _t, p = await loop.create_datagram_endpoint(
                    lambda: UpstreamProtocol(self.proxy, addr),
                    remote_addr=self.proxy.upstream,
                )
                self.proxy.upstream_proto_for_client[addr] = p
                p.transport.sendto(data)
                self.proxy.stats.c2s_fwd += 1

            asyncio.create_task(_setup())
            return

        # Already have an upstream socket - forward after delay.
        async def _send() -> None:
            if delay > 0:
                await asyncio.sleep(delay)
            try:
                upstream_proto.transport.sendto(data)
                self.proxy.stats.c2s_fwd += 1
            except OSError:
                pass

        asyncio.create_task(_send())


class UpstreamProtocol(asyncio.DatagramProtocol):
    """Receives datagrams FROM the upstream (game server) and forwards them
    back to the original client through the listen transport."""

    def __init__(self, proxy: UdpProxy, client_addr) -> None:
        self.proxy = proxy
        self.client_addr = client_addr
        self.transport: asyncio.DatagramTransport | None = None

    def connection_made(self, transport) -> None:
        self.transport = transport

    def datagram_received(self, data: bytes, addr) -> None:
        self.proxy.stats.s2c_recv += 1
        drop, delay = self.proxy._impair()
        if drop:
            self.proxy.stats.s2c_drop += 1
            return

        async def _send() -> None:
            if delay > 0:
                await asyncio.sleep(delay)
            if self.proxy.listen_transport is not None:
                try:
                    self.proxy.listen_transport.sendto(data, self.client_addr)
                    self.proxy.stats.s2c_fwd += 1
                except OSError:
                    pass

        asyncio.create_task(_send())


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--listen", default="127.0.0.1:21002")
    p.add_argument("--upstream", default="127.0.0.1:11002")
    p.add_argument("--drop", type=float, default=0.0, help="0.0-1.0")
    p.add_argument("--latency-ms", type=float, default=0.0)
    p.add_argument("--jitter-ms", type=float, default=0.0)
    args = p.parse_args()

    lh, lp = args.listen.split(":")
    uh, up = args.upstream.split(":")
    proxy = UdpProxy(lh, int(lp), uh, int(up), args.drop, args.latency_ms, args.jitter_ms)
    try:
        asyncio.run(proxy.run())
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
