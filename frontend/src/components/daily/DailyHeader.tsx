import { useState } from 'react';
import {
  Link,
  NavLink,
} from 'react-router-dom';

import type {
  DailyLang,
} from '../../config/dailyTranslations';

import { DailyLogo } from './DailyLogo';
import styles from './DailyShell.module.css';

interface DailyHeaderProps {
  lang: DailyLang;
  setLang: (lang: DailyLang) => void;
  dark: boolean;
  setDark: (value: boolean) => void;
  t: any;
}

interface NavigationItem {
  path: string;
  label: string;
}

export function DailyHeader({
  lang,
  setLang,
  dark,
  setDark,
  t,
}: DailyHeaderProps) {
  const [menuOpen, setMenuOpen] = useState(false);

  const navItems: NavigationItem[] = [
    {
      path: '/home',
      label: t.nav.home,
    },
    {
      path: '/daily',
      label: t.nav.consult,
    },
    {
      path: '/technology',
      label: t.nav.technology,
    },
    {
      path: '/faq',
      label: t.nav.faq,
    },
  ];

  const closeMenuAndScroll = () => {
    setMenuOpen(false);

    window.scrollTo({
      top: 0,
      behavior: 'instant' as ScrollBehavior,
    });
  };

  return (
    <header className={styles.header}>
      <Link
        to="/home"
        className={styles.brandButton}
        onClick={closeMenuAndScroll}
        aria-label="Ir al inicio de Daily"
      >
        <span className={styles.headerLogo}>
          <DailyLogo size={18} />
        </span>

        <span className={styles.brandText}>
          <strong>CONSEIN</strong>
          <span>Daily</span>
        </span>
      </Link>

      <nav
        className={styles.desktopNav}
        aria-label="Navegación principal"
      >
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            onClick={closeMenuAndScroll}
            className={({ isActive }) =>
              `${styles.navLink} ${
                isActive ? styles.navLinkActive : ''
              }`
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>

      <div className={styles.headerActions}>
        <button
          type="button"
          className={styles.smallControl}
          onClick={() =>
            setLang(lang === 'es' ? 'en' : 'es')
          }
          aria-label={
            lang === 'es'
              ? 'Cambiar idioma a inglés'
              : 'Change language to Spanish'
          }
        >
          {lang === 'es' ? 'EN' : 'ES'}
        </button>

        <button
          type="button"
          className={styles.smallControl}
          onClick={() => setDark(!dark)}
          aria-label={
            dark
              ? 'Activar tema claro'
              : 'Activar tema oscuro'
          }
        >
          {dark ? '☾' : '☀'}
        </button>

        <button
          type="button"
          className={styles.mobileMenuButton}
          onClick={() =>
            setMenuOpen((current) => !current)
          }
          aria-label={
            menuOpen
              ? 'Cerrar menú'
              : 'Abrir menú'
          }
          aria-expanded={menuOpen}
          aria-controls="daily-mobile-menu"
        >
          {menuOpen ? '×' : '☰'}
        </button>
      </div>

      {menuOpen && (
        <nav
          id="daily-mobile-menu"
          className={styles.mobileMenu}
          aria-label="Navegación móvil"
        >
          {navItems.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              onClick={closeMenuAndScroll}
              className={({ isActive }) =>
                `${styles.mobileNavLink} ${
                  isActive
                    ? styles.mobileNavLinkActive
                    : ''
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      )}
    </header>
  );
}