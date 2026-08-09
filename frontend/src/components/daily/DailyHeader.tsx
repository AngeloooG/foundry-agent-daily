import { useState } from 'react';
import type { DailyLang, DailyPage } from '../../config/dailyTranslations';
import { DailyLogo } from './DailyLogo';
import styles from './DailyShell.module.css';

interface DailyHeaderProps {
  page: DailyPage;
  setPage: (page: DailyPage) => void;
  lang: DailyLang;
  setLang: (lang: DailyLang) => void;
  dark: boolean;
  setDark: (value: boolean) => void;
  t: any;
}

export function DailyHeader({
  page,
  setPage,
  lang,
  setLang,
  dark,
  setDark,
  t,
}: DailyHeaderProps) {
  const [menuOpen, setMenuOpen] = useState(false);

  const navItems: Array<{ key: DailyPage; label: string }> = [
    { key: 'home', label: t.nav.home },
    { key: 'consult', label: t.nav.consult },
    { key: 'technology', label: t.nav.technology },
    { key: 'faq', label: t.nav.faq },
  ];

  const go = (nextPage: DailyPage) => {
    setPage(nextPage);
    setMenuOpen(false);
    window.scrollTo({ top: 0, behavior: 'instant' as ScrollBehavior });
  };

  return (
    <header className={styles.header}>
      <button type="button" className={styles.brandButton} onClick={() => go('home')}>
        <span className={styles.headerLogo}>
          <DailyLogo size={18} />
        </span>

        <span className={styles.brandText}>
          <strong>CONSEIN</strong>
          <span>Daily</span>
        </span>
      </button>

      <nav className={styles.desktopNav}>
        {navItems.map((item) => (
          <button
            key={item.key}
            type="button"
            className={`${styles.navLink} ${page === item.key ? styles.navLinkActive : ''}`}
            onClick={() => go(item.key)}
          >
            {item.label}
          </button>
        ))}
      </nav>

      <div className={styles.headerActions}>
        <button
          type="button"
          className={styles.smallControl}
          onClick={() => setLang(lang === 'es' ? 'en' : 'es')}
        >
          {lang === 'es' ? 'EN' : 'ES'}
        </button>

        <button
          type="button"
          className={styles.smallControl}
          onClick={() => setDark(!dark)}
          aria-label="Cambiar tema"
        >
          {dark ? '☾' : '☀'}
        </button>

        <button
          type="button"
          className={styles.mobileMenuButton}
          onClick={() => setMenuOpen((current) => !current)}
          aria-label="Abrir menú"
        >
          {menuOpen ? '×' : '☰'}
        </button>
      </div>

      {menuOpen && (
        <div className={styles.mobileMenu}>
          {navItems.map((item) => (
            <button
              key={item.key}
              type="button"
              className={`${styles.mobileNavLink} ${page === item.key ? styles.mobileNavLinkActive : ''}`}
              onClick={() => go(item.key)}
            >
              {item.label}
            </button>
          ))}
        </div>
      )}
    </header>
  );
}