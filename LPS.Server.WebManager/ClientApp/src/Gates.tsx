// -----------------------------------------------------------------------
// Gates index page. Lists every gate the HostManager knows about; each row
// links to /gate/:gateId/:hostNum for the live detail view.
// -----------------------------------------------------------------------
import { Stack, MessageBar, MessageBarType, DefaultButton, IColumn, DetailsList, SelectionMode, DetailsListLayoutMode } from "@fluentui/react";
import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import { ClusterOverview, InstanceStatusEntry, queryClusterOverview } from "./Network";

const STATUS_LABELS: Record<number, { label: string; color: string }> = {
    0: { label: "None", color: "#a19f9d" },
    1: { label: "Alive", color: "#107c10" },
    2: { label: "WaitingPong", color: "#797775" },
    3: { label: "Dead", color: "#d13438" },
};

const GatesPage: React.FunctionComponent = () => {
    const navigate = useNavigate();
    const [overview, setOverview] = useState<ClusterOverview | undefined>(undefined);
    const [error, setError] = useState<string | undefined>(undefined);

    const refresh = useCallback(async () => {
        try {
            setOverview(await queryClusterOverview());
            setError(undefined);
        } catch (e: any) {
            setError(`refresh failed: ${e.message ?? e}`);
        }
    }, []);

    useEffect(() => {
        refresh();
        const id = window.setInterval(refresh, 2000);
        return () => window.clearInterval(id);
    }, [refresh]);

    const columns: IColumn[] = [
        { key: "id", name: "Id", minWidth: 200, maxWidth: 340,
          onRender: (e: InstanceStatusEntry) => <span css={css`font-family:Consolas,monospace;font-size:12px;`}>{e.id}</span> },
        { key: "addr", name: "Address", minWidth: 150,
          onRender: (e: InstanceStatusEntry) => <span>{e.ip}:{e.port}#{e.hostNum}</span> },
        { key: "status", name: "Status", minWidth: 110,
          onRender: (e: InstanceStatusEntry) => {
              const s = STATUS_LABELS[e.status] ?? { label: `?(${e.status})`, color: "#605e5c" };
              return <span css={css`display:inline-block;padding:2px 8px;border-radius:10px;color:white;background:${s.color};font-size:12px;font-weight:600;`}>{s.label}</span>;
          } },
        { key: "actions", name: "", minWidth: 100,
          onRender: (e: InstanceStatusEntry) => (
              <DefaultButton text="Detail" iconProps={{ iconName: "OpenInNewTab" }}
                  onClick={() => navigate(`/gate/${encodeURIComponent(e.id)}/${e.hostNum}`)} />
          ) },
    ];

    return (
        <Stack horizontal={false} css={css`margin-top: 50px;`}>
            <NavBar index={2} />
            <h2 css={header}>Gates</h2>
            {error && <MessageBar messageBarType={MessageBarType.error} css={css`margin: 0 1em;`}>{error}</MessageBar>}
            <div css={css`margin: 0 1em;`}>
                {overview
                    ? <DetailsList items={overview.gates} columns={columns}
                        selectionMode={SelectionMode.none} layoutMode={DetailsListLayoutMode.justified} compact />
                    : <span css={css`color:#605e5c;`}>Loading…</span>}
            </div>
        </Stack>
    );
};

export default GatesPage;
