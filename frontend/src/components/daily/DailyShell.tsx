import {
  useCallback,
  useEffect,
  useState,
} from 'react';
import {
  Navigate,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from 'react-router-dom';

import type { IAgentMetadata } from '../../types/chat';
import {
  dailyTranslations,
  type DailyLang,
  type DailyTranslation,
} from '../../config/dailyTranslations';
import { DailyAgentChat } from './DailyAgentChat';
import { DailyHeader } from './DailyHeader';
import { DailyHome } from './DailyHome';
import { DailyTechnology } from './DailyTechnology';
import { DailyFAQ } from './DailyFAQ';
import styles from './DailyShell.module.css';

interface DailyShellProps {
  agentMetadata: IAgentMetadata;
}

interface DailyNavigationState {
  draftQuestion?: string;
}

export function DailyShell({
  agentMetadata,
}: DailyShellProps) {
  const [lang, setLang] = useState<DailyLang>('es');
  const [dark, setDark] = useState(true);

  const t = dailyTranslations[lang];

  useEffect(() => {
    document.documentElement.classList.toggle(
      'daily-light',
      !dark
    );

    document.documentElement.lang = lang;

    return () => {
      document.documentElement.classList.remove(
        'daily-light'
      );
    };
  }, [dark, lang]);

  return (
    <div
      className={`${styles.shell} ${dark ? styles.dark : styles.light
        }`}
    >
      <DailyHeader
        lang={lang}
        setLang={setLang}
        dark={dark}
        setDark={setDark}
        t={t}
      />

      <Routes>
        <Route
          path="/"
          element={<Navigate to="/home" replace />}
        />

        <Route
          path="/home"
          element={<DailyHome t={t} dark={dark} />}
        />

        <Route
          path="/daily"
          element={
            <DailyChatRoute
              agentMetadata={agentMetadata}
              t={t}
            />
          }
        />

        <Route
          path="/technology"
          element={<DailyTechnology t={t} />}
        />

        <Route
          path="/faq"
          element={<DailyFAQ t={t} />}
        />

        <Route
          path="*"
          element={<Navigate to="/home" replace />}
        />
      </Routes>
    </div>
  );
}

interface DailyChatRouteProps {
  agentMetadata: IAgentMetadata;
  t: DailyTranslation;
}

function DailyChatRoute({
  agentMetadata,
  t,
}: DailyChatRouteProps) {
  const location = useLocation();
  const navigate = useNavigate();

  const navigationState =
    location.state as DailyNavigationState | null;

  const draftQuestion =
    typeof navigationState?.draftQuestion === 'string'
      ? navigationState.draftQuestion.trim()
      : '';

  const consumeDraft = useCallback(() => {
    if (!draftQuestion) {
      return;
    }

    navigate(location.pathname, {
      replace: true,
      state: null,
    });
  }, [draftQuestion, location.pathname, navigate]);

  return (
    <div className={styles.chatPage}>
      <DailyAgentChat
        t={t}
        agentId={agentMetadata.id}
        agentName={agentMetadata.name || 'Daily'}
        agentDescription={t.chat.defaultAgentDescription}
        agentLogo={agentMetadata.metadata?.logo}
        initialDraft={draftQuestion}
        onInitialDraftConsumed={consumeDraft}
      />
    </div>
  );
}
