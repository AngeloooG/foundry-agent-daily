import { useEffect, useState } from 'react';
import type { IAgentMetadata } from '../../types/chat';
import {
  dailyTranslations,
  type DailyLang,
  type DailyPage,
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

export function DailyShell({ agentMetadata }: DailyShellProps) {
  const [page, setPage] = useState<DailyPage>('home');
  const [lang, setLang] = useState<DailyLang>('es');
  const [dark, setDark] = useState(true);

  const t = dailyTranslations[lang];

  useEffect(() => {
    document.documentElement.classList.toggle('daily-light', !dark);
  }, [dark]);

  return (
    <div className={`${styles.shell} ${dark ? styles.dark : styles.light}`}>
      <DailyHeader
        page={page}
        setPage={setPage}
        lang={lang}
        setLang={setLang}
        dark={dark}
        setDark={setDark}
        t={t}
      />

      {page === 'home' && <DailyHome t={t} dark={dark} setPage={setPage} />}

      {page === 'consult' && (
        <div className={styles.chatPage}>
          <DailyAgentChat
            agentId={agentMetadata.id}
            agentName={agentMetadata.name || 'Daily'}
            agentDescription={
              agentMetadata.description || 'Agente de conocimiento empresarial de CONSEIN'
            }
            agentLogo={agentMetadata.metadata?.logo}
            starterPrompts={agentMetadata.starterPrompts || undefined}
          />
        </div>
      )}

      {page === 'technology' && <DailyTechnology t={t} />}

      {page === 'faq' && <DailyFAQ t={t} setPage={setPage} />}
    </div>
  );
}