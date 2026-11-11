# Daily Enhanced: endurecimiento de seguridad (lotes 0–7)

Rama `hardening/daily-security` → `Develop-Daily`. **Borrador: no fusionar sin revisión senior.** Toca autenticación, autorización y cabeceras de seguridad. No cambia RBAC ni red: esos cambios están documentados como acciones para el equipo.

> **Arquitectura objetivo vigente: `internal-minimal`** (aprobada el 2026-11-11; ver [lote-7-simplificado.md](lote-7-simplificado.md)): reutiliza los recursos existentes y requiere **0 recursos nuevos obligatorios**. Clasificación de cada recomendación: **CURRENT** (funciona hoy), **RECOMMENDED NOW** (apropiado para Daily interno), **FUTURE** (arquitectura avanzada que no se despliega ahora), **LEGACY - DO NOT DEPLOY** (diseño de 45 recursos descartado) y **CLOSED** (hallazgo cerrado). Clasificación final en [lote-7-simplificado.md](lote-7-simplificado.md), sección 23.

## Resumen

- **Aislamiento entre usuarios.** Cada conversación, mensaje, stream, aprobación MCP y archivo pertenece a quien lo creó. Si no eres el dueño, recibes **404**. El propietario es `SHA256("{tid}:{oid}")`; nunca se guarda `tid:oid` en claro.
- **Acceso.** Cualquier usuario autenticado del inquilino CONSEIN con el scope `Chat.ReadWrite`. Política por defecto (`FallbackPolicy`) que exige token válido, `tid` del inquilino y scope. Solo son anónimos los health checks y la SPA. Sin app roles ni listas de usuarios.
- **Endpoints eliminados:** `POST /api/files/cleanup` (permitía a cualquiera borrar las cargas de todos) y `GET /api/files/uploaded`.
- **Backend:** rate limiting por usuario, límites de tamaño validados antes de llamar a Foundry, errores `ProblemDetails` sin detalles internos, `/api/agent` sin el prompt del agente, cabeceras de seguridad y una CSP construida con los endpoints reales (MSAL y Application Insights), y logs sin contenido de usuario.
- **Frontend:**
  - MSAL con `sessionStorage` y respaldo por redirección.
  - Markdown sin imágenes externas ni protocolos peligrosos; cierra el canal de exfiltración sin clic.
  - Validación de adjuntos.
  - Telemetría sin conversaciones.
- **Streaming:** reintentos idempotentes, solo antes del primer chunk y solo ante fallos transitorios. Un stream sin `done` termina en error, la interfaz nunca se queda en "respondiendo", el texto del usuario se restaura y la cancelación no se muestra como error.
- **Dependencias:** 10 vulnerabilidades altas o críticas corregidas con parches o versiones menores. En runtime, `npm audit` queda en 0.
- **Infraestructura:** perfil `internal-minimal` aprobado. Reutiliza la Container App, Foundry, Search, SQL, el ACR, Application Insights y Log Analytics existentes, con 0 recursos nuevos obligatorios. El diseño anterior de red privada (45 recursos) queda como LEGACY / FUTURE. La nueva versión de Bicep está pendiente de aprobación. **Nada desplegado.**

## Aviso a los usuarios antes del despliegue

> Con esta versión, cada conversación de Daily queda ligada a quien la creó. **Las conversaciones anteriores a la actualización no tienen propietario y dejarán de verse en Daily**: no se borran, pero ya no se pueden abrir desde la aplicación. No se asignan de forma retroactiva porque nunca se registró quién las creó.
>
> Otros cambios visibles: las imágenes externas en las respuestas no se muestran (es intencional), una pestaña nueva puede pedir iniciar sesión de nuevo (`sessionStorage`) y, aproximadamente una vez al día, la renovación de la sesión puede hacer una redirección completa (acción A4 para evitarlo).

## Lotes

| Lote | Contenido | Informe |
|---|---|---|
| 0 | Línea base, inventario de código y de Azure (solo lectura) y CI sin acceso a Azure (`.github/workflows/ci.yml`) | [lote-0.md](lote-0.md) |
| 0b | Dependencias: vite, ws, postcss, browserslist, brace-expansion, zod y `@nevware21/ts-utils` (npm); Pkcs, Xml y DataProtection 10.0.14 (NuGet). Una por commit, todas publicadas hace más de 14 días | [lote-0b.md](lote-0b.md) |
| 1 | Interfaz `IAgentFrameworkService` para sustituir Foundry en tests; `WebApplicationFactory` con autenticación de prueba (usuarios A y B) y un Foundry falso; tests objetivo de los lotes siguientes | [lote-1.md](lote-1.md) |
| 2 | Propiedad por conversación en la metadata de Foundry; `ConversationOwnerAuthorizationHandler`; archivos autorizados por conversación (`GET /api/files/{fileId}?conversationId=`); eliminación de `ConversationStore` y de los endpoints de limpieza; `FallbackPolicy` y `MapGroup("/api")` | [lote-2.md](lote-2.md) |
| 3 | Rate limiter por usuario (con hash), `ChatLimitsOptions`, `AttachmentProcessor` (bytes mágicos, sin HTML), `IExceptionHandler`, logging Information con clave de usuario con hash, `UseForwardedHeaders`, HSTS, CSP y cabeceras | [lote-3.md](lote-3.md) |
| 4 | MSAL (cuenta activa, respaldo `acquireTokenRedirect`, `/redirect.html` opcional desactivado), `SafeMarkdown`, validación de adjuntos traducible, telemetría sin contenido; 44 archivos de interfaz inalcanzables y 9 dependencias sin uso eliminados; sin `legacy-peer-deps` | [lote-4.md](lote-4.md) |
| 5 | Política de reintentos idempotente, recuperación por ids de mensaje, `ChatServiceProvider` (una instancia por sesión, cancelación al desmontar) | [lote-5.md](lote-5.md) |
| 6 | Análisis (sin cambios en Azure): cargo y área desde Office 365 Users; indexación incremental con `rowversion`; borrado lógico; ejecución a demanda más una programación diaria; vectorizador verificado; Copilot Studio con clave de consulta dedicada. El procedimiento de ingesta queda **bloqueado** hasta que exista `docs/sql/` | [lote-6.md](lote-6.md) |
| 7 | **Vigente:** perfil `internal-minimal`, [matriz-rbac.md](matriz-rbac.md), verificación V1 (Foundry → Search) y procedimiento de A10. **Legacy:** `infra/platform/`, el what-if del diseño de 45 recursos ([whatif/](whatif/README.md)) y [lote-7.md](lote-7.md). **Future:** [red-privada.md](red-privada.md) | [lote-7-simplificado.md](lote-7-simplificado.md) |

## Tests

| Conjunto | Resultado |
|---|---|
| Backend (`dotnet test backend/WebApp.sln`) | **134/134**. Línea base: 51, de los que se retiraron los tests tautológicos y los de `ConversationStore` |
| Frontend (`npm run test:run`) | **243/243** en 13 archivos. Línea base: 174 en 6 |
| `npm run lint` / `npx tsc -b` | 0 errores. Línea base: 5 errores de lint |
| `npm audit` | Runtime: 0. Total: 16 (2 bajas y 14 moderadas, todas de herramientas de desarrollo; la corrección exige saltos de versión mayor y queda como recomendación) |

Casos de integración destacados:

- B recibe 404 al leer, enviar, continuar el stream, aprobar MCP o descargar archivos de A.
- Las conversaciones sin propietario no aparecen.
- Cleanup no está expuesto.
- `text/html` se rechaza.
- Los límites se aplican antes de llamar a Foundry (también 413 en Kestrel real).
- Las cabeceras y la CSP están presentes.
- Los health checks son anónimos y el resto de `/api` exige autenticación.

La verificación con Entra real (CSP con MSAL, `acquireTokenSilent`, redirección, pestaña nueva) es manual: [verificacion-manual.md](verificacion-manual.md).

**Estado de validación (2026-11-11):**

- Validación funcional **PASS** en la revisión `ca-web-blx2jbksxc7iu--h57a36af`, imagen `web@sha256:9a8ea728…6a17`, commit `57a36af`, con la CI en verde.
- Hardening: **GO FOR REVIEW**.
- Pendiente: la propiedad con dos usuarios reales del inquilino (**PENDING REAL-TENANT VALIDATION**).

## Configuración nueva

| Clave | Por defecto | Uso |
|---|---|---|
| `ChatLimits:*` | ver `appsettings.json` | Longitud del mensaje, cuerpo, adjuntos, texto incrustado y descarga |
| `ChatLimits:ChatRequestsPerMinute` / `ApiRequestsPerMinute` | 20 / 120 | Rate limit por usuario (ventana de 1 minuto) |
| `SecurityHeaders:ContentSecurityPolicy` | (construida automáticamente) | Sustituye la CSP completa |
| `SecurityHeaders:ReportOnly` | `false` | Desplegar la CSP en modo informe para probarla sin bloquear |
| `SecurityHeaders:AdditionalConnectSources` | vacío | Orígenes adicionales para `connect-src` |
| `SecurityHeaders:SilentRedirectPageEnabled` y `VITE_MSAL_SILENT_REDIRECT_ENABLED` | `false` | `/redirect.html` para la renovación silenciosa (requiere A4) |
| `APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING` | — | De ahí se leen los hosts de ingesta que se añaden a la CSP |

## Despliegue

1. Revisión senior del PR (seguridad, autenticación y cabeceras).
2. Avisar a los usuarios (texto de arriba).
3. Desplegar en desarrollo con el flujo habitual. **Se reconstruye la imagen** (A2), así que también se actualiza el runtime de .NET.
4. Ejecutar [verificacion-manual.md](verificacion-manual.md). Si la CSP bloquea algo inesperado, activar `SecurityHeaders__ReportOnly=true` mientras se ajusta.
5. Producción, solo después de la verificación en desarrollo.

Rollback: reactivar la revisión anterior de la Container App. Los datos no cambian (la versión anterior ignora la clave `owner` de la metadata), pero vuelven las vulnerabilidades corregidas, como el acceso a conversaciones ajenas: úsalo solo como medida temporal.

## Acciones en Azure para el equipo (nada ejecutado)

Detalle con comandos, comprobación previa, impacto y rollback en [acciones-azure.md](acciones-azure.md).

| Acción | Tema | Clasificación | Revisión senior |
|---|---|---|---|
| A1 | Foundry ApiKey → identidad administrada / Entra ID (V1: hoy usa clave; tarea independiente, no implementada) | RECOMMENDED NOW | Sí |
| A2 | Reconstruir la imagen (runtime de .NET corregido) | RECOMMENDED NOW | — |
| A3 | Retención de imágenes subidas | RECOMMENDED NOW | — |
| A4 | Redirect URI para la renovación silenciosa de MSAL | RECOMMENDED NOW, opcional | Sí |
| A5 | Búsqueda híbrida en el agente (hoy `query_type: simple`) | RECOMMENDED NOW, sin semántico mientras Search sea Free | — |
| A6 | Copilot Studio: clave de consulta dedicada `daily-copilot-studio` y fuente duplicada | RECOMMENDED NOW, si hoy usa una clave de administración (verificar antes) | Sí |
| A7 | Indexador a demanda desde el flujo más programación diaria | RECOMMENDED NOW | — |
| A8 | Indexación incremental y borrado lógico en la vista | FUTURE | Sí |
| A9 | Cuota de `gpt-5.1` en centralus | CLOSED / LEGACY INFRASTRUCTURE FINDING | — |
| A10 | Mínimo privilegio de la identidad web: retirada secuencial de 2 roles (tarea posterior) | RECOMMENDED NOW | Sí |
| A11 | Migración a red privada | FUTURE – ROADMAP / ADVANCED HARDENING | Sí |
| A12 | Firewall de `knowledgeserver` (`AllowAllWindowsAzureIps`) | FUTURE – Future infrastructure hardening | Sí |
| A13 | Asignaciones administrativas excesivas o desconocidas | Hallazgo de gobierno externo (escalar a CONSEIN) | Sí |

## Pendientes conocidos

- `docs/sql/`: exportar el procedimiento de ingesta y la vista para cerrar el análisis del lote 6.
- `infra/main.bicep` (IaC de `azd` de la web app) sigue desalineado con lo desplegado; se reconciliará en la nueva versión de Bicep `internal-minimal` (pendiente de aprobación).
- Search Free es el entorno actual de desarrollo y pruebas. Su identidad saliente funciona, pero no está documentada para ese nivel: no es base para producción. Para una futura producción interna se recomienda Basic (FUTURE).
- Desactivar la autenticación local en Search, Foundry o Application Insights **solo** después de un inventario que demuestre que ningún consumidor usa claves (hoy, Copilot Studio y la conexión de Foundry usan claves de Search). El componente de Application Insights del navegador mantiene la autenticación local.
- Mensajes de error genéricos del frontend en inglés (i18n).
- Fijar las acciones de GitHub por SHA.
