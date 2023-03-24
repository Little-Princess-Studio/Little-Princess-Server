import React from 'react';
import styled, { css } from 'styled-components'
import './App.css';

function App() {
  return (
    <div css={appContainer}>
      <h1 css={appHeader}>
        Little Princess Server Web Manager
      </h1>
    </div>
  );
}

const appContainer = css`
  display: flex;
  flex-direction: column;
  justify-content: center;
  font-size: 20px;
`;

const appHeader = css`
  display: flex;
  justify-self: center;
`;

export default App;
