# Verificación manual: CSP, MSAL y sesión

> **Clasificación: RECOMMENDED NOW.** Procedimiento que el equipo debe ejecutar con Entra real antes de desplegar la imagen nueva (A2).

Estos casos necesitan Microsoft Entra ID real y un navegador, por eso no están en la CI. Ejecútalos en el entorno de desarrollo (o en local) **antes de desplegar en producción**, con la CSP activa (valor por defecto).

## Preparación

1. Navegador Chromium o Edge reciente, con las DevTools abiertas en las pestañas **Console** y **Network** (con "Preserve log").
2. Una cuenta del inquilino CONSEIN sin sesión iniciada (ventana InPrivate).
3. Comprobar que la respuesta de `/` incluye la cabecera `Content-Security-Policy` (Network → documento → Headers) y que contiene `frame-ancestors 'none'`, `img-src 'self' data: blob:` y los hosts de Application Insights configurados.

En todos los casos, la consola **no** debe mostrar errores `Refused to … because it violates the following Content Security Policy directive`. Si aparece alguno, anota la directiva y la URL bloqueada; para probar una corrección sin afectar a los usuarios usa `SecurityHeaders__ReportOnly=true`.

## Casos

| # | Caso | Pasos | Resultado esperado |
|---|---|---|---|
| 1 | Inicio de sesión | Abrir la URL de la app en InPrivate. | Redirección a `login.microsoftonline.com`, inicio de sesión y vuelta a `/home`. Sin errores de CSP. |
| 2 | `/api/agent` | Tras el caso 1, en Network buscar `GET /api/agent`. | 200; la respuesta no contiene `instructions`; `metadata` solo tiene `logo`. |
| 3 | Envío de mensajes | Ir a "Consultar Daily", enviar una pregunta sobre un caso indexado. | Respuesta en streaming (`POST /api/chat/stream`, `text/event-stream`), sin errores de CSP. |
| 4 | `acquireTokenSilent` con CSP | Enviar un segundo mensaje. En Network no debe haber navegación a `login.microsoftonline.com`. | El token sale de la caché o del refresh token (petición `POST …/oauth2/v2.0/token` permitida por `connect-src`). |
| 5 | Recarga | Recargar la página (F5) en la misma pestaña. | La sesión sigue activa sin volver a pedir credenciales (estado en `sessionStorage` de la pestaña). |
| 6 | Pestaña nueva (`sessionStorage`) | Abrir la app en una pestaña nueva de la misma ventana. | **Comportamiento aceptado:** la pestaña nueva no comparte la caché de tokens. Se produce un inicio de sesión silencioso o una redirección breve a Entra sin pedir contraseña (la sesión de Entra sigue viva). Cerrar la pestaña borra su estado. |
| 7 | Respaldo por redirección | Simular la caducidad: en DevTools → Application → Session Storage, borrar las entradas de MSAL de la pestaña excepto la cuenta (o esperar a que caduque el refresh token, ~24 h) y enviar un mensaje. | La renovación silenciosa por iframe falla (el iframe es bloqueado por `frame-ancestors 'none'`; puede verse en consola) y la app redirige a Entra y vuelve sola. No queda la pantalla colgada en "respondiendo". |
| 8 | Error de inicio de sesión | Cancelar el inicio de sesión en la página de Entra (o usar una cuenta de otro inquilino). | Mensaje "No se pudo iniciar sesión" con el botón "Reintentar"; no se queda en "Signing in…". |
| 9 | Telemetría | Con Application Insights configurado, en Network filtrar `applicationinsights`. | Las peticiones de ingesta responden 200 (no bloqueadas por CSP) y no contienen texto de mensajes. |
| 10 | Imagen externa | Pedir al agente que responda con `![x](https://example.com/x.png)`. | Se muestra `[x]`; en Network **no** hay petición a `example.com`. |

## Caso opcional: página de redirect silenciosa

Solo si se aplicó la acción A4 de [acciones-azure.md](acciones-azure.md) (`SecurityHeaders__SilentRedirectPageEnabled=true`, `VITE_MSAL_SILENT_REDIRECT_ENABLED=true` y redirect URI registrada):

| # | Caso | Resultado esperado |
|---|---|---|
| 11 | Repetir el caso 7 | La renovación se completa en el iframe oculto contra `/redirect.html` **sin** redirección de página completa. `/redirect.html` responde con `frame-ancestors 'self'` y `X-Frame-Options: SAMEORIGIN`; el resto de rutas sigue con `'none'` / `DENY`. |

## Registro

Anota para cada caso: fecha, entorno, navegador, resultado (OK / fallo) y, si falla, la directiva CSP o el código de error de MSAL (sin copiar tokens ni claims).

### 2026-11-11 · revisión `ca-web-blx2jbksxc7iu--heb3666a` (imagen `web@sha256:524eb95e…66cd`)

Pruebas manuales ejecutadas por el usuario en el navegador, con Entra real.

| Caso | Resultado | Nota |
|---|---|---|
| 1 Inicio de sesión | OK | |
| 2 `/api/agent` autenticado | **PASS** (200) | |
| 3 Envío de mensajes | OK, con un evento aislado (ver abajo) | |
| 9 Telemetría | **Fallo de CSP**: `connect-src` bloqueó `https://js.monitor.azure.com/scripts/b/ai.config.1.cfg.json` | El SDK de Application Insights descarga su configuración dinámica de ese origen. Se corrige en código añadiendo exactamente `https://js.monitor.azure.com` a `connect-src` (sin comodines; `script-src` no cambia). Corregido en `57a36af` y verificado en `--h57a36af` (ver abajo). |
| Aviso `unload` en consola | Informativo, sin cambios | La app **no** envía `unload` en `Permissions-Policy` (cabecera actual: `camera=(), microphone=(), geolocation=(), payment=()`). El aviso lo genera el propio navegador por la retirada del evento `unload`. Se mantiene la política actual. |

**Evento aislado de respuesta de respaldo (*fallback*).** En una consulta por el nombre de una especialista presente en el índice, el agente devolvió una vez la respuesta de respaldo ("no encontré información"). Al repetir la misma consulta 5 veces, las **5 respondieron correctamente** con datos del índice.

- Clasificación: **ISOLATED / NOT REPRODUCED** (5/5 consultas posteriores correctas). **No bloquea el merge**. Si se reproduce, se abrirá como incidencia separada.
- Sin cambios en Foundry (agente, versión ni instrucciones), en la conexión `azureairesourceprueba2`, en Search (índice, vectorizador, configuración semántica), en claves ni en Copilot Studio.
- Si se reproduce: anotar fecha y hora (UTC), revisión activa y el `operation_Id` de Application Insights, **sin** copiar el contenido de la conversación, y revisar el resultado de la herramienta de búsqueda en la traza de Foundry antes de tocar ninguna configuración.
- El nombre de la persona se omite a propósito en este registro (no se guardan identificadores personales sin seudonimizar).

### 2026-11-11 · Validación final · revisión `ca-web-blx2jbksxc7iu--h57a36af`

Resultado: **PASS** (validación funcional). Estado del hardening: **GO FOR REVIEW**.

- Revisión validada: `ca-web-blx2jbksxc7iu--h57a36af` (Single revision mode, 100 % del tráfico, Healthy, `restartCount` 0).
- Imagen validada: `crblx2jbksxc7iu.azurecr.io/web@sha256:9a8ea7283c0735fcd7d7b1574471f238888ed62747786e186994812dc3db6a17` (tag `web:sha-57a36afab528`, ejecución ACR `cj8`, linux/amd64).
- Commit: `57a36afab528c6c317e4869c30b4e8952c505f28` (corrección de CSP).
- CI: verde sobre `57a36af` (ejecución 43080388975). Frontend: lint, TypeScript, 248/248 tests y build. Backend: build y 153/153 tests. Transporte Base64: correcto.
- Configuración de la Container App sin cambios aparte de la imagen: las huellas de variables y de la configuración no secreta coinciden con la línea base.
- Comprobaciones automáticas (solo lectura): `/api/health`, `/health`, `/alive` y SPA → 200; `/api/agent` sin token → 401; cabeceras de seguridad presentes; arranque sin excepciones; sin 5xx ni errores de Foundry.
- Pruebas manuales ejecutadas por el usuario en el navegador, con Entra real.
- Ninguna de estas comprobaciones requirió rollback.

| Caso | Resultado | Nota |
|---|---|---|
| 1 Inicio de sesión con Microsoft Entra ID | **PASS** | Navegación a `/home` y `/daily` correcta. |
| 2 `/api/agent` autenticado | **PASS** | |
| 3 Envío de mensajes y streaming | **PASS** | El texto completo de la respuesta se conserva. |
| Recuperación desde Azure AI Search | **PASS** | 5/5 consultas adicionales correctas. |
| Cancelación del stream | **PASS** | |
| 6 Pestaña nueva (`sessionStorage`) | **PASS** | |
| Adjuntos | **PASS** | |
| CSP | **PASS** | `connect-src` publicado: `'self' https://login.microsoftonline.com https://js.monitor.azure.com https://centralus-2.in.applicationinsights.azure.com https://centralus.livediagnostics.monitor.azure.com`. Sin comodines; `Permissions-Policy` y `frame-ancestors 'none'` sin cambios. Ya no aparece la violación anterior. |
| Configuración dinámica de Application Insights | **PASS** | `GET https://js.monitor.azure.com/scripts/b/ai.config.1.cfg.json` → **200 OK**. |
| 9 Telemetría del frontend | **PASS** | Las peticiones `track` responden correctamente. |
| Aviso `unload` en consola | Informativo, **no bloquea** | Lo genera el navegador; `Permissions-Policy` no se modifica. |
| Propiedad con dos usuarios reales del inquilino | **PENDING REAL-TENANT VALIDATION** | No es un fallo. Se cubre hoy con los tests de integración, en los que el usuario B recibe 404 sobre los recursos del usuario A. Falta repetirlo en el navegador con dos cuentas reales del inquilino. |

Incidente inicial de recuperación (revisión `--heb3666a`): **ISOLATED / NOT REPRODUCED**. Las 5 consultas posteriores fueron correctas.
