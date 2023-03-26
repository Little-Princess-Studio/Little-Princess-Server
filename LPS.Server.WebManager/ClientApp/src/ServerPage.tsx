import { Stack, DetailsList, IColumn, SelectionMode, DetailsListLayoutMode, CommandBar, ICommandBarItemProps } from "@fluentui/react";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import { useState } from 'react';
import { copyAndSort } from './Utils';

interface IServerInfo {
    key: string;
    serverName: string;
    mailbox: string;
    entityCount: number;
    alive: boolean;
}

const mockServerItems: IServerInfo[] = [
    {
        key: "server0",
        serverName: "server0",
        mailbox: "127.0.0.1;4901;X209kw5LVJ9jlkj94;12001",
        entityCount: 129,
        alive: true,
    },
    {
        key: "server1",
        serverName: "server1",
        mailbox: "127.0.0.1;4901;X209kw5LVJ9jl23JK;12001",
        entityCount: 14,
        alive: false,
    }
];

const ServerPage: React.FunctionComponent = () => {
    const onColumnClick = (ev: React.MouseEvent<HTMLElement>, column: IColumn): void => {
        const { columns, items } = state;
        const newColumns: IColumn[] = columns.slice();
        const currColumn: IColumn = newColumns.filter(currCol => column.key === currCol.key)[0];
        newColumns.forEach((newCol: IColumn) => {
            if (newCol === currColumn) {
                currColumn.isSortedDescending = !currColumn.isSortedDescending;
                currColumn.isSorted = true;
            } else {
                newCol.isSorted = false;
                newCol.isSortedDescending = true;
            }
        });
        const newItems = copyAndSort(items, currColumn.key, currColumn.isSortedDescending);
        console.log(newItems);
        setState({
            columns: newColumns,
            items: newItems,
        });
    };

    const listColumnDefine: IColumn[] = [
        {
            key: "serverName",
            name: "Server Name",
            isSorted: true,
            isSortedDescending: false,
            isRowHeader: true,
            minWidth: 40,
            maxWidth: 100,
            isPadded: true,
            sortAscendingAriaLabel: 'Sorted A to Z',
            sortDescendingAriaLabel: 'Sorted Z to A',
            data: 'string',
            onRender: (item: IServerInfo) => {
                return <div>{item.serverName}</div>
            },
            onColumnClick: onColumnClick
        },
        {
            key: "mailbox",
            name: "MailBox",
            minWidth: 40,
            maxWidth: 250,
            onRender: (item: IServerInfo) => {
                return <div>{item.mailbox}</div>
            }
        },
        {
            key: "entityCount",
            name: "Entity Count",
            minWidth: 40,
            maxWidth: 100,
            onRender: (item: IServerInfo) => {
                return <div>{item.entityCount}</div>
            },
            onColumnClick: onColumnClick
        },
        {
            key: "alive",
            name: "Server State",
            minWidth: 40,
            maxWidth: 100,
            onRender: (item: IServerInfo) => {
                return <div><b>{item.alive ? "Alive" : "Dead"}</b></div>
            }
        }
    ];

    const [state, setState] = useState({
        items: mockServerItems,
        columns: listColumnDefine,
    });

    return <Stack horizontal={false} css={css`margin-top: 50px`}>
        <NavBar index={1} />
        <h2 css={header}>Server List</h2>
        <div css={css`margin: 0 20px 0 20px`}>
            <DetailsList
                columns={state.columns}
                items={state.items}
                selectionMode={SelectionMode.none}
                getKey={(item: IServerInfo) => item.key}
                setKey={"none"}
                isHeaderVisible={true}
                layoutMode={DetailsListLayoutMode.justified}
            />
        </div>
    </Stack>;
}

export default ServerPage;
