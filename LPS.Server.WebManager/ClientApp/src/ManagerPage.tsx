import { Stack } from "@fluentui/react";
import React from "react";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";

const ManagerPage: React.FunctionComponent = () => {
    return <Stack horizontal={false} css={css`margin-top: 50px`}>
        <NavBar index={0} />
        <h2 css={header}>HostManager List</h2>
    </Stack>;
}

export default ManagerPage;
