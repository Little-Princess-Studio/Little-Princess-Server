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
