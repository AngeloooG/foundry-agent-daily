import React from 'react';
import ReactDOM from 'react-dom/client';
import {
  PublicClientApplication,
  EventType,
  type AuthenticationResult,
} from '@azure/msal-browser';
import { MsalProvider } from '@azure/msal-react';
import { BrowserRouter } from 'react-router-dom';

import App from './App';
import { msalConfig } from './config/authConfig';
import { AppProvider } from './contexts/AppContext';
import { ThemeProvider } from './components/ThemeProvider';
import { initTelemetry } from './services/telemetry';

import './index.css';

initTelemetry();

const msalInstance = new PublicClientApplication(msalConfig);

msalInstance.initialize().then(() => {
  const accounts = msalInstance.getAllAccounts();

  if (accounts.length > 0) {
    msalInstance.setActiveAccount(accounts[0]);
  }

  msalInstance.addEventCallback((event) => {
    if (
      event.eventType === EventType.LOGIN_SUCCESS &&
      event.payload
    ) {
      const payload = event.payload as AuthenticationResult;
      msalInstance.setActiveAccount(payload.account);
    }
  });

  const rootElement = document.getElementById('root');

  if (!rootElement) {
    console.error('Failed to find the root element');
    return;
  }

  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <MsalProvider instance={msalInstance}>
        <BrowserRouter>
          <AppProvider>
            <ThemeProvider>
              <App />
            </ThemeProvider>
          </AppProvider>
        </BrowserRouter>
      </MsalProvider>
    </React.StrictMode>
  );
});