"""
Bidirectional TCP proxy with configurable per-byte-chunk latency.

Usage:
    python tcp_proxy.py --listen 127.0.0.1:21001 --upstream 127.0.0.1:11001 \
        --latency-ms 50 --jitter-ms 5

NOTE: This proxy does NOT drop bytes. TCP is a byte stream - silently
deleting bytes would corrupt the stream and break the client. To inject
real TCP packet loss you need a kernel-level packet filter (clumsy /
WinDivert on Windows, tc/netem on Linux). The proxy gives TCP a *fair*
RTT comparison but does not test its ARQ behavior under loss.
"""

import argparse
import asyncio
import random
import time


class TcpProxy:
    def __init__(
        self,
        listen_host: str,
        listen_port: int,
        upstream_host: str,
        upstream_port: int,
        latency_ms: float,
        jitter_ms: float,
    ) -> None:
        self.listen_host = listen_host
        self.listen_port = listen_port
        self.upstream_host = upstream_host
        self.upstream_port = upstream_port
        self.latency_ms = latency_ms
        self.jitter_ms = jitter_ms
        self.c2s_bytes = 0
        self.s2c_bytes = 0
        self.started = time.time()

    def _delay(self) -> float:
        d = self.latency_ms + random.uniform(-self.jitter_ms, self.jitter_ms)
        return max(0.0, d) / 1000.0

    async def _pipe(
        self,
        reader: asyncio.StreamReader,
        writer: asyncio.StreamWriter,
        counter_name: str,
    ) -> None:
        try:
            while True:
                data = await reader.read(4096)
                if not data:
                    break
                # Latency = inject before each chunk is forwarded. This
                # approximates the per-segment RTT one-way add.
                d = self._delay()
                if d > 0:
                    await asyncio.sleep(d)
                writer.write(data)
                await writer.drain()
                setattr(self, counter_name, getattr(self, counter_name) + len(data))
        except (ConnectionResetError, BrokenPipeError, asyncio.IncompleteReadError):
            pass
        finally:
            try:
                writer.close()
            except Exception:
                pass

    async def handle_client(
        self,
        client_reader: asyncio.StreamReader,
        client_writer: asyncio.StreamWriter,
    ) -> None:
        peer = client_writer.get_extra_info("peername")
        print(f"[tcp-proxy] accepted {peer}")
        try:
            up_reader, up_writer = await asyncio.open_connection(
                self.upstream_host, self.upstream_port
            )
        except OSError as e:
            print(f"[tcp-proxy] upstream connect failed: {e}")
            client_writer.close()
            return

        await asyncio.gather(
            self._pipe(client_reader, up_writer, "c2s_bytes"),
            self._pipe(up_reader, client_writer, "s2c_bytes"),
            return_exceptions=True,
        )

    async def run(self) -> None:
        server = await asyncio.start_server(
            self.handle_client, self.listen_host, self.listen_port
        )
        print(
            f"[tcp-proxy] listening on tcp://{self.listen_host}:{self.listen_port} -> "
            f"tcp://{self.upstream_host}:{self.upstream_port}"
        )
        print(f"[tcp-proxy] latency={self.latency_ms}ms (+-{self.jitter_ms}ms)")
        print("[tcp-proxy] note: bytes are not dropped; use clumsy for TCP loss")

        async def _stats() -> None:
            while True:
                await asyncio.sleep(5)
                print(
                    f"[tcp-proxy] c->s={self.c2s_bytes}B s->c={self.s2c_bytes}B "
                    f"uptime={time.time()-self.started:.0f}s"
                )

        asyncio.create_task(_stats())
        async with server:
            await server.serve_forever()


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--listen", default="127.0.0.1:21001")
    p.add_argument("--upstream", default="127.0.0.1:11001")
    p.add_argument("--latency-ms", type=float, default=0.0)
    p.add_argument("--jitter-ms", type=float, default=0.0)
    args = p.parse_args()

    lh, lp = args.listen.split(":")
    uh, up = args.upstream.split(":")
    proxy = TcpProxy(lh, int(lp), uh, int(up), args.latency_ms, args.jitter_ms)
    try:
        asyncio.run(proxy.run())
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
