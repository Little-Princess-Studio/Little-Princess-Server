const BaseApi = "https://localhost:7087/api/web-manager";

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
