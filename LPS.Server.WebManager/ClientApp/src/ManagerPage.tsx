// -----------------------------------------------------------------------
// Cluster overview - landing page for the WebManager.
// One-shot fetch of every instance HostManager tracks, plus 2s polling.
// -----------------------------------------------------------------------

import { Stack, MessageBar, MessageBarType, Toggle, DefaultButton, PrimaryButton, IColumn, DetailsList, SelectionMode, DetailsListLayoutMode, Icon, Spinner, SpinnerSize, Dialog, DialogType, DialogFooter } from "@fluentui/react";
import { useCallback, useEffect, useState } from "react";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import ShutdownButton from "./ShutdownButton";
import {
    ClusterOverview, InstanceStatusEntry, ServiceEntry, ServiceShardEntry, ServicesRoster,
    queryClusterOverview, queryServicesRoster,
    SupervisorInstance, querySupervisorStatus,
    clusterStart, clusterStop, clusterRestart,
    instanceStart, instanceStop, instanceRestart,
} from "./Network";

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

// ---------------- Phase 2/3: Supervisor control surface ----------------

// Cluster-wide control bar. Each button confirms (start is benign and
// skipped), then fires the matching /supervisor/cluster/* endpoint and
// nudges onChange so the parent re-polls.
const ClusterControlBar: React.FunctionComponent<{ onChange: () => void }> = ({ onChange }) => {
    const [pending, setPending] = useState<string | undefined>(undefined);
    const [confirm, setConfirm] = useState<"stop" | "restart" | undefined>(undefined);
    const [error, setError] = useState<string | undefined>(undefined);
    const [success, setSuccess] = useState<string | undefined>(undefined);

    const showSuccess = (msg: string) => {
        setSuccess(msg);
        window.setTimeout(() => setSuccess(undefined), 4000);
    };

    const doStart = useCallback(async () => {
        setPending("start"); setError(undefined);
        try {
            const r = await clusterStart();
            showSuccess(`Started ${r.startedCount} instance(s).`);
            onChange();
        } catch (e: any) {
            setError(`cluster/start failed: ${e.message ?? e}`);
        } finally { setPending(undefined); }
    }, [onChange]);

    const doStop = useCallback(async () => {
        setConfirm(undefined); setPending("stop"); setError(undefined);
        try {
            await clusterStop();
            showSuccess("All instances killed.");
            onChange();
        } catch (e: any) {
            setError(`cluster/stop failed: ${e.message ?? e}`);
        } finally { setPending(undefined); }
    }, [onChange]);

    const doRestart = useCallback(async () => {
        setConfirm(undefined); setPending("restart"); setError(undefined);
        try {
            const r = await clusterRestart();
            showSuccess(`Restarted ${r.startedCount} instance(s).`);
            onChange();
        } catch (e: any) {
            setError(`cluster/restart failed: ${e.message ?? e}`);
        } finally { setPending(undefined); }
    }, [onChange]);

    return (
        <>
            <Stack horizontal wrap tokens={{ childrenGap: 8 }} verticalAlign="center"
                css={css`margin: 0 1em 0.5em; padding: 0.6em 0.9em; background: #fff4ce; border-left: 4px solid #ffaa44; border-radius: 2px;`}>
                <span css={css`font-weight: 600; color: #323130;`}>
                    <Icon iconName="PowerButton" css={css`margin-right: 4px;`} />
                    Cluster Control
                </span>
                <PrimaryButton text="Start All" iconProps={{ iconName: "Play" }}
                    disabled={pending !== undefined} onClick={doStart} />
                <DefaultButton text="Restart All" iconProps={{ iconName: "Refresh" }}
                    disabled={pending !== undefined} onClick={() => setConfirm("restart")} />
                <DefaultButton text="Stop All" iconProps={{ iconName: "Stop" }}
                    disabled={pending !== undefined} onClick={() => setConfirm("stop")}
                    styles={{
                        root: { background: "#d83b01", borderColor: "#a4262c", color: "white" },
                        rootHovered: { background: "#a4262c", borderColor: "#a4262c", color: "white" },
                        rootDisabled: { background: "#e1bbb1", borderColor: "#c9a597", color: "#f3f2f1" },
                    }} />
                {pending && <Spinner size={SpinnerSize.small} label={`cluster/${pending}…`} />}
                {success && <MessageBar messageBarType={MessageBarType.success} isMultiline={false}>{success}</MessageBar>}
                {error && <MessageBar messageBarType={MessageBarType.error} isMultiline={false}
                    onDismiss={() => setError(undefined)}>{error}</MessageBar>}
            </Stack>

            <Dialog
                hidden={confirm === undefined}
                onDismiss={() => setConfirm(undefined)}
                dialogContentProps={{
                    type: DialogType.normal,
                    title: confirm === "stop" ? "Stop the entire cluster?" : "Restart the entire cluster?",
                    subText: confirm === "stop"
                        ? "Every subprocess (HostManager, Gates, Servers, Service Manager, Services, DbManager) will be force-killed. The supervisor remains running so you can Start them again."
                        : "Every subprocess will be force-killed and then respawned with the same config. Expect ~10s of unavailability.",
                }}
                modalProps={{ isBlocking: true }}
            >
                <DialogFooter>
                    <DefaultButton text="Cancel" onClick={() => setConfirm(undefined)} />
                    <PrimaryButton
                        text={confirm === "stop" ? "Stop everything" : "Restart everything"}
                        onClick={confirm === "stop" ? doStop : doRestart}
                        styles={{
                            root: { background: "#d83b01", borderColor: "#a4262c" },
                            rootHovered: { background: "#a4262c", borderColor: "#a4262c" },
                        }}
                    />
                </DialogFooter>
            </Dialog>
        </>
    );
};

// Per-process registry of every subprocess the launcher knows about. Names
// here (gate0, server1, dbmanager, ...) are the supervisor's primary key
// and are distinct from the mailbox.id used in the Cluster Overview tables.
// Dead rows stay visible so the user can press Start on them.
const ProcessSupervisorSection: React.FunctionComponent<{
    instances: SupervisorInstance[];
    onChange: () => void;
}> = ({ instances, onChange }) => {
    const [pendingName, setPendingName] = useState<string | undefined>(undefined);
    const [error, setError] = useState<string | undefined>(undefined);

    const wrap = useCallback(async (name: string, fn: () => Promise<unknown>) => {
        setPendingName(name); setError(undefined);
        try { await fn(); onChange(); }
        catch (e: any) { setError(`action on ${name} failed: ${e.message ?? e}`); }
        finally { setPendingName(undefined); }
    }, [onChange]);

    const columns: IColumn[] = [
        { key: "name", name: "Name", minWidth: 130, maxWidth: 180,
          onRender: (i: SupervisorInstance) =>
            <span css={css`font-family:Consolas,monospace;font-weight:600;`}>{i.name}</span> },
        { key: "type", name: "Type", minWidth: 90, maxWidth: 130,
          onRender: (i: SupervisorInstance) => <span>{i.type}</span> },
        { key: "pid", name: "PID", minWidth: 60, maxWidth: 80,
          onRender: (i: SupervisorInstance) => i.alive
            ? <span>{i.pid}</span>
            : <span css={css`color:#a19f9d;`}>—</span> },
        { key: "alive", name: "Status", minWidth: 90,
          onRender: (i: SupervisorInstance) => (
            <span css={css`display:inline-block;padding:2px 8px;border-radius:10px;color:white;font-size:12px;font-weight:600;background:${i.alive ? "#107c10" : "#605e5c"};`}>
                {i.alive ? "Alive" : "Stopped"}
            </span>
          ) },
        { key: "actions", name: "", minWidth: 320,
          onRender: (i: SupervisorInstance) => (
            <Stack horizontal tokens={{ childrenGap: 6 }}>
                {!i.alive && (
                    <PrimaryButton text="Start" iconProps={{ iconName: "Play" }}
                        disabled={pendingName !== undefined}
                        onClick={() => wrap(i.name, () => instanceStart(i.name))} />
                )}
                {i.alive && (
                    <>
                        <DefaultButton text="Restart" iconProps={{ iconName: "Refresh" }}
                            disabled={pendingName !== undefined}
                            onClick={() => wrap(i.name, () => instanceRestart(i.name))} />
                        <DefaultButton text="Kill" iconProps={{ iconName: "Cancel" }}
                            disabled={pendingName !== undefined}
                            onClick={() => wrap(i.name, () => instanceStop(i.name))}
                            styles={{
                                root: { background: "#a4262c", borderColor: "#751c20", color: "white" },
                                rootHovered: { background: "#751c20", borderColor: "#751c20", color: "white" },
                            }} />
                    </>
                )}
                {pendingName === i.name && <Spinner size={SpinnerSize.small} />}
            </Stack>
          ) },
    ];

    return (
        <div css={css`margin: 0 1em 1.5em;`}>
            <h3 css={css`margin: 0.5em 0; color: #323130;`}>
                Process Supervisor
                <span css={css`color:#605e5c;font-weight:400;font-size:14px;margin-left:8px;`}>
                    ({instances.filter(i => i.alive).length}/{instances.length} alive
                    &nbsp;·&nbsp; force-kill / spawn by name via launcher on :7090)
                </span>
            </h3>
            {error && (
                <MessageBar messageBarType={MessageBarType.error} onDismiss={() => setError(undefined)}>
                    {error}
                </MessageBar>
            )}
            <DetailsList
                items={instances}
                columns={columns}
                selectionMode={SelectionMode.none}
                layoutMode={DetailsListLayoutMode.justified}
                compact
            />
            <div css={css`color:#605e5c;font-size:12px;margin-top:6px;`}>
                Note: <b>Kill</b> is a hard SIGKILL (no drain). For a graceful shutdown
                use the per-instance <b>Stop</b> button in the Cluster Overview tables
                below (sends a HostCommand for in-band drain). <b>Restart</b> here is
                kill + re-spawn — also hard.
            </div>
        </div>
    );
};

const ManagerPage: React.FunctionComponent = () => {
    const [overview, setOverview] = useState<ClusterOverview | undefined>(undefined);
    const [roster, setRoster] = useState<ServicesRoster | undefined>(undefined);
    const [supervisor, setSupervisor] = useState<SupervisorInstance[]>([]);
    const [autoRefresh, setAutoRefresh] = useState<boolean>(true);
    const [error, setError] = useState<string | undefined>(undefined);
    const [lastFetch, setLastFetch] = useState<Date | undefined>(undefined);

    const refresh = useCallback(async () => {
        try {
            // Three RPCs in parallel: HostManager (gates/servers/svcmgrs),
            // ServiceManager (shard roster), Supervisor (OS-level process
            // registry on port 7090). Render whatever succeeds; the
            // Supervisor section degrades gracefully if the launcher's HTTP
            // is unreachable (e.g. running an older build).
            const [o, rRes, sRes] = await Promise.allSettled([
                queryClusterOverview(),
                queryServicesRoster(),
                querySupervisorStatus(),
            ]);
            if (o.status === "fulfilled") setOverview(o.value); else throw o.reason;
            setRoster(rRes.status === "fulfilled" ? rRes.value : undefined);
            setSupervisor(sRes.status === "fulfilled" ? sRes.value.instances : []);
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

            <ClusterControlBar onChange={refresh} />

            {error && (
                <MessageBar messageBarType={MessageBarType.error} css={css`margin: 0 1em;`}>
                    {error}
                </MessageBar>
            )}

            {supervisor.length > 0 && (
                <ProcessSupervisorSection instances={supervisor} onChange={refresh} />
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
