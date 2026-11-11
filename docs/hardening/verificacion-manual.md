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
| 9 Telemetría | **Fallo de CSP**: `connect-src` bloqueó `https://js.monitor.azure.com/scripts/b/ai.config.1.cfg.json` | El SDK de Application Insights descarga su configuración dinámica de ese origen. Se corrige en código añadiendo exactamente `https://js.monitor.azure.com` a `connect-src` (sin comodines; `script-src` no cambia). Pendiente de verificar en una imagen nueva. |
| Aviso `unload` en consola | Informativo, sin cambios | La app **no** envía `unload` en `Permissions-Policy` (cabecera actual: `camera=(), microphone=(), geolocation=(), payment=()`). El aviso lo genera el propio navegador por la retirada del evento `unload`. Se mantiene la política actual. |

**Evento aislado de respuesta de respaldo (*fallback*).** En una consulta por el nombre de una especialista presente en el índice, el agente devolvió una vez la respuesta de respaldo ("no encontré información"). Al repetir la misma consulta 5 veces, las **5 respondieron correctamente** con datos del índice.

- Clasificación: evento aislado, no reproducible. **No bloquea el merge** salvo que se reproduzca.
- Sin cambios en Foundry (agente, versión ni instrucciones), en la conexión `azureairesourceprueba2`, en Search (índice, vectorizador, configuración semántica), en claves ni en Copilot Studio.
- Si se reproduce: anotar fecha y hora (UTC), revisión activa y el `operation_Id` de Application Insights, **sin** copiar el contenido de la conversación, y revisar el resultado de la herramienta de búsqueda en la traza de Foundry antes de tocar ninguna configuración.
- El nombre de la persona se omite a propósito en este registro (no se guardan identificadores personales sin seudonimizar).
