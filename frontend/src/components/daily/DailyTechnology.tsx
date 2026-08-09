import styles from './DailyShell.module.css';

interface DailyTechnologyProps {
    t: any;
}

const flowSteps = [
    {
        n: '01',
        label: 'KIARA',
        title: 'Kiara captura',
        desc: 'Relatos, soluciones, aprendizajes y métricas de proyectos.',
        tone: 'green',
    },
    {
        n: '02',
        label: 'PIPELINE',
        title: 'Revisión y publicación',
        desc: 'Los documentos pasan de borrador a publicado tras validación.',
        tone: 'blue',
    },
    {
        n: '03',
        label: 'PIPELINE',
        title: 'Power Automate procesa',
        desc: 'Lleva la información validada a almacenamiento estructurado.',
        tone: 'blue',
    },
    {
        n: '04',
        label: 'PIPELINE',
        title: 'Azure SQL organiza',
        desc: 'Vistas relacionan especialistas, proyectos, tecnologías y aprendizajes.',
        tone: 'blue',
    },
    {
        n: '05',
        label: 'PIPELINE',
        title: 'Azure AI Search indexa',
        desc: 'Indexadores, chunking, embeddings y búsqueda semántica/vectorial.',
        tone: 'blue',
    },
    {
        n: '06',
        label: 'DAILY',
        title: 'Daily consulta',
        desc: 'El usuario pregunta en lenguaje natural a través de la interfaz.',
        tone: 'cyan',
    },
    {
        n: '07',
        label: 'DAILY',
        title: 'Daily responde',
        desc: 'Casos, especialistas, tecnologías, lecciones y recomendaciones.',
        tone: 'cyan',
    },
];

const techs = [
    {
        name: 'Azure AI Search',
        desc: 'Recuperación inteligente de conocimiento indexado.',
        tone: 'blue',
    },
    {
        name: 'Azure SQL',
        desc: 'Almacenamiento estructurado de vistas y relaciones.',
        tone: 'blue',
    },
    {
        name: 'Embeddings',
        desc: 'Representación semántica para encontrar similitudes.',
        tone: 'green',
    },
    {
        name: 'Chunking',
        desc: 'División del contenido para mejorar precisión de consulta.',
        tone: 'green',
    },
    {
        name: 'Semantic Search',
        desc: 'Búsqueda basada en significado, no solo en palabras.',
        tone: 'cyan',
    },
    {
        name: 'Vector Search',
        desc: 'Recuperación de documentos por similitud vectorial.',
        tone: 'cyan',
    },
    {
        name: 'Hybrid Search',
        desc: 'Combina búsqueda clásica y vectorial para mayor precisión.',
        tone: 'cyan',
    },
    {
        name: 'Power Automate',
        desc: 'Automatización de publicación y procesamiento documental.',
        tone: 'deep',
    },
    {
        name: 'SharePoint',
        desc: 'Origen documental y almacenamiento de casos formalizados.',
        tone: 'blue',
    },
    {
        name: 'Azure AI Foundry',
        desc: 'Plataforma de orquestación de agentes e IA empresarial.',
        tone: 'blue',
    },
    {
        name: 'GPT / Azure OpenAI',
        desc: 'Modelo de lenguaje para generación de respuestas.',
        tone: 'blue',
    },
    {
        name: 'Copilot Studio',
        desc: 'Orquestación de flujos conversacionales y agentes.',
        tone: 'blue',
    },
    {
        name: 'MCP',
        desc: 'Protocolo de contexto para agentes interconectados.',
        tone: 'blue',
    },
    {
        name: 'Microsoft 365',
        desc: 'Ecosistema de productividad e integración documental.',
        tone: 'blue',
    },
];

export function DailyTechnology({ t }: DailyTechnologyProps) {
    return (
        <main className={styles.techPage}>
            <section className={styles.techHero}>
                <div className={styles.heroBadge}>ARQUITECTURA</div>
                <h1>{t.tech.title}</h1>
                <p>{t.tech.subtitle}</p>
            </section>

            <section className={styles.techFlowSection}>
                <h2>{t.tech.flowTitle}</h2>

                <div className={styles.techTimeline}>
                    {flowSteps.map((step) => (
                        <article key={step.n} className={styles.techTimelineItem}>
                            <div className={`${styles.techStepNumber} ${styles[`tone${capitalize(step.tone)}`]}`}>
                                {step.n}
                            </div>

                            <div className={styles.techStepCard}>
                                <span className={`${styles.techLabel} ${styles[`tone${capitalize(step.tone)}`]}`}>
                                    {step.label}
                                </span>

                                <h3>{step.title}</h3>
                                <p>{step.desc}</p>
                            </div>
                        </article>
                    ))}
                </div>
            </section>

            <section className={styles.techStackSection}>
                <h2>{t.tech.techTitle}</h2>

                <div className={styles.techStackGrid}>
                    {techs.map((tech) => (
                        <article key={tech.name} className={styles.techStackCard}>
                            <span className={`${styles.techLabel} ${styles[`tone${capitalize(tech.tone)}`]}`}>
                                {tech.name}
                            </span>

                            <p>{tech.desc}</p>
                        </article>
                    ))}
                </div>

                <div className={styles.techSummary}>
                    {t.tech.summary.map((item: any) => (
                        <article key={item.label}>
                            <h3 className={styles[`summary${capitalize(item.tone)}`]}>{item.label}</h3>
                            <p>{item.desc}</p>
                        </article>
                    ))}
                </div>
            </section>
        </main>
    );
}

function capitalize(value: string) {
    return value.charAt(0).toUpperCase() + value.slice(1);
}