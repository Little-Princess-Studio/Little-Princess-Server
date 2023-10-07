import * as React from 'react';
import { AnimationClassNames, getTheme } from '@fluentui/react/lib/Styling';
import { Stack, Layer, DefaultButton } from '@fluentui/react';
import styled, { css } from 'styled-components';
import { useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';

const theme = getTheme();

const MainMenuButton = styled(DefaultButton)`
    height: 50px;
    background-color: ${theme.palette.themePrimary};
    border: none;
    border-radius: 0;
    color: white;

    :hover {
        background-color: ${theme.palette.blueMid};
        color: white;
    }
`;

const SelectedMainMenuButton = styled(MainMenuButton)`
    background-color: ${theme.palette.blueDark};   
`

const ContentCss = css`
    background-color: ${theme.palette.themePrimary};
    color: ${theme.palette.white};
    line-height: 50px;
    padding: 0 20px;
`;

export type MainMenuItemDesc = {
    title: string,
    onClicked: (index: number) => void;
}

export interface INavBarProp {
    index: number;
}

const NavBar: React.FunctionComponent<INavBarProp> = (props: INavBarProp) => {
    const mainMenuDesc: MainMenuItemDesc[] = [
        { title: "Manager", onClicked: (index) => { navigate("/manager") } },
        { title: "Servers", onClicked: (index) => { navigate("/server") } },
        { title: "Gates", onClicked: (index) => { navigate("/gate") } },
        { title: "Services", onClicked: (index) => { navigate("/service") } },
    ];

    const navigate = useNavigate();

    const onClickedCb = useCallback((index: number, callback: (index: number) => void) => {
        callback(index);
    }, []);

    return <Layer>
        <Stack horizontal className={AnimationClassNames.scaleUpIn100} css={ContentCss}>
            {
                mainMenuDesc.map((item, index) => {
                    const { title, onClicked } = item;
                    if (index === props.index) {
                        return <SelectedMainMenuButton allowDisabledFocus onClick={_ => onClickedCb(index, onClicked)} key={index}>{title}</SelectedMainMenuButton>;
                    }
                    else {
                        return <MainMenuButton allowDisabledFocus onClick={_ => onClickedCb(index, onClicked)} key={index}>{title}</MainMenuButton>;
                    }
                })
            }
        </Stack>
    </Layer>
}

export default NavBar;
