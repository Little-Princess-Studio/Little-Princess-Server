import styled, { css } from 'styled-components'
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import ManagerPage from './ManagerPage';
import ServerPage from './ServerPage';
import LogsPage from './LogsPage';
import { initializeIcons } from '@fluentui/react/lib/Icons';

initializeIcons("https://static2.sharepointonline.com/files/fabric/assets/icons/");

const router = createBrowserRouter([
  {
    path: "/",
    element: <ManagerPage></ManagerPage>,
  },
  {
    path: "/manager",
    element: <ManagerPage></ManagerPage>,
  },
  {
    path: "/server",
    element: <ServerPage></ServerPage>
  },
  {
    path: "/logs",
    element: <LogsPage></LogsPage>
  },
]);

function App() {
  return (
    <div css={appContainer}>
      <RouterProvider router={router}/>
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
