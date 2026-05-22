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
