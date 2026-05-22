// API base URL.
//   - In dev (SPA proxy at https://localhost:44403) we hit the ASP.NET host
//     directly because the SPA proxy passes Cookie/Auth through but does not
//     rewrite the API path.
//   - In a published build the SPA is served from the same origin as the API,
//     so a relative URL is correct.
//   - Override via REACT_APP_API_BASE if you need to point at a different host
//     (e.g. when running against a staging cluster).
const BaseApi =
    process.env.REACT_APP_API_BASE ??
    (process.env.NODE_ENV === "development"
        ? "https://localhost:7087/api/web-manager"
        : "/api/web-manager");

export type ServerMailBox = {
    id: string;
    ip: string;
    port: number;
    hostNum: number;
};

export const mailBoxToString = (mailbox: ServerMailBox): string => {
    return `${mailbox.id};${mailbox.ip}:${mailbox.port}:${mailbox.hostNum}`;
}

export type ServerBasicInfo = {
    serverCnt: number;
    serverMailBoxes: ServerMailBox[];
};

export const queryServerBasicInfo = (): Promise<ServerBasicInfo> => {
    return fetch(`${BaseApi}/server-basic-info`, {
        method: 'get',
    }).then(res => {
        return res.json();
    }).then(data => {
        console.log(data);
        if (data['res'] === 'Ok') {
            return data['serverInfo'] as ServerBasicInfo;
        }
        throw new Error('queryServerBasicInfo failed');
    });
};

export type ServerInfo = {
    name: string,
    mailbox: ServerMailBox,
    entitiesCnt: number,
    cellCnt: number,
};

export const querySingleServerInfo = (id: string, hostNum: number): Promise<ServerInfo> => {
    return fetch(`${BaseApi}/single-server-info?serverId=${id}&hostNum=${hostNum}`, {
        method: 'get',
    }).then(res => {
        return res.json();
    }).then(data => {
        console.log(data);
        if (data['res'] === 'Ok') {
            return data['serverDetailedInfo'] as ServerInfo;
        }
        throw new Error('queryServerInfo failed');
    });
}

export type EntityInfo = {
    id: string,
    mailbox: ServerMailBox,
    entityClassName: string,
    cellEntityId: string,
}

export const queryEntities = (serverId: string, hostNum: number): Promise<EntityInfo[]> => {
    return fetch(`${BaseApi}/all-entities?serverId=${serverId}&hostNum=${hostNum}`, {
        method: 'get',
    }).then(res => {
        return res.json();
    }).then(data => {
        console.log(data);
        if (data['res'] === 'Ok') {
            return data['entities'] as EntityInfo[];
        }
        throw new Error('queryEntities failed');
    });
}

export type ServerPingPingInfo = {
    srvPingPongInfo: {id: string, status: number}[]
}

export const queryAllServerPingPongInfo = (): Promise<ServerPingPingInfo> => {
    return fetch(`${BaseApi}/all-server-ping-ping-info`, {
        method: 'get',
    }).then(res => {
        return res.json();
    }).then(data => {
        console.log(data);
        if (data['res'] === 'Ok') {
            return data['srvPingPongInfo'] as ServerPingPingInfo;
        }
        throw new Error('queryAllServerPingPongInfo failed');
    });
}

// --- Logs ---------------------------------------------------------------

export type LogFileEntry = {
    name: string;
    fileName: string;
    sizeBytes: number;
    lastWriteUtc: string;
};

export type LogListResponse = {
    logsDirectory: string;
    exists: boolean;
    logs: LogFileEntry[];
};

export const queryLogList = (): Promise<LogListResponse> => {
    return fetch(`${BaseApi}/logs/list`, { method: 'get' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('queryLogList failed');
            return {
                logsDirectory: data['logsDirectory'],
                exists: data['exists'],
                logs: data['logs'] as LogFileEntry[],
            };
        });
};

export type LogTailResponse = {
    name: string;
    fileName: string;
    totalSize: number;
    returnedLines: number;
    truncated: boolean;
    lines: string[];
};

export const queryLogTail = (name: string, lines: number = 200): Promise<LogTailResponse> => {
    const url = `${BaseApi}/logs/tail?name=${encodeURIComponent(name)}&lines=${lines}`;
    return fetch(url, { method: 'get' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error(data['error'] ?? 'queryLogTail failed');
            return data as LogTailResponse;
        });
};

// --- Cluster overview ---------------------------------------------------

export type InstanceStatusEntry = {
    id: string;
    ip: string;
    port: number;
    hostNum: number;
    /** matches LPS.Server.Instance.InstanceStatusType */
    status: number;
    lastHeartBeat: string;
};

export type ClusterOverview = {
    hostManager: {
        ip: string;
        port: number;
        hostNum: number;
        desiredServerNum: number;
        desiredGateNum: number;
        status: string;
    };
    gates: InstanceStatusEntry[];
    servers: InstanceStatusEntry[];
    serviceManagers: InstanceStatusEntry[];
    services: InstanceStatusEntry[];
};

export const queryClusterOverview = (): Promise<ClusterOverview> => {
    return fetch(`${BaseApi}/cluster-overview`, { method: 'get' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('queryClusterOverview failed');
            return data['overview'] as ClusterOverview;
        });
};

// --- Services roster (ServiceManager) ----------------------------------

export type ServiceShardEntry = {
    shard: number;
    id: string;
    ip: string;
    port: number;
    hostNum: number;
};

export type ServiceEntry = {
    name: string;
    shardCount: number;
    allShardReady: boolean;
    unreadyShards: number[];
    shards: ServiceShardEntry[];
};

export type ServicesRoster = {
    serviceManager: {
        name: string;
        ip: string;
        port: number;
        hostNum: number;
    };
    services: ServiceEntry[];
};

export const queryServicesRoster = (): Promise<ServicesRoster> => {
    return fetch(`${BaseApi}/services-roster`, { method: 'get' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('queryServicesRoster failed');
            return data['roster'] as ServicesRoster;
        });
};

// --- Gate detailed info -------------------------------------------------

export type GateMailBox = {
    id: string;
    ip: string;
    port: number;
    hostNum: number;
};

export type GateConnectionEntry = GateMailBox;

export type GateClientEntityEntry = {
    entityId: string;
    mailbox: GateMailBox;
};

export type GateDetailedInfo = {
    name: string;
    mailbox: GateMailBox;
    serviceManager: GateMailBox;
    counters: {
        serverConnections: number;
        gateConnections: number;
        clientEntities: number;
        pendingClientAuths: number;
        sendQueueDepth: number;
        readyToPumpClients: boolean;
    };
    serverConnections: GateConnectionEntry[];
    gateConnections: GateConnectionEntry[];
    clientEntities: GateClientEntityEntry[];
};

export const queryGateDetailedInfo = (gateId: string, hostNum: number): Promise<GateDetailedInfo> => {
    const url = `${BaseApi}/gate-detailed-info?gateId=${encodeURIComponent(gateId)}&hostNum=${hostNum}`;
    return fetch(url, { method: 'get' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('queryGateDetailedInfo failed');
            return data['gate'] as GateDetailedInfo;
        });
};

// --- Service shard detailed info ---------------------------------------

export type ServiceShardRpcParam = { name: string; type: string };
export type ServiceShardRpcMethod = {
    name: string;
    authority: string;
    returnType: string;
    parameters: ServiceShardRpcParam[];
};

export type ServiceShardDetailedInfo = {
    serviceName: string;
    serviceClass: string;
    shard: number;
    typeId: number;
    shardMailbox: GateMailBox;
    hostMailbox: { name: string; ip: string; port: number; hostNum: number };
    coLocatedShards: { serviceName: string; shard: number; shardId: string }[];
    rpcMethods: ServiceShardRpcMethod[];
};

export const queryServiceShardDetailedInfo = (serviceName: string, shard: number): Promise<ServiceShardDetailedInfo> => {
    const url = `${BaseApi}/service-shard-detailed-info?serviceName=${encodeURIComponent(serviceName)}&shard=${shard}`;
    return fetch(url, { method: 'get' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('queryServiceShardDetailedInfo failed');
            return data['shard'] as ServiceShardDetailedInfo;
        });
};

// --- Entity property dump ----------------------------------------------

export type EntityPropertyEntry = {
    name: string;
    setting: string;
    containerType: string;
    value: any;
};

export type EntityPropertyDump = {
    entityId: string;
    entityClassName: string;
    mailbox: { id: string; ip: string; port: number; hostNum: number };
    cellEntityId: string;
    isFrozen: boolean;
    isDestroyed: boolean;
    properties: EntityPropertyEntry[];
};

export const queryEntityPropertyDump = (entityId: string): Promise<EntityPropertyDump> => {
    const url = `${BaseApi}/entity-property-dump?entityId=${encodeURIComponent(entityId)}`;
    return fetch(url, { method: 'get' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('queryEntityPropertyDump failed');
            return data['entity'] as EntityPropertyDump;
        });
};

// --- Time-series metrics -----------------------------------------------

export type TsPoint = { t: number; v: number };

export type MetricsTimeSeries = {
    series: {
        aliveGates: TsPoint[];
        aliveServers: TsPoint[];
        aliveServiceManagers: TsPoint[];
        aliveServices: TsPoint[];
        pingSuccessRate: TsPoint[];
    };
    intervalMs: number;
    capacity: number;
};

export const queryMetricsTimeSeries = (): Promise<MetricsTimeSeries> => {
    return fetch(`${BaseApi}/metrics-time-series`, { method: 'get' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('queryMetricsTimeSeries failed');
            return data['metrics'] as MetricsTimeSeries;
        });
};

// --- Instance shutdown (graceful) --------------------------------------
//
// Sent to HostManager. HostManager either delivers a HostCommand to the
// target instance directly (TCP fallback to MQ for the targeted exchange)
// or - for Service - forwards through ServiceManager as a
// ServiceManagerCommand. The target runs its DrainForShutdown then calls
// Environment.Exit(0) so StartupManager's auto-restart logic (which only
// respawns on non-zero exit codes) treats the shutdown as intentional.
//
// instanceType is one of: 'Gate' | 'Server' | 'ServiceManager' | 'Service'.
// 'HostManager' is rejected (would orphan the control plane).
// timeoutMs of 0 means "use the receiver's default" (currently 10000).

export type ShutdownInstanceResult = {
    accepted: boolean;
    transport?: string;
    reason?: string;
};

export const shutdownInstance = (
    instanceType: string,
    instanceId: string,
    timeoutMs: number = 0,
): Promise<ShutdownInstanceResult> => {
    const url = `${BaseApi}/shutdown-instance?instanceType=${encodeURIComponent(instanceType)}`
        + `&instanceId=${encodeURIComponent(instanceId)}&timeoutMs=${timeoutMs}`;
    return fetch(url, { method: 'post' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('shutdownInstance failed');
            return data['result'] as ShutdownInstanceResult;
        });
};

// --- Supervisor (LPS.Server.Demo launcher) ------------------------------
//
// The supervisor lives in the launcher process (port 7090 by default) and
// owns the OS-process registry. It can spawn/kill instances by name and
// drive cluster-wide start/stop/restart. WebManager talks to it DIRECTLY
// (CORS-enabled) rather than proxying through ASP.NET because the launcher
// must remain reachable even when WebManager is down (e.g. user wants to
// restart WebManager itself via the supervisor in a future iteration).
//
// Override via REACT_APP_SUPERVISOR_BASE if the launcher runs on a
// different host.

const SupervisorBase =
    process.env.REACT_APP_SUPERVISOR_BASE ?? 'http://localhost:7090/supervisor';

export type SupervisorInstance = {
    name: string;
    type: string;
    alive: boolean;
    pid: number;
    hasExited: boolean;
};

export type SupervisorStatus = {
    instances: SupervisorInstance[];
};

export const querySupervisorStatus = (): Promise<SupervisorStatus> => {
    return fetch(`${SupervisorBase}/status`)
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error('querySupervisorStatus failed');
            return { instances: data['instances'] as SupervisorInstance[] };
        });
};

const supervisorPost = (path: string): Promise<any> =>
    fetch(`${SupervisorBase}${path}`, { method: 'POST' })
        .then(r => r.json())
        .then(data => {
            if (data['res'] !== 'Ok') throw new Error(`${path} failed`);
            return data;
        });

export const clusterStart = (): Promise<{ startedCount: number }> =>
    supervisorPost('/cluster/start');

export const clusterStop = (): Promise<{ stopped: boolean }> =>
    supervisorPost('/cluster/stop');

export const clusterRestart = (): Promise<{ startedCount: number }> =>
    supervisorPost('/cluster/restart');

export const instanceStart = (name: string): Promise<{ name: string; started: boolean }> =>
    supervisorPost(`/instance/${encodeURIComponent(name)}/start`);

export const instanceStop = (name: string): Promise<{ name: string; stopped: boolean }> =>
    supervisorPost(`/instance/${encodeURIComponent(name)}/stop`);

export const instanceRestart = (name: string): Promise<{ name: string; restarted: boolean }> =>
    supervisorPost(`/instance/${encodeURIComponent(name)}/restart`);
