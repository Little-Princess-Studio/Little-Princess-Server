const BaseApi = "https://localhost:7087/api/web-manager";

export type ServerMailBox = {
    id: string,
    ip: string,
    port: number,
    hostnum: number,
}

export type ServerBasicInfo = {
    serverCnt: number;
    serverMailBoxes: ServerMailBox[];
};

export const queryServerInfo = (): Promise<ServerBasicInfo> => {
    return fetch(`${BaseApi}/server-basic-info`, {
        method: 'get',
    }).then(res => {
        return res.json();
    }).then(data => {
        console.log(data);
        if (data['res'] === 'Ok') {
            return data['serverInfo'] as ServerBasicInfo;
        }
        return { serverCnt: 0, serverMailBoxes: [] } as ServerBasicInfo;
    })
};
