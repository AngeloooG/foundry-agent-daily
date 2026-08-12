import type { DailyTranslation } from '../../config/dailyTranslations';
import styles from './DailyShell.module.css';

interface DailyTechnologyProps {
  t: DailyTranslation;
}

export function DailyTechnology({ t }: DailyTechnologyProps) {
  return (
    <main className={styles.techPage}>
      <section className={styles.techHero}>
        <div className={styles.heroBadge}>{t.tech.architectureLabel}</div>
        <h1>{t.tech.title}</h1>
        <p>{t.tech.subtitle}</p>
      </section>

      <section className={styles.techFlowSection}>
        <h2>{t.tech.flowTitle}</h2>

        <div className={styles.techTimeline}>
          {t.tech.flowSteps.map((step) => (
            <article key={step.n} className={styles.techTimelineItem}>
              <div
                className={`${styles.techStepNumber} ${styles[`tone${capitalize(step.tone)}`]
                  }`}
              >
                {step.n}
              </div>

              <div className={styles.techStepCard}>
                <span
                  className={`${styles.techLabel} ${styles[`tone${capitalize(step.tone)}`]
                    }`}
                >
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
          {t.tech.stack.map((technology) => (
            <article key={technology.name} className={styles.techStackCard}>
              <span
                className={`${styles.techLabel} ${styles[`tone${capitalize(technology.tone)}`]
                  }`}
              >
                {technology.name}
              </span>

              <p>{technology.desc}</p>
            </article>
          ))}
        </div>

        <div className={styles.techSummary}>
          {t.tech.summary.map((item) => (
            <article key={item.label}>
              <h3 className={styles[`summary${capitalize(item.tone)}`]}>
                {item.label}
              </h3>
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
