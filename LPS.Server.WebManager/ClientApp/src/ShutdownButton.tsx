// -----------------------------------------------------------------------
// ShutdownButton - reusable "Stop instance" button with confirm dialog +
// optional timeout override. Backs every per-instance stop control on the
// detail pages (Gate / Server / Service / ServiceManager).
//
// Visual states:
//   idle           -> red "Stop" button
//   confirming     -> dialog with timeout input + Cancel / Confirm
//   pending        -> spinner + "Shutting down..."; button disabled
//   succeeded      -> green inline message for ~3s, then back to idle
//   failed         -> red inline message until cleared
//
// Once a shutdown is accepted the target process drains then Exit(0)s, so
// the parent page's poller will start failing - that's the natural visual
// signal that the instance went away.
// -----------------------------------------------------------------------
import {
    DefaultButton,
    Dialog,
    DialogFooter,
    DialogType,
    MessageBar,
    MessageBarType,
    PrimaryButton,
    Spinner,
    SpinnerSize,
    Stack,
    TextField,
} from "@fluentui/react";
import { useCallback, useState } from "react";
import { css } from "styled-components";
import { shutdownInstance } from "./Network";

export type ShutdownButtonProps = {
    /** Display name (used in confirm dialog). */
    instanceLabel: string;
    /** Cluster role - matches HostManager's InstanceType enum. */
    instanceType: "Gate" | "Server" | "ServiceManager" | "Service";
    /** MailBox id of the target. */
    instanceId: string;
    /** Optional: invoked after a successful shutdown (e.g. to navigate back). */
    onShutdown?: () => void;
};

const ShutdownButton: React.FunctionComponent<ShutdownButtonProps> = ({
    instanceLabel,
    instanceType,
    instanceId,
    onShutdown,
}) => {
    const [showConfirm, setShowConfirm] = useState(false);
    const [pending, setPending] = useState(false);
    const [error, setError] = useState<string | undefined>(undefined);
    const [success, setSuccess] = useState<string | undefined>(undefined);
    // 0 = use receiver default (10s on the cluster side).
    const [timeoutMs, setTimeoutMs] = useState<string>("0");

    const closeDialog = useCallback(() => {
        if (!pending) {
            setShowConfirm(false);
        }
    }, [pending]);

    const doShutdown = useCallback(async () => {
        setPending(true);
        setError(undefined);
        setSuccess(undefined);
        try {
            const result = await shutdownInstance(
                instanceType,
                instanceId,
                parseInt(timeoutMs, 10) || 0,
            );
            if (result.accepted) {
                setSuccess(`Accepted via ${result.transport ?? "?"} transport.`);
                setShowConfirm(false);
                window.setTimeout(() => setSuccess(undefined), 5000);
                onShutdown?.();
            } else {
                setError(result.reason ?? "Shutdown rejected by cluster.");
            }
        } catch (e: any) {
            setError(`Shutdown failed: ${e.message ?? e}`);
        } finally {
            setPending(false);
        }
    }, [instanceType, instanceId, timeoutMs, onShutdown]);

    return (
        <>
            <Stack horizontal tokens={{ childrenGap: 8 }} verticalAlign="center">
                <DefaultButton
                    iconProps={{ iconName: "PowerButton" }}
                    text="Stop"
                    onClick={() => setShowConfirm(true)}
                    styles={{
                        root: { background: "#d83b01", borderColor: "#a4262c", color: "white" },
                        rootHovered: { background: "#a4262c", borderColor: "#a4262c", color: "white" },
                        rootPressed: { background: "#751c20", borderColor: "#a4262c", color: "white" },
                    }}
                />
                {success && (
                    <MessageBar messageBarType={MessageBarType.success} isMultiline={false}
                        css={css`max-width: 320px;`}>
                        {success}
                    </MessageBar>
                )}
                {error && (
                    <MessageBar messageBarType={MessageBarType.error} isMultiline={false}
                        onDismiss={() => setError(undefined)} css={css`max-width: 380px;`}>
                        {error}
                    </MessageBar>
                )}
            </Stack>
            <Dialog
                hidden={!showConfirm}
                onDismiss={closeDialog}
                dialogContentProps={{
                    type: DialogType.normal,
                    title: `Shut down ${instanceType}?`,
                    subText:
                        `This will gracefully drain ${instanceLabel} and exit its process. ` +
                        `StartupManager will NOT auto-restart it (clean exit code 0).`,
                }}
                modalProps={{ isBlocking: true }}
            >
                <TextField
                    label="Drain timeout (ms)"
                    description="0 = use receiver default (10000ms). After this budget the watchdog forces Exit(0)."
                    value={timeoutMs}
                    onChange={(_, v) => setTimeoutMs(v ?? "0")}
                    disabled={pending}
                />
                <DialogFooter>
                    {pending && <Spinner size={SpinnerSize.small} label="Shutting down..." />}
                    <DefaultButton text="Cancel" onClick={closeDialog} disabled={pending} />
                    <PrimaryButton
                        text="Confirm shutdown"
                        onClick={doShutdown}
                        disabled={pending}
                        styles={{
                            root: { background: "#d83b01", borderColor: "#a4262c" },
                            rootHovered: { background: "#a4262c", borderColor: "#a4262c" },
                        }}
                    />
                </DialogFooter>
            </Dialog>
        </>
    );
};

export default ShutdownButton;
