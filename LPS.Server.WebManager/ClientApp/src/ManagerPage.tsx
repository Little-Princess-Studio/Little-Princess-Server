// -----------------------------------------------------------------------
// Cluster overview - landing page for the WebManager.
// One-shot fetch of every instance HostManager tracks, plus 2s polling.
// -----------------------------------------------------------------------

import { Stack, MessageBar, MessageBarType, Toggle, DefaultButton, IColumn, DetailsList, SelectionMode, DetailsListLayoutMode, Icon } from "@fluentui/react";
import { useCallback, useEffect, useState } from "react";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import ShutdownButton from "./ShutdownButton";
import { ClusterOverview, InstanceStatusEntry, ServiceEntry, ServiceShardEntry, ServicesRoster, queryClusterOverview, queryServicesRoster } from "./Network";

const POLL_INTERVAL_MS = 2000;

// Mirrors LPS.Server.Instance.InstanceStatusType
const STATUS_LABELS: Record<number, { label: string; color: string }> = {
    0: { label: "None",       color: "#a19f9d" },
    1: { label: "Alive",      color: "#107c10" },
    2: { label: "WaitingPong", color: "#797775" },
    3: { label: "Dead",       color: "#d13438" },
};

const StatusBadge: React.FunctionComponent<{ status: number }> = ({ status }) => {
    const s = STATUS_LABELS[status] ?? { label: `?(${status})`, color: "#605e5c" };
    return (
        <span css={css`
            display: inline-block;
            padding: 2px 8px;
            border-radius: 10px;
            color: white;
            background: ${s.color};
            font-size: 12px;
            font-weight: 600;
        `}>{s.label}</span>
    );
};

const formatMailbox = (e: InstanceStatusEntry) => `${e.ip}:${e.port}#${e.hostNum}`;

// Factory: columns include a per-row "Stop" button whose instanceType is
// fixed per section (Gate/Server/ServiceManager/Service). The button's onClick
// fires a graceful shutdown HostCommand at the row's mailbox id.
const buildInstanceColumns = (
    instanceType: "Gate" | "Server" | "ServiceManager" | "Service",
    onShutdown: () => void,
): IColumn[] => [
    {
        key: "id", name: "Id", minWidth: 200, maxWidth: 340,
        onRender: (e: InstanceStatusEntry) => (
            <span css={css`font-family: Consolas, monospace; font-size: 12px;`}>{e.id}</span>
        ),
    },
    {
        key: "addr", name: "Address", minWidth: 150,
        onRender: (e: InstanceStatusEntry) => <span>{formatMailbox(e)}</span>,
    },
    {
        key: "status", name: "Status", minWidth: 110,
        onRender: (e: InstanceStatusEntry) => <StatusBadge status={e.status} />,
    },
    {
        key: "lastHeartBeat", name: "Last Heartbeat", minWidth: 180,
        onRender: (e: InstanceStatusEntry) => {
            // 0001-01-01 means "never received a pong yet" (default(DateTime))
            const t = new Date(e.lastHeartBeat);
            if (t.getUTCFullYear() < 2000) {
                return <span css={css`color: #a19f9d;`}>—</span>;
            }
            return <span>{t.toLocaleTimeString()}</span>;
        },
    },
    {
        key: "actions", name: "", minWidth: 320,
        onRender: (e: InstanceStatusEntry) => (
            <ShutdownButton
                instanceLabel={`${instanceType} ${e.id}`}
                instanceType={instanceType}
                instanceId={e.id}
                onShutdown={onShutdown}
            />
        ),
    },
];

const SectionBlock: React.FunctionComponent<{
    title: string;
    items: InstanceStatusEntry[];
    instanceType: "Gate" | "Server" | "ServiceManager" | "Service";
    onShutdown: () => void;
}> = ({ title, items, instanceType, onShutdown }) => (
    <div css={css`margin: 0 1em 1.5em;`}>
        <h3 css={css`margin: 0.5em 0; color: #323130;`}>
            {title} <span css={css`color: #605e5c; font-weight: 400; font-size: 14px;`}>({items.length})</span>
        </h3>
        {items.length === 0
            ? <div css={css`color: #a19f9d; font-style: italic; margin-left: 0.5em;`}>(no instances)</div>
            : <DetailsList
                items={items}
                columns={buildInstanceColumns(instanceType, onShutdown)}
                selectionMode={SelectionMode.none}
                layoutMode={DetailsListLayoutMode.justified}
                compact
            />}
    </div>
);

const buildShardColumns = (onShutdown: () => void): IColumn[] => [
    {
        key: "shard", name: "Shard", minWidth: 60, maxWidth: 80,
        onRender: (s: ServiceShardEntry) => (
            <span css={css`font-weight: 600;`}>#{s.shard}</span>
        ),
    },
    {
        key: "id", name: "Entity Id", minWidth: 200, maxWidth: 340,
        onRender: (s: ServiceShardEntry) => (
            <span css={css`font-family: Consolas, monospace; font-size: 12px;`}>{s.id}</span>
        ),
    },
    {
        key: "addr", name: "Owner Address", minWidth: 150,
        onRender: (s: ServiceShardEntry) => <span>{s.ip}:{s.port}#{s.hostNum}</span>,
    },
    {
        key: "actions", name: "", minWidth: 320,
        onRender: (s: ServiceShardEntry) => (
            // Shutting down a Service via any shard id stops the whole
            // host-process that owns that shard (one Service host = one
            // OS process, hosting all its shards).
            <ShutdownButton
                instanceLabel={`Service host owning shard #${s.shard} (${s.id})`}
                instanceType="Service"
                instanceId={s.id}
                onShutdown={onShutdown}
            />
        ),
    },
];

const ServicesSection: React.FunctionComponent<{ roster: ServicesRoster | undefined; onShutdown: () => void }> = ({ roster, onShutdown }) => {
    if (!roster) {
        return (
            <div css={css`margin: 0 1em 1.5em;`}>
                <h3 css={css`margin: 0.5em 0; color: #323130;`}>Service Shards</h3>
                <div css={css`color: #a19f9d; font-style: italic; margin-left: 0.5em;`}>
                    (ServiceManager unreachable or not deployed)
                </div>
            </div>
        );
    }

    const totalShards = roster.services.reduce((acc, s) => acc + s.shardCount, 0);

    return (
        <div css={css`margin: 0 1em 1.5em;`}>
            <h3 css={css`margin: 0.5em 0; color: #323130;`}>
                Service Shards
                <span css={css`color: #605e5c; font-weight: 400; font-size: 14px;`}>
                    &nbsp;({roster.services.length} services / {totalShards} shards
                    &nbsp;via&nbsp; {roster.serviceManager.name} {roster.serviceManager.ip}:{roster.serviceManager.port})
                </span>
            </h3>
            {roster.services.length === 0
                ? <div css={css`color: #a19f9d; font-style: italic; margin-left: 0.5em;`}>(no services registered)</div>
                : roster.services.map((svc: ServiceEntry) => (
                    <div key={svc.name} css={css`margin: 0.5em 0 1em; padding: 0.5em 0.75em; border-left: 3px solid ${svc.allShardReady ? "#107c10" : "#ca5010"}; background: #faf9f8; border-radius: 2px;`}>
                        <div css={css`font-weight: 600; margin-bottom: 4px;`}>
                            {svc.name}
                            <span css={css`color: #605e5c; font-weight: 400; font-size: 12px; margin-left: 8px;`}>
                                {svc.allShardReady
                                    ? `all ${svc.shardCount} shards ready`
                                    : `${svc.shards.length}/${svc.shardCount} ready · waiting on shard(s) ${svc.unreadyShards.join(", ")}`}
                            </span>
                        </div>
                        {svc.shards.length > 0 && (
                            <DetailsList
                                items={svc.shards}
                                columns={buildShardColumns(onShutdown)}
                                selectionMode={SelectionMode.none}
                                layoutMode={DetailsListLayoutMode.justified}
                                compact
                            />
                        )}
                    </div>
                ))}
        </div>
    );
};

const ManagerPage: React.FunctionComponent = () => {
    const [overview, setOverview] = useState<ClusterOverview | undefined>(undefined);
    const [roster, setRoster] = useState<ServicesRoster | undefined>(undefined);
    const [autoRefresh, setAutoRefresh] = useState<boolean>(true);
    const [error, setError] = useState<string | undefined>(undefined);
    const [lastFetch, setLastFetch] = useState<Date | undefined>(undefined);

    const refresh = useCallback(async () => {
        try {
            // Two RPCs in parallel: HostManager has gates/servers/svcmgrs,
            // ServiceManager has the service shard roster. We render both
            // even if the roster call fails (cluster may have no ServiceMgr).
            const [o, rRes] = await Promise.allSettled([
                queryClusterOverview(),
                queryServicesRoster(),
            ]);
            if (o.status === "fulfilled") setOverview(o.value); else throw o.reason;
            setRoster(rRes.status === "fulfilled" ? rRes.value : undefined);
            setLastFetch(new Date());
            setError(undefined);
        } catch (e: any) {
            setError(`refresh failed: ${e.message ?? e}`);
        }
    }, []);

    useEffect(() => { refresh(); }, [refresh]);
    useEffect(() => {
        if (!autoRefresh) return;
        const id = window.setInterval(refresh, POLL_INTERVAL_MS);
        return () => window.clearInterval(id);
    }, [autoRefresh, refresh]);

    return (
        <Stack horizontal={false} css={css`margin-top: 50px;`}>
            <NavBar index={0} />
            <h2 css={header}>Cluster Overview</h2>

            <Stack horizontal tokens={{ childrenGap: 12 }} css={css`margin: 0 1em 1em;`} verticalAlign="center">
                <Toggle label="Auto refresh (2s)" checked={autoRefresh} onChange={(_, c) => setAutoRefresh(!!c)} inlineLabel />
                <DefaultButton text="Refresh now" iconProps={{ iconName: "refresh" }} onClick={refresh} />
                {lastFetch && (
                    <span css={css`color: #605e5c; font-size: 12px;`}>
                        last updated {lastFetch.toLocaleTimeString()}
                    </span>
                )}
            </Stack>

            {error && (
                <MessageBar messageBarType={MessageBarType.error} css={css`margin: 0 1em;`}>
                    {error}
                </MessageBar>
            )}

            {overview && (
                <>
                    <div css={css`margin: 0 1em 1em; padding: 0.75em 1em; background: #f3f2f1; border-left: 4px solid #0078d4; border-radius: 2px;`}>
                        <div css={css`font-size: 16px; font-weight: 600;`}>
                            <Icon iconName="Server" css={css`margin-right: 6px;`} />
                            HostManager &nbsp; {overview.hostManager.ip}:{overview.hostManager.port}#{overview.hostManager.hostNum}
                        </div>
                        <div css={css`color: #605e5c; font-size: 13px; margin-top: 4px;`}>
                            status <b>{overview.hostManager.status}</b>
                            &nbsp;&middot;&nbsp; desired servers: {overview.hostManager.desiredServerNum}
                            &nbsp;&middot;&nbsp; desired gates: {overview.hostManager.desiredGateNum}
                        </div>
                    </div>

                    <SectionBlock title="Gates" items={overview.gates} instanceType="Gate" onShutdown={refresh} />
                    <SectionBlock title="Servers" items={overview.servers} instanceType="Server" onShutdown={refresh} />
                    <SectionBlock title="Service Managers" items={overview.serviceManagers} instanceType="ServiceManager" onShutdown={refresh} />
                    <ServicesSection roster={roster} onShutdown={refresh} />
                </>
            )}

            {!overview && !error && (
                <div css={css`margin: 1em; color: #605e5c;`}>Loading…</div>
            )}
        </Stack>
    );
};

export default ManagerPage;
