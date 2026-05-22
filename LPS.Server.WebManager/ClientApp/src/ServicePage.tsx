// -----------------------------------------------------------------------
// Services index page. Lists every service shard from ServiceManager.
// -----------------------------------------------------------------------
import { Stack, MessageBar, MessageBarType, DefaultButton, IColumn, DetailsList, SelectionMode, DetailsListLayoutMode } from "@fluentui/react";
import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import { ServicesRoster, ServiceShardEntry, queryServicesRoster } from "./Network";

type ShardRow = ServiceShardEntry & { serviceName: string };

const ServicePage: React.FunctionComponent = () => {
    const navigate = useNavigate();
    const [roster, setRoster] = useState<ServicesRoster | undefined>(undefined);
    const [error, setError] = useState<string | undefined>(undefined);

    const refresh = useCallback(async () => {
        try {
            setRoster(await queryServicesRoster());
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
        { key: "shard", name: "Shard", minWidth: 60, maxWidth: 80,
          onRender: (r: ShardRow) => <span css={css`font-weight:600;`}>#{r.shard}</span> },
        { key: "id", name: "Entity Id", minWidth: 200, maxWidth: 340,
          onRender: (r: ShardRow) => <span css={css`font-family:Consolas,monospace;font-size:12px;`}>{r.id}</span> },
        { key: "addr", name: "Owner Address", minWidth: 150,
          onRender: (r: ShardRow) => <span>{r.ip}:{r.port}#{r.hostNum}</span> },
        { key: "actions", name: "", minWidth: 100,
          onRender: (r: ShardRow) => (
              <DefaultButton text="Detail" iconProps={{ iconName: "OpenInNewTab" }}
                  onClick={() => navigate(`/service/${encodeURIComponent(r.serviceName)}/${r.shard}`)} />
          ) },
    ];

    return (
        <Stack horizontal={false} css={css`margin-top: 50px;`}>
            <NavBar index={3} />
            <h2 css={header}>Services</h2>
            {error && <MessageBar messageBarType={MessageBarType.error} css={css`margin: 0 1em;`}>{error}</MessageBar>}
            {!roster && !error && <div css={css`margin:1em;color:#605e5c;`}>Loading…</div>}
            {roster && (
                <div css={css`margin: 0 1em;`}>
                    <div css={css`color:#605e5c;font-size:13px;margin-bottom:0.5em;`}>
                        ServiceManager: {roster.serviceManager.name} &nbsp; {roster.serviceManager.ip}:{roster.serviceManager.port}
                    </div>
                    {roster.services.length === 0
                        ? <div css={css`color:#a19f9d;font-style:italic;`}>(no services registered)</div>
                        : roster.services.map(svc => (
                            <div key={svc.name} css={css`margin:0.5em 0 1em;padding:0.5em 0.75em;border-left:3px solid ${svc.allShardReady ? "#107c10" : "#ca5010"};background:#faf9f8;border-radius:2px;`}>
                                <div css={css`font-weight:600;margin-bottom:4px;`}>
                                    {svc.name}
                                    <span css={css`color:#605e5c;font-weight:400;font-size:12px;margin-left:8px;`}>
                                        {svc.allShardReady ? `all ${svc.shardCount} shards ready` : `${svc.shards.length}/${svc.shardCount} ready`}
                                    </span>
                                </div>
                                <DetailsList
                                    items={svc.shards.map(s => ({ ...s, serviceName: svc.name }))}
                                    columns={columns}
                                    selectionMode={SelectionMode.none}
                                    layoutMode={DetailsListLayoutMode.justified}
                                    compact
                                />
                            </div>
                        ))}
                </div>
            )}
        </Stack>
    );
};

export default ServicePage;
