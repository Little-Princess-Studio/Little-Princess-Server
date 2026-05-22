// -----------------------------------------------------------------------
// Cluster metrics dashboard - polls /metrics-time-series every intervalMs
// and renders three small recharts line charts.
// -----------------------------------------------------------------------
import { Stack, MessageBar, MessageBarType, Toggle } from "@fluentui/react";
import { useCallback, useEffect, useState } from "react";
import { CartesianGrid, Line, LineChart as LineChartRaw, ResponsiveContainer as ResponsiveContainerRaw, Tooltip, XAxis, YAxis } from "recharts";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import { MetricsTimeSeries, TsPoint, queryMetricsTimeSeries } from "./Network";

// Cast away the React-18-vs-recharts-2.x children typing mismatch
// (recharts ships React-element children but @types/react 18 wants ReactNode).
// Using `any`-props is intentional - the raw recharts types reject our
// JSX children at the type level even though they work at runtime.
const ResponsiveContainer = ResponsiveContainerRaw as unknown as React.FC<any>;
const LineChart = LineChartRaw as unknown as React.FC<any>;

const timeFmt = (t: number) => new Date(t).toLocaleTimeString([], { minute: "2-digit", second: "2-digit" });

const MetricChart: React.FunctionComponent<{ title: string; data: TsPoint[]; color: string; yMax?: number; unit?: string }> = ({ title, data, color, yMax, unit }) => (
    <div css={css`margin:0 1em 1.5em;`}>
        <h3 css={css`margin:0 0 0.5em 0;color:#323130;`}>{title}</h3>
        <div css={css`background:#fff;padding:8px 4px;border:1px solid #edebe9;border-radius:3px;`}>
            <ResponsiveContainer width="100%" height={200}>
                <LineChart data={data} margin={{ top: 8, right: 16, left: 0, bottom: 4 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#edebe9" />
                    <XAxis dataKey="t" tickFormatter={(t: number) => timeFmt(t)} tick={{ fontSize: 11 }} minTickGap={40} />
                    <YAxis tick={{ fontSize: 11 }} domain={yMax !== undefined ? [0, yMax] : ["auto", "auto"]} unit={unit} />
                    <Tooltip
                        labelFormatter={(t: any) => `Time: ${timeFmt(+t)}`}
                        formatter={(v: any) => {
                            const n = Number(v);
                            return [unit ? `${n.toFixed(1)}${unit}` : n.toFixed(1), title] as [string, string];
                        }}
                    />
                    <Line type="monotone" dataKey="v" stroke={color} strokeWidth={2} dot={false} isAnimationActive={false} />
                </LineChart>
            </ResponsiveContainer>
        </div>
    </div>
);

const MetricsPage: React.FunctionComponent = () => {
    const [data, setData] = useState<MetricsTimeSeries | undefined>(undefined);
    const [error, setError] = useState<string | undefined>(undefined);
    const [autoRefresh, setAutoRefresh] = useState(true);

    const refresh = useCallback(async () => {
        try { setData(await queryMetricsTimeSeries()); setError(undefined); }
        catch (e: any) { setError(`refresh failed: ${e.message ?? e}`); }
    }, []);

    useEffect(() => {
        refresh();
        if (!autoRefresh) return;
        const id = window.setInterval(refresh, 5000);
        return () => window.clearInterval(id);
    }, [refresh, autoRefresh]);

    return (
        <Stack horizontal={false} css={css`margin-top:50px;`}>
            <NavBar index={5} />
            <h2 css={header}>Metrics</h2>
            <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 12 }} css={css`margin:0 1em 1em;`}>
                <Toggle label="Auto refresh (5s)" checked={autoRefresh} onChange={(_, c) => setAutoRefresh(!!c)} inlineLabel />
                {data && <span css={css`color:#605e5c;font-size:12px;`}>tick={data.intervalMs}ms · capacity={data.capacity}</span>}
            </Stack>
            {error && <MessageBar messageBarType={MessageBarType.error} css={css`margin:0 1em;`}>{error}</MessageBar>}
            {!data && !error && <div css={css`margin:1em;color:#605e5c;`}>Loading…</div>}
            {data && (
                <>
                    <MetricChart title="Ping Success Rate" data={data.series.pingSuccessRate} color="#0078d4" yMax={100} unit="%" />
                    <MetricChart title="Alive Gates" data={data.series.aliveGates} color="#107c10" />
                    <MetricChart title="Alive Servers" data={data.series.aliveServers} color="#5c2d91" />
                    <MetricChart title="Alive Services" data={data.series.aliveServices} color="#ca5010" />
                </>
            )}
        </Stack>
    );
};

export default MetricsPage;
