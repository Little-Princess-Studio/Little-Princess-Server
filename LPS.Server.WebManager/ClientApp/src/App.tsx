import styled, { css } from 'styled-components'
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import HostManagerPage from './HostManagerPage';
import ServerPage from './ServerPage';
import { initializeIcons } from '@fluentui/react/lib/Icons';

initializeIcons("https://static2.sharepointonline.com/files/fabric/assets/icons/");

const router = createBrowserRouter([
  {
    path: "/",
    element: <HostManagerPage></HostManagerPage>,
  },
  {
    path: "/hostmanager",
    element: <HostManagerPage></HostManagerPage>,
  },
  {
    path: "/server",
    element: <ServerPage></ServerPage>
  },
]);

function App() {
  return (
    <div css={appContainer}>
      <RouterProvider router={router}/>
      
      {/*  <SubItem>*/}
      {/*    <h2 css={appSubHeader}>*/}
      {/*      Server List*/}
      {/*    </h2>*/}
      {/*    <StyledTable>*/}
      {/*      <tr>*/}
      {/*        <StyledTh>Server Name</StyledTh>*/}
      {/*        <StyledTh>Server MailBox</StyledTh>*/}
      {/*        <StyledTh>Entity Count</StyledTh>*/}
      {/*        <StyledTh>Server Status</StyledTh>*/}
      {/*        <StyledTh>Operation</StyledTh>*/}
      {/*      </tr>*/}
      {/*      <tr>*/}
      {/*        <StyledTd>server0</StyledTd>*/}
      {/*        <StyledTd>Xb3vkdh5JN;192.168.0.1;10011;10001</StyledTd>*/}
      {/*        <StyledTd>2000</StyledTd>*/}
      {/*        <StyledTd>Alive</StyledTd>*/}
      {/*        <StyledTd>Refresh</StyledTd>*/}
      {/*      </tr>*/}
      {/*    </StyledTable>*/}
      {/*  </SubItem>*/}
      
      {/*  <SubItem>*/}
      {/*    <h2 css={appSubHeader}>Gate List</h2>*/}
      {/*  </SubItem>*/}
      </div>
  );
}

const appContainer = css`
  display: flex;
  flex-direction: column;
  justify-content: center;
  font-size: 20px;
`;

const StyledTable = styled.table`
  margin: 10px;
`;

const StyledTh = styled.th`
  text-align: center;
  min-width: 250px;
`

const StyledTd = styled.td`
  text-align: center;
`

export default App;
