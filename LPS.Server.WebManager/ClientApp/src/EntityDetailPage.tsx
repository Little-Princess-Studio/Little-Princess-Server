// -----------------------------------------------------------------------
// Single-entity property dump page. Routed as /entity/:entityId.
// Backs the "click row in ServerPage/all-entities" UX.
// -----------------------------------------------------------------------
import { Stack, MessageBar, MessageBarType, DefaultButton, IColumn, DetailsList, SelectionMode, DetailsListLayoutMode } from "@fluentui/react";
import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { css } from "styled-components";
import NavBar from "./NavBar";
import { EntityPropertyDump, EntityPropertyEntry, queryEntityPropertyDump } from "./Network";

const renderValue = (v: any): string => {
    if (v === null || v === undefined) return "(null)";
    if (typeof v === "object") {
        try { return JSON.stringify(v, null, 2); } catch { return String(v); }
    }
    return String(v);
};

const columns: IColumn[] = [
    { key: "name", name: "Property", minWidth: 160, maxWidth: 240,
      onRender: (p: EntityPropertyEntry) => <span css={css`font-weight:600;font-family:Consolas,monospace;`}>{p.name}</span> },
    { key: "container", name: "Container", minWidth: 180, maxWidth: 260,
      onRender: (p: EntityPropertyEntry) => <span css={css`font-family:Consolas,monospace;font-size:12px;color:#605e5c;`}>{p.containerType}</span> },
    { key: "setting", name: "Setting", minWidth: 200,
      onRender: (p: EntityPropertyEntry) => <span css={css`color:#605e5c;font-size:12px;`}>{p.setting}</span> },
    { key: "value", name: "Value", minWidth: 250,
      onRender: (p: EntityPropertyEntry) => (
          <pre css={css`margin:0;padding:4px 6px;background:#faf9f8;border-radius:3px;font-family:Consolas,monospace;font-size:12px;white-space:pre-wrap;word-break:break-word;`}>
              {renderValue(p.value)}
          </pre>
      ) },
];

const EntityDetailPage: React.FunctionComponent = () => {
    const navigate = useNavigate();
    const params = useParams();
    const entityId = decodeURIComponent(params.entityId ?? "");
    const [dump, setDump] = useState<EntityPropertyDump | undefined>(undefined);
    const [error, setError] = useState<string | undefined>(undefined);

    const refresh = useCallback(async () => {
        try { setDump(await queryEntityPropertyDump(entityId)); setError(undefined); }
        catch (e: any) { setError(`refresh failed: ${e.message ?? e}`); }
    }, [entityId]);

    useEffect(() => {
        refresh();
        const id = window.setInterval(refresh, 2000);
        return () => window.clearInterval(id);
    }, [refresh]);

    return (
        <Stack horizontal={false} css={css`margin-top: 50px;`}>
            <NavBar index={1} />
            <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 12 }} css={css`margin: 1em;`}>
                <DefaultButton iconProps={{ iconName: "Back" }} text="Back" onClick={() => navigate(-1)} />
                <h2 css={css`margin:0;`}>Entity <span css={css`font-family:Consolas,monospace;font-size:16px;`}>{entityId}</span></h2>
            </Stack>
            {error && <MessageBar messageBarType={MessageBarType.error} css={css`margin: 0 1em;`}>{error}</MessageBar>}
            {!dump && !error && <div css={css`margin:1em;color:#605e5c;`}>Loading…</div>}
            {dump && (
                <>
                    <div css={css`margin:0 1em 1em;padding:0.75em 1em;background:#f3f2f1;border-left:4px solid ${dump.isFrozen ? "#797775" : "#0078d4"};border-radius:2px;`}>
                        <div css={css`font-weight:600;font-size:16px;`}>{dump.entityClassName}</div>
                        <div css={css`color:#605e5c;font-size:13px;margin-top:4px;`}>
                            {dump.mailbox.ip}:{dump.mailbox.port}#{dump.mailbox.hostNum}
                            {dump.cellEntityId && <> &nbsp;·&nbsp; cell: <span css={css`font-family:Consolas,monospace;`}>{dump.cellEntityId}</span></>}
                            &nbsp;·&nbsp; {dump.isFrozen ? "frozen" : "active"}
                            {dump.isDestroyed && <> &nbsp;·&nbsp; <span css={css`color:#d13438;`}>DESTROYED</span></>}
                        </div>
                    </div>
                    <h3 css={css`margin:0.5em 1em;`}>
                        RpcProperty Tree <span css={css`color:#605e5c;font-weight:400;font-size:14px;`}>({dump.properties.length})</span>
                    </h3>
                    <div css={css`margin:0 1em 1em;`}>
                        {dump.properties.length === 0
                            ? <span css={css`color:#a19f9d;font-style:italic;`}>(no RpcProperty members declared)</span>
                            : <DetailsList items={dump.properties} columns={columns}
                                selectionMode={SelectionMode.none} layoutMode={DetailsListLayoutMode.justified} compact />}
                    </div>
                </>
            )}
        </Stack>
    );
};

export default EntityDetailPage;
