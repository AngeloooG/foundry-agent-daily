import { useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { DailyTranslation } from '../../config/dailyTranslations';
import { DailyLogo } from './DailyLogo';
import styles from './DailyShell.module.css';

interface DailyFAQProps {
    t: DailyTranslation;
}

export function DailyFAQ({ t }: DailyFAQProps) {
    const [open, setOpen] = useState<number | null>(0);
    const navigate = useNavigate();

    const openDaily = () => {
        navigate('/daily');
        window.scrollTo({ top: 0, behavior: 'instant' as ScrollBehavior });
    };

    return (
        <main className={styles.page}>
            <section className={styles.section}>
                <div className={styles.sectionHeader}>
                    <div className={styles.kicker}>FAQ</div>
                    <h1>{t.faq.title}</h1>
                    <p>{t.faq.subtitle}</p>
                </div>

                <div className={styles.faqList}>
                    {t.faq.items.map((item, index) => {
                        const isOpen = open === index;

                        return (
                            <article key={item.q} className={styles.faqItem}>
                                <button
                                    type="button"
                                    onClick={() => setOpen(isOpen ? null : index)}
                                    aria-expanded={isOpen}
                                >
                                    <span>{item.q}</span>
                                    <strong aria-hidden="true">{isOpen ? '−' : '+'}</strong>
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

                <h2>{t.faq.cta.title}</h2>
                <p>{t.faq.cta.description}</p>

                <button
                    type="button"
                    className={styles.primaryButton}
                    onClick={openDaily}
                >
                    {t.faq.cta.button} →
                </button>
            </section>
        </main>
    );
}
