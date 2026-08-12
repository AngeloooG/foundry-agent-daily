import { useNavigate } from 'react-router-dom';

import type { DailyTranslation } from '../../config/dailyTranslations';
import { DailyLogo } from './DailyLogo';
import NeuralNetwork from './NeuralNetwork';
import styles from './DailyShell.module.css';

interface DailyHomeProps {
    t: DailyTranslation;
    dark: boolean;
}

export function DailyHome({ t, dark }: DailyHomeProps) {
    const navigate = useNavigate();

    const openDaily = () => {
        navigate('/daily');
        scrollToTop();
    };

    const openDailyWithQuestion = (question: string) => {
        const normalizedQuestion = question.trim();

        if (!normalizedQuestion) {
            return;
        }

        navigate('/daily', {
            state: {
                draftQuestion: normalizedQuestion,
            },
        });

        scrollToTop();
    };

    const scrollToHowItWorks = () => {
        document
            .getElementById('how-works')
            ?.scrollIntoView({ behavior: 'smooth' });
    };

    return (
        <main className={styles.page}>
            <section className={styles.hero}>
                <div className={styles.heroContent}>
                    <div className={styles.heroBadge}>
                        <span />
                        {t.hero.tag}
                    </div>

                    <h1 className={styles.heroTitle}>
                        <span>{t.hero.headlineLine1}</span>
                        <strong>{t.hero.headlineLine2}</strong>
                        {t.hero.headlineLine3 && (
                            <span>{t.hero.headlineLine3}</span>
                        )}
                    </h1>

                    <p>{t.hero.subtitle}</p>

                    <div className={styles.heroActions}>
                        <button
                            type="button"
                            className={styles.primaryButton}
                            onClick={openDaily}
                        >
                            {t.hero.cta} →
                        </button>

                        <button
                            type="button"
                            className={styles.secondaryButton}
                            onClick={scrollToHowItWorks}
                        >
                            {t.hero.ctaSecondary}
                        </button>
                    </div>

                    <div className={styles.techChips}>
                        {[
                            'Azure AI Search',
                            'Vector Search',
                            'Semantic Search',
                            'GPT',
                            'Azure AI Foundry',
                            'MCP',
                        ].map((chip) => (
                            <span key={chip}>{chip}</span>
                        ))}
                    </div>
                </div>

                <div className={styles.heroVisual}>
                    <div className={styles.chatPreview}>
                        <div className={styles.previewHeader}>
                            <div className={styles.previewAgent}>
                                <span className={styles.previewLogo}>
                                    <DailyLogo size={14} />
                                </span>

                                <div>
                                    <strong>Daily</strong>
                                    <span>
                                        <i /> {t.home.preview.agentStatus}
                                    </span>
                                </div>
                            </div>

                            <div className={styles.previewDots}>
                                <span />
                                <span />
                                <span />
                            </div>
                        </div>

                        <div className={styles.neuralBox}>
                            <NeuralNetwork dark={dark} />
                        </div>

                        <div className={styles.previewUserBubble}>
                            {t.home.preview.userQuestion}
                        </div>

                        <div className={styles.previewAssistantRow}>
                            <span className={styles.previewMiniLogo}>
                                <DailyLogo size={10} />
                            </span>

                            <div className={styles.previewAssistantColumn}>
                                <div className={styles.previewAssistantBubble}>
                                    {t.home.preview.assistantAnswer}
                                </div>

                                {t.home.preview.projects.map((project) => (
                                    <div key={project.name} className={styles.previewCase}>
                                        <strong>{project.name}</strong>
                                        <span>{project.relevance}</span>
                                    </div>
                                ))}
                            </div>
                        </div>

                        <div className={styles.previewInput}>
                            <span>{t.home.preview.placeholder}</span>
                            <button
                                type="button"
                                onClick={openDaily}
                                aria-label={t.home.preview.openChatLabel}
                            >
                                →
                            </button>
                        </div>
                    </div>
                </div>
            </section>

            <section className={styles.section}>
                <div className={styles.sectionHeaderLeft}>
                    <h2>{t.whatIs.title}</h2>
                    <p>{t.whatIs.subtitle}</p>
                </div>

                <div className={styles.cardGrid}>
                    {t.whatIs.cards.map((card: any, index: number) => (
                        <article key={card.title} className={styles.infoCard}>
                            <div
                                className={`${styles.cardIcon} ${styles[`tone${capitalize(card.tone)}`]
                                    }`}
                            >
                                {index === 0 && <SearchIcon />}
                                {index === 1 && <FolderIcon />}
                                {index === 2 && <UsersIcon />}
                            </div>

                            <h3>{card.title}</h3>
                            <p>{card.desc}</p>
                        </article>
                    ))}
                </div>
            </section>

            <section className={styles.flowSection}>
                <h2>{t.flow.title}</h2>

                <div className={styles.flowCards}>
                    {t.flow.steps.map((step: any, index: number) => (
                        <div key={step.tag} className={styles.flowCardShell}>
                            <article className={styles.flowCard}>
                                <div
                                    className={`${styles.flowIcon} ${index === 0
                                            ? styles.toneGreen
                                            : index === 1
                                                ? styles.toneBlue
                                                : styles.toneCyan
                                        }`}
                                >
                                    {index === 0 && <PenIcon />}
                                    {index === 1 && <CheckIcon />}
                                    {index === 2 && <NetworkIcon />}
                                </div>

                                <span>{step.tag}</span>
                                <h3>{step.label}</h3>
                                <p>{step.desc}</p>
                            </article>

                            {index < t.flow.steps.length - 1 && (
                                <div className={styles.flowArrow}>→</div>
                            )}
                        </div>
                    ))}
                </div>

                <p className={styles.flowTag}>{t.flow.tag}</p>
            </section>

            <section id="how-works" className={styles.sectionBordered}>
                <h2 className={styles.leftTitle}>{t.howWorks.title}</h2>

                <div className={styles.howGrid}>
                    {t.howWorks.steps.map((step: any, index: number) => (
                        <article key={step.n} className={styles.howCard}>
                            <span
                                className={
                                    index === t.howWorks.steps.length - 1
                                        ? styles.stepGreen
                                        : undefined
                                }
                            >
                                {step.n}
                            </span>

                            <h3>{step.title}</h3>
                            <p>{step.desc}</p>

                            {index < t.howWorks.steps.length - 1 && <b>→</b>}
                        </article>
                    ))}
                </div>
            </section>

            <section className={styles.questionsSection}>
                <h2>{t.sampleQuestions.title}</h2>

                <div className={styles.questionCloud}>
                    {t.sampleQuestions.questions.map((question: string) => (
                        <button
                            key={question}
                            type="button"
                            onClick={() => openDailyWithQuestion(question)}
                        >
                            {question}
                        </button>
                    ))}
                </div>
            </section>

            <section className={styles.benefitsSection}>
                <h2>{t.benefits.title}</h2>

                <div className={styles.benefitGrid}>
                    {t.benefits.groups.map((group: any) => (
                        <article key={group.role} className={styles.benefitCard}>
                            <span
                                className={`${styles.roleBadge} ${styles[`tone${capitalize(group.tone)}`]
                                    }`}
                            >
                                {group.role}
                            </span>

                            <ul>
                                {group.items.map((item: string) => (
                                    <li key={item}>
                                        <CheckSmall tone={group.tone} />
                                        <span>{item}</span>
                                    </li>
                                ))}
                            </ul>
                        </article>
                    ))}
                </div>
            </section>

            <section className={styles.finalCta}>
                <div className={styles.finalIcon}>
                    <DailyLogo size={28} />
                </div>

                <h2>{t.home.finalCta.title}</h2>
                <p>{t.home.finalCta.description}</p>

                <button
                    type="button"
                    className={styles.primaryButton}
                    onClick={openDaily}
                >
                    {t.home.finalCta.button} →
                </button>
            </section>
        </main>
    );
}

function scrollToTop() {
    window.scrollTo({
        top: 0,
        behavior: 'instant' as ScrollBehavior,
    });
}

function capitalize(value: string) {
    return value.charAt(0).toUpperCase() + value.slice(1);
}

function SearchIcon() {
    return (
        <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
        >
            <circle cx="11" cy="11" r="7" />
            <path d="M20 20l-3.3-3.3" />
        </svg>
    );
}

function FolderIcon() {
    return (
        <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
        >
            <path d="M3 7h7l2 2h9v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z" />
            <path d="M16 13h5" />
            <path d="M18.5 10.5v5" />
        </svg>
    );
}

function UsersIcon() {
    return (
        <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
        >
            <path d="M17 21v-2a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v2" />
            <circle cx="10" cy="7" r="4" />
            <path d="M21 21v-2a4 4 0 0 0-3-3.87" />
            <path d="M16 3.13a4 4 0 0 1 0 7.75" />
        </svg>
    );
}

function PenIcon() {
    return (
        <svg
            width="22"
            height="22"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
        >
            <path d="M12 20h9" />
            <path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z" />
        </svg>
    );
}

function CheckIcon() {
    return (
        <svg
            width="22"
            height="22"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
        >
            <path d="M20 6 9 17l-5-5" />
        </svg>
    );
}

function NetworkIcon() {
    return (
        <svg
            width="22"
            height="22"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
        >
            <circle cx="12" cy="12" r="2" />
            <circle cx="12" cy="4" r="2" />
            <circle cx="20" cy="12" r="2" />
            <circle cx="12" cy="20" r="2" />
            <circle cx="4" cy="12" r="2" />
            <path d="M12 6v4M14 12h4M12 14v4M10 12H6" />
        </svg>
    );
}

function CheckSmall({ tone }: { tone: string }) {
    const color = tone === 'green' ? '#8cc63f' : '#7cbce3';

    return (
        <svg
            width="13"
            height="13"
            viewBox="0 0 24 24"
            fill="none"
            stroke={color}
            strokeWidth="3"
            aria-hidden="true"
        >
            <path d="M20 6 9 17l-5-5" />
        </svg>
    );
}
