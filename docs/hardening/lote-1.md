# Lote 1 – Tests de integración y abstracción de Foundry

> **Clasificación: CURRENT.** Registro de los tests de integración y la abstracción de Foundry (implementado en la rama).

## 1. Objetivo

Poder probar el pipeline real del API (autenticación, autorización, endpoints, SSE) con un Foundry falso **antes** de cualquier refactor, y dejar escritos los tests que definen el comportamiento objetivo de los lotes siguientes.

## 2. Archivos modificados y diff resumido

| Archivo | Cambio |
|---|---|
| `backend/WebApp.Api/Services/IAgentFrameworkService.cs` | Nuevo. Interfaz 1:1 con la superficie pública de `AgentFrameworkService`. |
| `backend/WebApp.Api/Services/AgentFrameworkService.cs` | Implementa la interfaz (1 línea). |
| `backend/WebApp.Api/Program.cs` | `AddScoped<IAgentFrameworkService, AgentFrameworkService>()`; los 9 endpoints reciben la interfaz; `public partial class Program;` para `WebApplicationFactory`. |
| `backend/WebApp.Api.Tests/WebApp.Api.Tests.csproj` | + `Microsoft.AspNetCore.Mvc.Testing` 10.0.14. |
| `backend/WebApp.Api.Tests/TestSupport/TestAuthHandler.cs` | Esquema de autenticación de prueba: `oid`, `tid` y `scp` por cabecera (sin tokens reales). |
| `backend/WebApp.Api.Tests/TestSupport/FakeFoundry.cs` | Fake de Foundry en memoria (conversaciones, stream fijo, descargas, cleanup). |
| `backend/WebApp.Api.Tests/TestSupport/ApiFactory.cs` | `WebApplicationFactory<Program>` en entorno Production, sin llamadas a Azure. |
| `backend/WebApp.Api.Tests/CriticalRoutesIntegrationTests.cs` | Tests de rutas críticas. |
| `frontend/src/services/__tests__/chatService.stream.test.ts` | Tests de la ruta crítica `sendMessage` → SSE → acciones. |

Sin cambios de comportamiento.

## 3. Tests añadidos

Backend (usuarios A y B, inquilino CONSEIN):

| Estado | Casos |
|---|---|
| Activos (comportamiento a conservar) | health checks anónimos (`/api/health`, `/health`, `/alive`); 401 sin token en 5 rutas; 403 sin `Chat.ReadWrite`; contrato SSE (`conversationId → chunk → chunk → annotations → usage → done`); reutilización del `conversationId` sin duplicar conversaciones; el dueño lee su conversación; `/api/agent` |
| Objetivo lote 2 (`[Ignore]`) | B recibe 404 al leer; B recibe 404 al enviar o continuar el stream; B no aprueba MCP de A; B no descarga archivos de A; la descarga exige `conversationId`; el listado solo muestra lo propio y oculta conversaciones sin propietario |
| Objetivo lote 3 (`[Ignore]`) | cleanup y uploads fuera del API; `tid` de otro inquilino rechazado; `/api/agent` sin `Instructions` ni metadata interna; adjunto `text/html` → 400 sin efectos secundarios; cabeceras de seguridad y CSP; rutas `/api` desconocidas → 404 |

Frontend:

| Estado | Casos |
|---|---|
| Activos | flujo feliz de `sendMessage`; envío del `conversationId` existente |
| Objetivo lote 5 (`it.skip`) | 4xx sin reintento y con input restaurado; sin reintento tras el primer chunk o un error SSE; stream sin `done` libera la UI y restaura el input; reintento transitorio con el mismo `conversationId`; cancelación sin error ni mensajes duplicados |
| Objetivo lote 4 | `text/html` rechazado (`it.skip`); Markdown externo, enlaces y archivos vacíos (`it.todo`) |

Cada lote activa sus tests: primero los ve fallar y luego implementa.

## 4. Comandos y resultados exactos

| Comando | Resultado |
|---|---|
| `dotnet build backend/WebApp.sln` | 0 errores, 0 advertencias |
| `dotnet test backend/WebApp.sln` | `Error: 0, Correcto: 64, Omitido: 13, Total: 77` |
| `npx tsc -b` | 0 errores |
| `npx vitest run` | 7 archivos: 176 pasados, 6 omitidos, 3 todo |

## 5. Fallos preexistentes

5 errores de lint (`no-explicit-any`), sin cambios.

## 6. Decisiones

- La interfaz copia la superficie actual tal cual (incluido `GetLastUsage`, que depende del orden de llamada) para no mezclar refactor con pruebas. Se reduce en el lote 2.
- Tests objetivo marcados `[Ignore]` / `it.skip` con el lote que los activa, para que la CI siga en verde en tests.

## 7. Riesgos pendientes

Ninguno nuevo. `Mvc.Testing` es solo de tests.

## 8. Acciones para el equipo

Ninguna.

## 9. Sin modificar a propósito

Comportamiento del API, frontend de producción y configuración.
