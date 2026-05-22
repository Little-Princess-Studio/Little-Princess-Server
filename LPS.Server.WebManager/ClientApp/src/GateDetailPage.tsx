// -----------------------------------------------------------------------
// Single-Gate detail page. Routed as /gate/:gateId/:hostNum.
// Polls /api/web-manager/gate-detailed-info every 2s.
// -----------------------------------------------------------------------
import { Stack, MessageBar, MessageBarType, DefaultButton, IColumn, DetailsList, SelectionMode, DetailsListLayoutMode } from "@fluentui/react";
import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import { GateClientEntityEntry, GateConnectionEntry, GateDetailedInfo, queryGateDetailedInfo } from "./Network";

const Stat: React.FunctionComponent<{ label: string; value: React.ReactNode }> = ({ label, value }) => (
    <div css={css`background:#f3f2f1;padding:0.5em 0.9em;border-radius:4px;min-width:140px;`}>
        <div css={css`color:#605e5c;font-size:11px;text-transform:uppercase;letter-spacing:0.5px;`}>{label}</div>
        <div css={css`font-size:18px;font-weight:600;margin-top:2px;`}>{value}</div>
    </div>
);

const connectionColumns: IColumn[] = [
    { key: "id", name: "Mailbox Id", minWidth: 200, maxWidth: 340,
      onRender: (e: GateConnectionEntry) => <span css={css`font-family:Consolas,monospace;font-size:12px;`}>{e.id}</span> },
    { key: "addr", name: "Address", minWidth: 150,
      onRender: (e: GateConnectionEntry) => <span>{e.ip}:{e.port}#{e.hostNum}</span> },
];

const clientEntityColumns: IColumn[] = [
    { key: "entityId", name: "Entity Id", minWidth: 200, maxWidth: 340,
      onRender: (e: GateClientEntityEntry) => <span css={css`font-family:Consolas,monospace;font-size:12px;`}>{e.entityId}</span> },
    { key: "addr", name: "Mailbox Address", minWidth: 150,
      onRender: (e: GateClientEntityEntry) => <span>{e.mailbox.ip}:{e.mailbox.port}#{e.mailbox.hostNum}</span> },
];

const GateDetailPage: React.FunctionComponent = () => {
    const navigate = useNavigate();
    const params = useParams();
    const gateId = decodeURIComponent(params.gateId ?? "");
    const hostNum = parseInt(params.hostNum ?? "0", 10);

    const [info, setInfo] = useState<GateDetailedInfo | undefined>(undefined);
    const [error, setError] = useState<string | undefined>(undefined);

    const refresh = useCallback(async () => {
        try {
            setInfo(await queryGateDetailedInfo(gateId, hostNum));
            setError(undefined);
        } catch (e: any) {
            setError(`refresh failed: ${e.message ?? e}`);
        }
    }, [gateId, hostNum]);

    useEffect(() => {
        refresh();
        const id = window.setInterval(refresh, 2000);
        return () => window.clearInterval(id);
    }, [refresh]);

    return (
        <Stack horizontal={false} css={css`margin-top: 50px;`}>
            <NavBar index={2} />
            <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 12 }} css={css`margin: 1em;`}>
                <DefaultButton iconProps={{ iconName: "Back" }} text="Back to Gates" onClick={() => navigate("/gate")} />
                <h2 css={css`margin: 0;`}>Gate: <span css={css`font-family:Consolas,monospace;font-size:16px;`}>{gateId}</span></h2>
            </Stack>
            {error && <MessageBar messageBarType={MessageBarType.error} css={css`margin: 0 1em;`}>{error}</MessageBar>}
            {!info && !error && <div css={css`margin:1em;color:#605e5c;`}>Loading…</div>}
            {info && (
                <>
                    <div css={css`margin: 0 1em 1em;padding:0.75em 1em;background:#f3f2f1;border-left:4px solid #0078d4;border-radius:2px;`}>
                        <div css={css`font-weight:600;font-size:16px;`}>{info.name} &nbsp; {info.mailbox.ip}:{info.mailbox.port}#{info.mailbox.hostNum}</div>
                        <div css={css`color:#605e5c;font-size:13px;margin-top:4px;`}>
                            ServiceManager: {info.serviceManager.ip || "—"}:{info.serviceManager.port}
                        </div>
                    </div>

                    <Stack horizontal wrap tokens={{ childrenGap: 12 }} css={css`margin: 0 1em 1em;`}>
                        <Stat label="Server Conns" value={info.counters.serverConnections} />
                        <Stat label="Gate Conns" value={info.counters.gateConnections} />
                        <Stat label="Client Entities" value={info.counters.clientEntities} />
                        <Stat label="Pending Auths" value={info.counters.pendingClientAuths} />
                        <Stat label="Send Queue" value={info.counters.sendQueueDepth} />
                        <Stat label="Pump Ready" value={info.counters.readyToPumpClients ? "yes" : "no"} />
                    </Stack>

                    <h3 css={css`margin: 0.5em 1em;`}>Server Connections</h3>
                    <div css={css`margin: 0 1em 1em;`}>
                        <DetailsList items={info.serverConnections} columns={connectionColumns}
                            selectionMode={SelectionMode.none} layoutMode={DetailsListLayoutMode.justified} compact />
                    </div>

                    <h3 css={css`margin: 0.5em 1em;`}>Other Gate Connections</h3>
                    <div css={css`margin: 0 1em 1em;`}>
                        <DetailsList items={info.gateConnections} columns={connectionColumns}
                            selectionMode={SelectionMode.none} layoutMode={DetailsListLayoutMode.justified} compact />
                    </div>

                    <h3 css={css`margin: 0.5em 1em;`}>
                        Bound Client Entities <span css={css`color:#605e5c;font-weight:400;font-size:14px;`}>({info.clientEntities.length})</span>
                    </h3>
                    <div css={css`margin: 0 1em 1em;`}>
                        {info.clientEntities.length === 0
                            ? <span css={css`color:#a19f9d;font-style:italic;`}>(no clients connected)</span>
                            : <DetailsList items={info.clientEntities} columns={clientEntityColumns}
                                selectionMode={SelectionMode.none} layoutMode={DetailsListLayoutMode.justified} compact />}
                    </div>
                </>
            )}
        </Stack>
    );
};

export default GateDetailPage;
