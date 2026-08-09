import { useState } from 'react';
import type { DailyPage } from '../../config/dailyTranslations';
import styles from './DailyShell.module.css';
import { DailyLogo } from './DailyLogo';

interface DailyFAQProps {
    t: any;
    setPage: (page: DailyPage) => void;
}

export function DailyFAQ({ t, setPage }: DailyFAQProps) {
    const [open, setOpen] = useState<number | null>(0);

    return (
        <main className={styles.page}>
            <section className={styles.section}>
                <div className={styles.sectionHeader}>
                    <div className={styles.kicker}>FAQ</div>
                    <h1>{t.faq.title}</h1>
                    <p>{t.faq.subtitle}</p>
                </div>

                <div className={styles.faqList}>
                    {t.faq.items.map((item: any, index: number) => {
                        const isOpen = open === index;

                        return (
                            <article key={item.q} className={styles.faqItem}>
                                <button type="button" onClick={() => setOpen(isOpen ? null : index)}>
                                    <span>{item.q}</span>
                                    <strong>{isOpen ? '−' : '+'}</strong>
                                </button>

                                {isOpen && <p>{item.a}</p>}
                            </article>
                        );
                    })}
                </div>
            </section>

            <section className={styles.faqCta}>
                <div className={styles.finalIcon}>
                    <DailyLogo size={28} />
                </div>

                <h2>¿Listo para probar Daily?</h2>
                <p>Consulta experiencias y proyectos de CONSEIN en lenguaje natural.</p>

                <button type="button" className={styles.primaryButton} onClick={() => setPage('consult')}>
                    Consultar Daily →
                </button>
            </section>
        </main>
    );
}