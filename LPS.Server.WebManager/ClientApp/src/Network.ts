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
