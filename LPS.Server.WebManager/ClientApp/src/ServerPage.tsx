import { Stack, DetailsList, IColumn, SelectionMode, DetailsListLayoutMode, CommandBar, ICommandBarItemProps, SearchBox, Dropdown, IDropdownOption, Separator, IconButton } from "@fluentui/react";
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

interface IEntityInfo {
    key: string;
    id: string;
    mailbox: string;
    entityClassName: string;
    cellEntityId: string;
}

const mockEntities: IEntityInfo[] = [
    {
        key: '1',
        id: '1',
        mailbox: '1;127.0.0.1;27001;10010',
        entityClassName: 'CellEntity',
        cellEntityId: '',
    },
    {
        key: '2',
        id: '2',
        mailbox: '2;127.0.0.127001;10010',
        entityClassName: 'ServerEntity',
        cellEntityId: '1',
    },
    {
        key: '3',
        id: '3',
        mailbox: '3;127.0.0.1;27001;10010',
        entityClassName: 'ServerEntity',
        cellEntityId: '1',
    },
];

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

const commandBarItems: ICommandBarItemProps[] = [
    {
        key: 'refresh',
        text: 'Refresh',
        iconProps: { iconName: 'refresh' }
    },
];

const ServerPage: React.FunctionComponent = () => {
    const onColumnClick = (ev: React.MouseEvent<HTMLElement>, column: IColumn): void => {
        const { columns, items } = serverlistState;
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
        setServerListState({
            ...serverlistState,
            columns: newColumns,
            items: newItems,
        });
    };

    const serverListColumnDefine: IColumn[] = [
        {
            key: "serverName",
            name: "Server Name",
            isSorted: false,
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

    const serverOptions: IDropdownOption[] = mockServerItems.map((value, index, _) => {
        return { key: value.serverName, text: value.serverName };
    });

    const searchEntity = () => {
        setSearchResultState({
            ...searchResultState,
            searchResult: mockEntities,
        });
    }

    const farItems = [
        {
            key: 'search',
            onRender: () => <SearchBox placeholder="Search entity by id" className="searchBox" />
        },
        {
            key: 'serverNameSelection',
            onRender: () => <Dropdown
                options={serverOptions}
                css={css`width: 100px; margin-left: 4px; margin-right: 4px`}
                dropdownWidth={100}
                defaultSelectedKey={serverOptions[0].key} />
        },
        {
            key: 'searchBtn',
            onRender: () => <IconButton iconProps={{ iconName: 'search' }} onClick={_ => searchEntity()}></IconButton>
        }
    ];

    const searchResultListColumnDefine: IColumn[] = [
        {
            key: "id",
            name: "id",
            isSorted: false,
            isSortedDescending: false,
            isRowHeader: true,
            minWidth: 40,
            maxWidth: 100,
            isPadded: true,
            sortAscendingAriaLabel: 'Sorted A to Z',
            sortDescendingAriaLabel: 'Sorted Z to A',
            data: 'string',
            onRender: (item: IEntityInfo) => {
                return <div>{item.id}</div>
            },
        },
        {
            key: "mailbox",
            name: "MailBox",
            minWidth: 40,
            maxWidth: 250,
            onRender: (item: IEntityInfo) => {
                return <div>{item.mailbox}</div>
            }
        },
        {
            key: "entityClassName",
            name: "Entity Class Name",
            minWidth: 40,
            maxWidth: 250,
            onRender: (item: IEntityInfo) => {
                return <div>{item.entityClassName}</div>
            }
        },
        {
            key: "cellEntityId",
            name: "Cell Entity Id",
            minWidth: 40,
            maxWidth: 250,
            onRender: (item: IEntityInfo) => {
                return <div>{item.cellEntityId}</div>
            }
        }
    ];

    const [serverlistState, setServerListState] = useState({
        items: mockServerItems,
        columns: serverListColumnDefine,
    });

    const [searchResultState, setSearchResultState] = useState({
        searchResultColumns: searchResultListColumnDefine,
        searchResult: [] as IEntityInfo[],
    })

    return <Stack horizontal={false} css={css`margin-top: 50px`}>
        <NavBar index={1} />
        <h2 css={header}>Server List</h2>

        <div>
            <CommandBar items={commandBarItems} farItems={farItems} ></CommandBar>
        </div>

        <div css={ListMargin}>
            <DetailsList
                columns={serverlistState.columns}
                items={serverlistState.items}
                selectionMode={SelectionMode.none}
                getKey={(item: IServerInfo) => item.key}
                setKey={"none"}
                isHeaderVisible={true}
                layoutMode={DetailsListLayoutMode.justified}
            />
        </div>

        {searchResultState.searchResult.length > 0 ?
            <div>
                <div css={css`margin: 20px 0 0 20px`}>
                    <Separator alignContent="start"><div css={css`font-size: 20px;`}>Search Result</div></Separator>
                </div>
                <div css={ListMargin}>
                    <DetailsList
                        columns={searchResultState.searchResultColumns}
                        items={searchResultState.searchResult}
                        selectionMode={SelectionMode.none}
                        getKey={(item: IEntityInfo) => item.key}
                        setKey={"none"}
                        isHeaderVisible={true}
                        layoutMode={DetailsListLayoutMode.justified}
                    />
                </div>
            </div>
            : null
        }
    </Stack>;
}

const ListMargin = css`
    margin: 0 20px 0 20px
`

export default ServerPage;
