import { Stack, Dropdown, IDropdownOption, Toggle, Slider, DefaultButton, MessageBar, MessageBarType, Spinner } from "@fluentui/react";
import { useCallback, useEffect, useRef, useState } from "react";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import { LogFileEntry, queryLogList, queryLogTail } from "./Network";

const POLL_INTERVAL_MS = 2000;
const DEFAULT_TAIL_LINES = 300;

// Color a single log line based on its NLog severity tag (`[Info]`, `[Warn]` …).
// Plays nice with the `${level}` template configured in the project's nlog.config.
const lineColor = (line: string): string | undefined => {
    if (line.includes("[Error]") || line.includes("[Fatal]") || line.includes("Unhandled Exception")) return "#d13438";
    if (line.includes("[Warn]")) return "#ca5010";
    if (line.includes("[Debug]")) return "#7a7574";
    return undefined;
};

const formatBytes = (n: number): string => {
    if (n < 1024) return `${n} B`;
    if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
    return `${(n / 1024 / 1024).toFixed(1)} MB`;
};

const LogsPage: React.FunctionComponent = () => {
    const [logFiles, setLogFiles] = useState<LogFileEntry[]>([]);
    const [logsDirectory, setLogsDirectory] = useState<string>("");
    const [selected, setSelected] = useState<string | undefined>(undefined);
    const [tailLines, setTailLines] = useState<string[]>([]);
    const [meta, setMeta] = useState<{ totalSize: number; fileName: string } | undefined>(undefined);
    const [lineCount, setLineCount] = useState<number>(DEFAULT_TAIL_LINES);
    const [autoRefresh, setAutoRefresh] = useState<boolean>(true);
    const [autoScroll, setAutoScroll] = useState<boolean>(true);
    const [error, setError] = useState<string | undefined>(undefined);
    const [loading, setLoading] = useState<boolean>(false);

    const scrollRef = useRef<HTMLDivElement | null>(null);

    // Refresh the file list once on mount; users hit "Refresh List" to redo this.
    const refreshFileList = useCallback(async () => {
        try {
            const res = await queryLogList();
            setLogFiles(res.logs);
            setLogsDirectory(res.logsDirectory);
            // If we haven't picked one yet, pick the most recently written file as default.
            if (!selected && res.logs.length > 0) {
                const newest = [...res.logs].sort(
                    (a, b) => new Date(b.lastWriteUtc).getTime() - new Date(a.lastWriteUtc).getTime(),
                )[0];
                setSelected(newest.name);
            }
        } catch (e: any) {
            setError(`list failed: ${e.message ?? e}`);
        }
    }, [selected]);

    const refreshTail = useCallback(async () => {
        if (!selected) return;
        setLoading(true);
        try {
            const res = await queryLogTail(selected, lineCount);
            setTailLines(res.lines);
            setMeta({ totalSize: res.totalSize, fileName: res.fileName });
            setError(undefined);
        } catch (e: any) {
            setError(`tail failed: ${e.message ?? e}`);
        } finally {
            setLoading(false);
        }
    }, [selected, lineCount]);

    useEffect(() => { refreshFileList(); }, []); // eslint-disable-line react-hooks/exhaustive-deps

    // Re-fetch tail when the selected file or line count changes.
    useEffect(() => { refreshTail(); }, [refreshTail]);

    // Polling.
    useEffect(() => {
        if (!autoRefresh || !selected) return;
        const id = window.setInterval(refreshTail, POLL_INTERVAL_MS);
        return () => window.clearInterval(id);
    }, [autoRefresh, selected, refreshTail]);

    // Scroll-to-bottom when content changes and the user wants tail-mode.
    useEffect(() => {
        if (autoScroll && scrollRef.current) {
            scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
        }
    }, [tailLines, autoScroll]);

    const dropdownOptions: IDropdownOption[] = logFiles.map(f => ({
        key: f.name,
        text: `${f.name}  (${formatBytes(f.sizeBytes)})`,
    }));

    return (
        <Stack horizontal={false} css={css`margin-top: 50px;`}>
            <NavBar index={4} />
            <h2 css={header}>Cluster Logs</h2>

            <Stack horizontal tokens={{ childrenGap: 12 }} css={css`margin: 0 1em 1em;`} verticalAlign="end">
                <Dropdown
                    label="Process"
                    selectedKey={selected}
                    options={dropdownOptions}
                    onChange={(_, opt) => opt && setSelected(opt.key as string)}
                    styles={{ root: { minWidth: 260 } }}
                />
                <Stack tokens={{ childrenGap: 4 }} css={css`min-width: 220px;`}>
                    <Slider
                        label={`Tail lines: ${lineCount}`}
                        min={50} max={2000} step={50}
                        value={lineCount}
                        onChange={(v) => setLineCount(v)}
                        showValue={false}
                    />
                </Stack>
                <Toggle label="Auto refresh (2s)" checked={autoRefresh} onChange={(_, c) => setAutoRefresh(!!c)} inlineLabel />
                <Toggle label="Auto scroll" checked={autoScroll} onChange={(_, c) => setAutoScroll(!!c)} inlineLabel />
                <DefaultButton text="Refresh now" iconProps={{ iconName: "refresh" }} onClick={refreshTail} />
                <DefaultButton text="Refresh list" iconProps={{ iconName: "DocumentSet" }} onClick={refreshFileList} />
                {loading && <Spinner label="loading..." labelPosition="right" />}
            </Stack>

            {error && (
                <MessageBar messageBarType={MessageBarType.error} css={css`margin: 0 1em;`}>
                    {error}
                </MessageBar>
            )}

            {meta && (
                <div css={css`margin: 0 1em 0.5em; color: #605e5c; font-size: 12px;`}>
                    {meta.fileName} &middot; {formatBytes(meta.totalSize)} &middot; showing last {tailLines.length} line(s)
                    {logsDirectory && <> &middot; from <code>{logsDirectory}</code></>}
                </div>
            )}

            <div
                ref={scrollRef}
                css={css`
                    margin: 0 1em 1em;
                    padding: 0.75em 1em;
                    background: #1e1e1e;
                    color: #d4d4d4;
                    font-family: Consolas, 'Courier New', monospace;
                    font-size: 12px;
                    line-height: 1.45;
                    height: 65vh;
                    overflow: auto;
                    white-space: pre-wrap;
                    border-radius: 4px;
                `}
            >
                {tailLines.length === 0 && !loading && <div css={css`color: #888;`}>(no content yet)</div>}
                {tailLines.map((line, idx) => (
                    <div key={idx} css={css`color: ${lineColor(line) ?? '#d4d4d4'};`}>
                        {line || '\u00a0'}
                    </div>
                ))}
            </div>
        </Stack>
    );
};

export default LogsPage;
