// -----------------------------------------------------------------------
// Single service-shard detail page. Routed as /service/:serviceName/:shard.
// -----------------------------------------------------------------------
import { Stack, MessageBar, MessageBarType, DefaultButton, IColumn, DetailsList, SelectionMode, DetailsListLayoutMode } from "@fluentui/react";
import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { css } from "styled-components";
import NavBar from "./NavBar";
import { ServiceShardDetailedInfo, ServiceShardRpcMethod, queryServiceShardDetailedInfo } from "./Network";

const Stat: React.FunctionComponent<{ label: string; value: React.ReactNode }> = ({ label, value }) => (
    <div css={css`background:#f3f2f1;padding:0.5em 0.9em;border-radius:4px;min-width:140px;`}>
        <div css={css`color:#605e5c;font-size:11px;text-transform:uppercase;letter-spacing:0.5px;`}>{label}</div>
        <div css={css`font-size:18px;font-weight:600;margin-top:2px;`}>{value}</div>
    </div>
);

const rpcColumns: IColumn[] = [
    { key: "name", name: "Method", minWidth: 200, maxWidth: 280,
      onRender: (m: ServiceShardRpcMethod) => <span css={css`font-family:Consolas,monospace;font-weight:600;`}>{m.name}</span> },
    { key: "auth", name: "Authority", minWidth: 100,
      onRender: (m: ServiceShardRpcMethod) => <span css={css`color:#605e5c;`}>{m.authority}</span> },
    { key: "ret", name: "Return", minWidth: 100,
      onRender: (m: ServiceShardRpcMethod) => <span>{m.returnType}</span> },
    { key: "params", name: "Parameters", minWidth: 280,
      onRender: (m: ServiceShardRpcMethod) => (
          <span css={css`font-family:Consolas,monospace;font-size:12px;`}>
              {m.parameters.length === 0 ? "()" : m.parameters.map(p => `${p.name}: ${p.type}`).join(", ")}
          </span>
      ) },
];

const ServiceShardDetailPage: React.FunctionComponent = () => {
    const navigate = useNavigate();
    const params = useParams();
    const serviceName = decodeURIComponent(params.serviceName ?? "");
    const shard = parseInt(params.shard ?? "0", 10);

    const [info, setInfo] = useState<ServiceShardDetailedInfo | undefined>(undefined);
    const [error, setError] = useState<string | undefined>(undefined);

    const refresh = useCallback(async () => {
        try {
            setInfo(await queryServiceShardDetailedInfo(serviceName, shard));
            setError(undefined);
        } catch (e: any) {
            setError(`refresh failed: ${e.message ?? e}`);
        }
    }, [serviceName, shard]);

    useEffect(() => {
        refresh();
        const id = window.setInterval(refresh, 2000);
        return () => window.clearInterval(id);
    }, [refresh]);

    return (
        <Stack horizontal={false} css={css`margin-top: 50px;`}>
            <NavBar index={3} />
            <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 12 }} css={css`margin: 1em;`}>
                <DefaultButton iconProps={{ iconName: "Back" }} text="Back to Services" onClick={() => navigate("/service")} />
                <h2 css={css`margin: 0;`}>{serviceName} &nbsp;
                    <span css={css`color:#605e5c;font-size:16px;`}>shard #{shard}</span>
                </h2>
            </Stack>
            {error && <MessageBar messageBarType={MessageBarType.error} css={css`margin: 0 1em;`}>{error}</MessageBar>}
            {!info && !error && <div css={css`margin:1em;color:#605e5c;`}>Loading…</div>}
            {info && (
                <>
                    <div css={css`margin: 0 1em 1em;padding:0.75em 1em;background:#f3f2f1;border-left:4px solid #0078d4;border-radius:2px;`}>
                        <div css={css`font-weight:600;font-size:16px;`}>
                            {info.serviceClass} &nbsp; on &nbsp;
                            {info.hostMailbox.name} ({info.hostMailbox.ip}:{info.hostMailbox.port}#{info.hostMailbox.hostNum})
                        </div>
                        <div css={css`color:#605e5c;font-size:13px;margin-top:4px;font-family:Consolas,monospace;`}>
                            shardMailbox id: {info.shardMailbox.id}
                        </div>
                    </div>

                    <Stack horizontal wrap tokens={{ childrenGap: 12 }} css={css`margin: 0 1em 1em;`}>
                        <Stat label="Shard" value={`#${info.shard}`} />
                        <Stat label="Type Id" value={info.typeId} />
                        <Stat label="RPC Methods" value={info.rpcMethods.length} />
                        <Stat label="Co-located Shards" value={info.coLocatedShards.length} />
                    </Stack>

                    <h3 css={css`margin: 0.5em 1em;`}>RPC Methods</h3>
                    <div css={css`margin: 0 1em 1em;`}>
                        {info.rpcMethods.length === 0
                            ? <span css={css`color:#a19f9d;font-style:italic;`}>(no [RpcMethod] declared)</span>
                            : <DetailsList items={info.rpcMethods} columns={rpcColumns}
                                selectionMode={SelectionMode.none} layoutMode={DetailsListLayoutMode.justified} compact />}
                    </div>

                    <h3 css={css`margin: 0.5em 1em;`}>Other Shards on this Host</h3>
                    <div css={css`margin: 0 1em 1em;`}>
                        {info.coLocatedShards.length === 0
                            ? <span css={css`color:#a19f9d;font-style:italic;`}>(alone)</span>
                            : <ul css={css`font-family:Consolas,monospace;font-size:13px;margin:0;padding-left:1.5em;`}>
                                {info.coLocatedShards.map((s, i) => (
                                    <li key={i}>{s.serviceName} #{s.shard} &nbsp; <span css={css`color:#605e5c;`}>{s.shardId}</span></li>
                                ))}
                            </ul>}
                    </div>
                </>
            )}
        </Stack>
    );
};

export default ServiceShardDetailPage;
