# Lote 3 – Seguridad del backend, límites y cabeceras

## 1. Objetivo

Limitar el abuso (coste y disponibilidad), centralizar el manejo de errores sin filtrar detalles internos, no exponer el prompt del agente y añadir cabeceras de seguridad con una CSP construida a partir de los endpoints reales y compatible con MSAL.

## 2. Archivos modificados y diff resumido

| Archivo | Cambio |
|---|---|
| `backend/WebApp.Api/Program.cs` | `IExceptionHandler` + ProblemDetails en todos los entornos (se eliminan los `try/catch` por endpoint); validación de mensaje y adjuntos **antes** de crear la conversación o llamar a Foundry; `RequestSizeLimit` en `/api/chat/stream`; rate limiting por usuario (después de la autenticación); `UseForwardedHeaders` → `UseHsts` (fuera de Development) → cabeceras; scope de logs con el owner hasheado; CORS solo en Development o con orígenes configurados (antes producción aceptaba `localhost` con credenciales); descargas con tipo de contenido seguro (HTML, SVG, JS → `application/octet-stream`); el cargador de `.env` ya no sobrescribe variables existentes; cancelación del cliente sin registrar error; errores del stream ya iniciado saneados. |
| `backend/WebApp.Api/Options/ChatLimitsOptions.cs` | Límites configurables y validados al arrancar: longitud del mensaje, número y tamaño de imágenes y archivos, total por mensaje, texto incrustado, tamaño del body, tamaño de descarga, peticiones por minuto. |
| `backend/WebApp.Api/Services/AttachmentProcessor.cs` | Validación pura de adjuntos: tipo, `;base64`, tamaño estimado antes de decodificar, firma binaria de imágenes y PDF, UTF-8 en texto, coincidencia del MIME declarado, total por mensaje, texto incrustado máximo, nombres de archivo saneados. **`text/html` rechazado.** |
| `backend/WebApp.Api/Services/AgentFrameworkService.cs` | Recibe adjuntos ya validados; si falla la subida de una imagen, borra las ya subidas; `/api/agent` solo devuelve la metadata permitida (`logo`). |
| `backend/WebApp.Api/Models/AgentMetadata.cs` | Se elimina `Instructions` del DTO. |
| `backend/WebApp.Api/Infrastructure/*` | `ApiExceptionHandler` (Foundry 401/403/5xx → 502, 429 → 503; validación → 400 con detalle), `RequestValidationException`, `RateLimiting`. |
| `backend/WebApp.Api/Security/SecurityHeaders.cs` | Cabeceras y CSP (ver 6). Página de redirect silenciosa opcional, desactivada por defecto. |
| `backend/WebApp.Api/Security/UserLogScope.cs` | Scope `UserKey` (16 caracteres del hash) en cada log autenticado. |
| `backend/WebApp.Api/appsettings*.json` | Logging en Information en producción (antes `Debug` para los servicios); secciones `ChatLimits` y `SecurityHeaders`; CORS de localhost solo en Development. |
| `backend/WebApp.Api/Models/ErrorResponse.cs` | Mensajes para 502. |

## 3. Tests añadidos

- Activados del lote 1: `/api/agent` sin `Instructions` ni metadata interna; adjunto `text/html` → 400 sin crear conversación; cabeceras de seguridad y CSP (con `X-Forwarded-Proto: https` y host no local, como detrás del ingress).
- `BackendHardeningTests`: mensaje demasiado largo → 400 sin llamar a Foundry; errores de validación explican qué corregir; rate limit 429 con `Retry-After` y por usuario; health checks sin rate limit; body excesivo → 413 en **Kestrel real**; error del stream saneado; metadata pública; página de redirect silenciosa desactivada por defecto y, activada, con `frame-ancestors 'self'` solo en su ruta; CSP con los endpoints de Application Insights configurados.
- `ContentSecurityPolicyBuilderTests`, `ApiExceptionHandlerTests`, `AttachmentProcessorTests` (19) y la fila 502 de `ErrorResponseFactoryTests`.

## 4. Comandos y resultados exactos

| Comando | Resultado |
|---|---|
| Activación de los tests del lote (antes de implementar) | `Error: 3, Correcto: 92, Omitido: 0, Total: 95` (rojo esperado) |
| `dotnet build backend/WebApp.sln` | 0 errores, 0 advertencias |
| `dotnet test backend/WebApp.sln` | `Error: 0, Correcto: 134, Omitido: 0, Total: 134` |

## 5. Fallos preexistentes

Ninguno en el backend.

## 6. Decisiones

**CSP** (por cabecera, configurable con `SecurityHeaders`):

```
default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self';
script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:;
connect-src 'self' https://login.microsoftonline.com <endpoints de Application Insights>;
frame-src https://login.microsoftonline.com
```

- **Entra (MSAL):** autoridad `https://login.microsoftonline.com/{tenant}` en `authConfig.ts`; token, descubrimiento e iframe de renovación usan ese origen.
- **Application Insights:** el SDK va empaquetado desde npm (no se carga de `js.monitor.azure.com`, así que `script-src` queda en `'self'`). Los hosts de `connect-src` se leen al arrancar de `APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING` (la misma cadena con la que se compila la SPA); hoy son `centralus-2.in.applicationinsights.azure.com` y `centralus.livediagnostics.monitor.azure.com`. Solo se usan los hosts; la cadena no se registra.
- **`style-src 'unsafe-inline'`:** Fluent UI v9 (Griffel; `FluentProvider`, `Spinner`, `ErrorBoundary` con `makeStyles`) inserta `<style>` en tiempo de ejecución, y React renderiza atributos `style` en la pantalla de carga de `App.tsx`. Los scripts siguen siendo solo `'self'`.
- **`frame-ancestors 'none'`** y `X-Frame-Options: DENY`. La renovación silenciosa de MSAL carga la redirect URI en un iframe del mismo origen, que esta directiva bloquea: el lote 4 hace que el frontend caiga a redirección ante errores de iframe y timeout. Opción preparada y **desactivada por defecto**: `SecurityHeaders:SilentRedirectPageEnabled` sirve `/redirect.html` con `frame-ancestors 'self'` solo en esa ruta (requiere registrar la redirect URI en Entra: [acciones-azure.md](acciones-azure.md), A4).
- **HSTS:** `UseForwardedHeaders` (X-Forwarded-For/Proto, un salto, cualquier proxy: el contenedor solo es accesible a través del ingress) antes de `UseHsts`. **Sin `UseHttpsRedirection`:** el ingress ya redirige (`allowInsecure: false`) y las sondas de salud llaman por HTTP sin `X-Forwarded-Proto`.

**Límites por defecto:** mensaje 16 000 caracteres; 5 imágenes de 5 MB; 10 archivos de 20 MB; 20 MB en total; 200 000 caracteres de texto incrustado; body 30 MB; descarga 50 MB; 20 envíos de chat y 120 llamadas a la API por minuto y usuario. El rate limiting vive en memoria por réplica: con N réplicas el límite efectivo es N veces mayor (para cuotas compartidas, API Management; ver lote 7).

**Errores:** un 401/403 de Foundry se devuelve como 502 (es un problema de permisos del servidor, no una sesión caducada; antes provocaba bucles de inicio de sesión). Los errores de validación muestran su detalle (describen la entrada del usuario); el resto se sanea fuera de Development.

## 7. Riesgos pendientes

- La CSP solo se puede validar del todo con Entra real y un navegador: procedimiento en `verificacion-manual.md` (lote 4). Para probar sin romper a los usuarios: `SecurityHeaders:ReportOnly=true`.
- Si se añade otra región de Application Insights o un CDN, hay que actualizar `SecurityHeaders:AdditionalConnectSources`.

## 8. Acciones para el equipo

A4 en [acciones-azure.md](acciones-azure.md) (solo si se activa la página de redirect silenciosa).

## 9. Sin modificar a propósito

Detección de herramientas por nombre de tipo y `GetLastUsage` (dependencia de orden) en el servicio: no son de seguridad; quedan para un refactor posterior.
