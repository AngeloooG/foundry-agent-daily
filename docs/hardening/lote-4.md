# Lote 4 – Seguridad del frontend

## 1. Objetivo

Endurecer la sesión (MSAL), el renderizado de respuestas del modelo (Markdown), los adjuntos y la telemetría, sin enviar ni registrar contenido de usuario, tokens ni claims.

## 2. Archivos modificados y diff resumido

Commits: `a8d0df7` (dependencias) y el de este informe (código, tests y documentación).

| Archivo | Cambio |
|---|---|
| `frontend/src/config/authConfig.ts` | `cacheLocation: sessionStorage` (decisión aceptada), sin `storeAuthStateInCookie`; `silentTokenRequest` con `/redirect.html` solo si `VITE_MSAL_SILENT_REDIRECT_ENABLED=true` (desactivado). |
| `frontend/src/hooks/useAuth.ts` | Cuenta activa; `acquireTokenSilent`; respaldo `acquireTokenRedirect` ante `InteractionRequired` **y** ante fallos del iframe silencioso o timeout (`requiresRedirectFallback`). Sin popup; sin registrar errores con detalle. |
| `frontend/src/utils/authFallback.ts` | Nuevo. Decide cuándo pasar a redirección (`monitor_window_timeout`, `timed_out`, `block_iframe_reload`, `iframe_closed_prematurely`). |
| `frontend/src/main.tsx` | Maneja el fallo de `msal.initialize()` con un mensaje en lugar de una página en blanco. |
| `frontend/src/App.tsx` | Maneja el error de `useMsalAuthentication` (mensaje y botón "Reintentar"; antes se quedaba en "Signing in…"). |
| `frontend/public/redirect.html` | Página en blanco para la renovación silenciosa (solo se usa si se activa). |
| `frontend/src/components/daily/SafeMarkdown.tsx`, `frontend/src/utils/markdownSafety.ts` | Markdown con `rehype-sanitize`; imágenes solo del mismo origen, `data:` o `blob:` (las externas se muestran como `[alt]`); enlaces solo `http(s)`/`mailto` con `target="_blank"` y `rel="noopener noreferrer"`; `javascript:`, `file:` y protocolos desconocidos bloqueados. Memoizado. |
| `frontend/src/utils/fileAttachments.ts` | Sin `text/html`; `validateAttachmentSelection` con códigos traducibles: tipo, extensión que coincide con el tipo, archivo vacío, tamaño, número de archivos e imágenes, total por mensaje; `ACCEPTED_FILE_TYPES`. |
| `frontend/src/config/dailyTranslations.ts` | Mensajes de error de adjuntos en español e inglés. |
| `frontend/src/components/daily/DailyChatInterface.tsx` | Usa `SafeMarkdown`; valida adjuntos al seleccionar y al enviar (no borra el texto si hay errores); `accept` en el selector; reinicia el input para poder volver a elegir el mismo archivo; `aria-label` en el área de texto. |
| `frontend/src/services/telemetry.ts`, `frontend/src/utils/errorHandler.ts` | Sin correlación CORS ni cabeceras de respuesta; seguimiento de rutas activado; `trackException` acepta `AppError` sin enviar "[object Object]" ni contenido. |
| `frontend/src/utils/sseParser.ts`, `frontend/src/services/chatService.ts`, `frontend/src/contexts/AppContext.tsx` | Sin logs de payloads SSE, errores con contenido ni acciones completas (el logger de desarrollo solo registra el tipo de acción). |
| `frontend/src/components/daily/DailyHeader.tsx`, `DailyHome.tsx` | Tipos en lugar de `any` (los 5 errores de lint preexistentes). |
| `frontend/package.json`, `package-lock.json`, `.npmrc` | Ver 6. |
| `docs/hardening/verificacion-manual.md` | Procedimiento manual (CSP, MSAL, `sessionStorage`). |

## 3. Tests añadidos

- `markdownSafety.test.ts`: imágenes permitidas y bloqueadas; enlaces `javascript:`, `data:`, `vbscript:`, `file:` y `ms-msdt:` bloqueados; `SafeMarkdown` no renderiza imágenes externas, abre enlaces con `rel` y no deja pasar `<script>`.
- `attachmentSelection.test.ts`: tipos admitidos; `text/html` y tipos desconocidos rechazados; extensión que no coincide; archivo vacío; tamaño con su límite; número de imágenes y archivos; total; `accept` sin HTML.
- `authFallback.test.ts`: redirección ante `InteractionRequired` y fallos del iframe; sin redirección para otros errores.
- `DailyChatInterface.test.tsx` (Testing Library): imagen externa no renderizada; enlaces con `rel`; adjunto HTML rechazado con mensaje traducido, sin envío y sin borrar el texto; `accept`; adjuntos válidos se envían.
- Activado del lote 1: `text/html` rechazado en el cliente.

## 4. Comandos y resultados exactos

| Comando | Resultado |
|---|---|
| Activación del test del lote | `1 failed | 178 passed | 5 skipped` (rojo esperado) |
| `npx tsc -b` | 0 errores |
| `npm run lint` | **0 errores** (antes 5) |
| `npx vitest run` | 11 archivos: 226 pasados, 5 omitidos (lote 5) |
| `npm run build` | OK; `index-*.js` 880,03 kB; `dist/redirect.html` incluido |
| `npm audit --omit=dev` | `found 0 vulnerabilities` |
| `npm audit` | 16 (2 bajas, 14 moderadas), todas de desarrollo |
| `npm ci` sin `.npmrc` | OK |

## 5. Fallos preexistentes

Ninguno (los errores de lint quedan corregidos).

## 6. Decisiones

- **Dependencias eliminadas** (sin imports tras retirar la UI no alcanzable): `@fluentui-copilot/react-copilot`, `@fluentui-copilot/react-copilot-chat`, `react-syntax-highlighter` (y sus tipos), `copy-to-clipboard`, `remark-breaks`, `rehype-highlight`, `yjs`, `date-fns`, `clsx`. Con ello desaparece `happy-dom` (alta, recomendación del lote 0b). Sin conflictos de peer deps, se retira `frontend/.npmrc` (`legacy-peer-deps`) y sus referencias en README, devcontainer, tareas de VS Code y guías de Copilot.
- **Testing Library** añadida como dependencia de desarrollo (16.3.3 y 10.4.2, publicadas antes del 2026-10-27).
- **Moderadas restantes:** cadenas `eslint 10`/`typescript-eslint` → `minimatch`, `vitest` y `@vitest/mocker`, `@humanfs/node`; bajas `@babel/core` y `esbuild`. Todas de desarrollo; las de eslint exigen bajar de versión mayor. No se actualizan (política del lote 0b: solo altas y críticas).
- **MSAL sin popup:** el respaldo es siempre `acquireTokenRedirect` (los popups suelen bloquearse y la CSP no permite el iframe de la app).
- **Imágenes externas:** no se muestran (SharePoint, Blob u otros dominios). Es intencional.
- **Las imágenes `data:` del modelo** tampoco se muestran: `react-markdown` las elimina antes del componente. Es más estricto que la regla y no afecta a ninguna función actual.

## 7. Riesgos pendientes

- La CSP y MSAL solo se validan del todo con Entra real: [verificacion-manual.md](verificacion-manual.md).
- La redirección diaria al caducar el refresh token es el coste aceptado de `frame-ancestors 'none'`; la acción A4 lo evita si se desea.

## 8. Acciones para el equipo

Ejecutar [verificacion-manual.md](verificacion-manual.md) en desarrollo. A4 (opcional) en [acciones-azure.md](acciones-azure.md).

## 9. Sin modificar a propósito

Reintentos, streaming y ciclo de vida del servicio de chat (lote 5); moderadas de desarrollo.
