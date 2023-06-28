import { Stack, DetailsList, IColumn, SelectionMode, DetailsListLayoutMode, CommandBar, ICommandBarItemProps, SearchBox, Dropdown, IDropdownOption, Separator, IconButton, Toggle } from "@fluentui/react";
import { css } from "styled-components";
import { header } from "./CommonCss";
import NavBar from "./NavBar";
import { useEffect, useState } from 'react';
import { copyAndSort } from './Utils';
import { mailBoxToString, queryEntities, queryServerBasicInfo, querySingleServerInfo, ServerInfo, ServerMailBox } from "./Network";

interface IServerInfo {
    key: string;
    serverName: string;
    mailbox: ServerMailBox;
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

const commandBarItems: ICommandBarItemProps[] = [
    {
        key: 'refresh',
        text: 'Refresh',
        iconProps: { iconName: 'refresh' }
    },
];

const ServerPage: React.FunctionComponent = () => {
    const onColumnClick = (_ev: React.MouseEvent<HTMLElement>, column: IColumn): void => {
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
        setServerListState({
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
                return <span css={css`margin-left: 10px;`}>{item.serverName}</span>
            },
            onColumnClick: onColumnClick
        },
        {
            key: "mailbox",
            name: "MailBox",
            minWidth: 40,
            maxWidth: 400,
            onRender: (item: IServerInfo) => {
                return <span>{mailBoxToString(item.mailbox)}</span>
            }
        },
        {
            key: "entityCount",
            name: "Entity Count",
            minWidth: 40,
            maxWidth: 100,
            onRender: (item: IServerInfo) => {
                return <span css={css`margin-left: 10px`}>{item.entityCount}</span>
            },
            onColumnClick: onColumnClick
        },
        {
            key: "alive",
            name: "Server State",
            minWidth: 40,
            maxWidth: 100,
            onRender: (item: IServerInfo) => {
                return <span css={css`margin-left: 10px`}><b>{item.alive ? "Alive" : "Dead"}</b></span>
            }
        }
    ];

    const generateServerOptions = (): IDropdownOption[] => serverlistState.items.map((value, _) => {
        return { key: value.serverName, text: value.serverName };
    });

    const searchEntity = () => {
        const mb = serverlistState.items[searchSelection].mailbox;
        const serverId = mb.id;
        const serverHostNum = mb.hostNum;

        queryEntities(serverId, serverHostNum).then((entities) => {
            const searchResult = entities.map((value, index): IEntityInfo => {
                return {
                    key: value.id,
                    id: value.id,
                    mailbox: mailBoxToString(value.mailbox),
                    entityClassName: value.entityClassName,
                    cellEntityId: value.cellEntityId,
                }
            });
            setSearchResultState({
                ...searchResultState,
                searchResult: searchResult,
            });
        }).catch(console.error);
    }

    const generateFarItems = () => {
        const options = generateServerOptions();
        return [
            {
                key: 'search',
                onRender: () => !searchEntityMode ? null : <SearchBox css={css`margin-left: 4px`} placeholder="Search entity by id" className="searchBox" />
            },
            {
                key: 'serverNameSelection',
                onRender: () => <Dropdown
                    options={options}
                    css={css`width: 100px; margin-left: 4px; margin-right: 4px`}
                    dropdownWidth={100}
                    defaultSelectedKey={options.length > 0 ? options[0].key : undefined}
                    onChange={(_event, _option, index) => { setSearchSelection(index!); }}
                />
            },
            {
                key: 'searchBtn',
                onRender: () => <IconButton iconProps={{ iconName: 'search' }} onClick={_ => searchEntity()}></IconButton>
            },
            {
                key: 'toggleMode',
                onRender: () => <Toggle
                    css={css`display: flex; align-items: center`}
                    label="" onText="Show All Entities"
                    offText="Search entity"
                    defaultChecked={!searchEntityMode}
                    onChange={(_, checked) => { setSearchEntityMode(!checked!); }} />
            },
        ];
    };

    const searchResultListColumnDefine: IColumn[] = [
        {
            key: "id",
            name: "id",
            isSorted: false,
            isSortedDescending: false,
            isRowHeader: true,
            minWidth: 40,
            maxWidth: 250,
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
            maxWidth: 400,
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
        items: [] as IServerInfo[],
        columns: serverListColumnDefine,
    });

    const [searchResultState, setSearchResultState] = useState({
        searchResultColumns: searchResultListColumnDefine,
        searchResult: [] as IEntityInfo[],
    })

    const [searchEntityMode, setSearchEntityMode] = useState(false);

    const [searchSelection, setSearchSelection] = useState(0);

    useEffect(() => {
        let isSubscribed = true;
        console.log("start fetch server info");
        const fetchData = async () => {
            const resp = await queryServerBasicInfo();
            const serverInfo = resp.serverMailBoxes.map((item, _idx, _): IServerInfo => {
                return {
                    key: `${item.id}`,
                    serverName: "unknown",
                    mailbox: item,
                    entityCount: -1,
                    alive: false,
                }
            });
            if (isSubscribed) {
                setServerListState(pre => {
                    return { ...pre, items: [...serverInfo], }
                })

                // replace detailed server info to temp server info.
                const serverDetailedInfoMap: Map<string, ServerInfo> = new Map();
                for (let i = 0; i < resp.serverMailBoxes.length; ++i) {
                    const server = resp.serverMailBoxes[i];
                    const serverId = server.id;
                    const hostNum = server.hostNum;
                    const detailedServerInfo = await querySingleServerInfo(serverId, hostNum);
                    serverDetailedInfoMap.set(serverId, detailedServerInfo);
                }

                const serverDetailedInfo = serverInfo.map((item, _idx, _): IServerInfo => {
                    if (serverDetailedInfoMap.has(item.key)) {
                        const detailedServerInfo = serverDetailedInfoMap.get(item.key)!;
                        return {
                            key: detailedServerInfo.mailbox.id,
                            serverName: detailedServerInfo.name,
                            mailbox: detailedServerInfo.mailbox,
                            entityCount: detailedServerInfo.cellCnt + detailedServerInfo.entitiesCnt,
                            alive: true,
                        }
                    }
                    else {
                        return {
                            ...item,
                        }
                    }
                });
                setServerListState(pre => {
                    return {
                        ...pre,
                        items: serverDetailedInfo,
                    }
                });
            }
        }
        fetchData().catch(console.error);

        return () => { isSubscribed = false };
    }, []);

    return <Stack horizontal={false} css={css`margin-top: 50px`}>
        <NavBar index={1} />
        <h2 css={header}>Server List</h2>

        <div>
            <CommandBar items={commandBarItems} farItems={generateFarItems()} ></CommandBar>
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
