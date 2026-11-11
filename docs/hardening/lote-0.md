# Lote 0 – Línea base, inventario y CI

> **Clasificación: CURRENT.** Registro de la línea base, el inventario y las verificaciones de Azure (solo lectura) del 2026-11-10. Arquitectura vigente: `internal-minimal` ([lote-7-simplificado.md](lote-7-simplificado.md)).

## 1. Objetivo

Medir el estado de partida de `Develop-Daily` (commit `c378ea5`) antes de cualquier cambio, inventariar el código relevante para seguridad, verificar la configuración de Azure con lecturas y crear una CI que no toca Azure.

## 2. Archivos modificados

| Archivo | Cambio |
|---|---|
| `.github/workflows/ci.yml` | Nuevo. Node 22.23.4 y .NET SDK 10.0.303 fijados; restore, build, lint, `tsc`, tests (JUnit y TRX), build de producción; `npm audit` y `dotnet list package --vulnerable` **informativos**; informes como artefactos. Sin login en Azure, sin secretos, sin OIDC (`permissions: contents: read`), sin `az`/`azd`, sin despliegue. |
| `docs/hardening/lote-0.md`, `docs/hardening/acciones-azure.md` | Nuevos. |

## 3. Tests añadidos

Ninguno (lote de medición).

## 4. Comandos y resultados exactos (código original)

| Comando | Resultado |
|---|---|
| `npm ci` | OK; 538 paquetes; `25 vulnerabilities (2 low, 13 moderate, 10 high)` |
| `npm run lint` | **5 errores** `@typescript-eslint/no-explicit-any`: `DailyHeader.tsx:19`, `DailyHome.tsx:165,187,222,263` |
| `npx tsc -b` | 0 errores |
| `npm run test:run` | 6 archivos, 174/174 |
| `npm run build` | OK; `index-*.js` 876,64 kB (gzip 251,62 kB), aviso de chunk > 500 kB |
| `npm audit` | 2 bajas, 13 moderadas, 10 altas, 0 críticas. En runtime (`--omit=dev`): 3 altas |
| `dotnet restore backend/WebApp.sln` | OK, con avisos NU de vulnerabilidad |
| `dotnet build backend/WebApp.sln` | 0 errores, 13 advertencias (todas NU de vulnerabilidad) |
| `dotnet test backend/WebApp.sln` | `Error: 0, Correcto: 51, Omitido: 0, Total: 51` |
| `dotnet list backend/WebApp.sln package --vulnerable --include-transitive` | Ver tabla NuGet |

### Vulnerabilidades npm altas

| Paquete | Rango afectado | Directa | Runtime | Origen | Corrección |
|---|---|---|---|---|---|
| `@nevware21/ts-utils` | ≤ 0.13.0 | No | **Sí** | `@microsoft/applicationinsights-web` 3.4.1 | Parche/menor disponible |
| `happy-dom` | ≤ 21.0.3 | No | **Sí** (ruta) | `@fluentui-copilot/*` → `@lexical/headless`; también par opcional de vitest | Parche/menor disponible |
| `ws` | 8.0.0 – 8.20.1 | No | **Sí** (ruta) | dependencias de `happy-dom`/`jsdom` | Parche/menor disponible |
| `vite` | 7.0.0 – 7.3.6 | Sí | No (dev) | — | Parche/menor disponible |
| `postcss`, `nanoid`, `source-map-js`, `browserslist`, `brace-expansion`, `zod` | varios | No | No (dev) | herramientas de build/test | Parche/menor disponible |

Ninguna requiere una versión mayor. Las moderadas de la cadena `eslint 10` / `typescript-eslint` → `minimatch` (GHSA-g3hv-75wg-m249) solo se corrigen bajando a eslint 9 (cambio mayor): **recomendación, no se actualizan**.

### Vulnerabilidades NuGet

| Proyecto | Paquete | Versión | Tipo | Severidad | Corregida en |
|---|---|---|---|---|---|
| WebApp.Api | `System.Security.Cryptography.Pkcs` | 10.0.0 | Transitiva | Alta + moderada | 10.0.13 |
| WebApp.Api.Tests | `Microsoft.AspNetCore.DataProtection` (CVE-2026-40372) | 10.0.0 | Transitiva | **Crítica** | 10.0.7 |
| WebApp.Api.Tests | `System.Security.Cryptography.Xml` | 10.0.6 | Directa | Alta (×8) | 10.0.10 |
| WebApp.Api.Tests | `System.Security.Cryptography.Pkcs` | 10.0.6 | Transitiva | Alta | 10.0.13 |

Impacto: la de producción es `Pkcs` (DoS). Las del proyecto de tests no se despliegan, pero el runtime de .NET de la imagen desplegada (construida en 08/2026) probablemente contiene las versiones afectadas: se corrige reconstruyendo la imagen (ver `acciones-azure.md`).

## 5. Fallos preexistentes

- 5 errores de lint (`no-explicit-any`). La CI quedará en rojo en el paso de lint hasta el lote 4.
- 25 vulnerabilidades npm y 4 paquetes NuGet vulnerables (pasos informativos en la CI; se corrigen en el lote 0b).
- Advertencia de tamaño de bundle (876 kB).

## 6. Inventario de código

| Punto | Hallazgo |
|---|---|
| `tid` y `oid` | El backend **no los lee**. Solo `Microsoft.Identity.Web` valida el issuer del inquilino configurado (`ENTRA_TENANT_ID`). |
| Scope real | `Chat.ReadWrite`. SPA: `api://{clientId}/Chat.ReadWrite` (`authConfig.ts:67,71`). API: `RequireScope("Chat.ReadWrite")` (`Program.cs:96`). Existe en Entra: tipo User, habilitado; app de un solo inquilino (`AzureADMyOrg`), sin app roles, `appRoleAssignmentRequired=false`. |
| Políticas | Una sola política (`RequireChatScope` = autenticado + scope) aplicada endpoint a endpoint a los 9 de `/api/*`. Sin `FallbackPolicy`. |
| Endpoints anónimos | `/api/health`, `/health`, `/alive`, archivos estáticos y el fallback de la SPA (`MapFallbackToFile("index.html")`). Las rutas `/api/*` inexistentes devuelven `index.html` con 200. |
| ¿Sirve la SPA? | Sí: `UseDefaultFiles` + `UseStaticFiles` desde `wwwroot` y fallback de la SPA. |
| Aprobaciones MCP | Implementadas en el backend (`ChatRequest.McpApproval` → `ResponseItem.CreateMcpApprovalResponseItem`) y en `chatService.sendMcpApproval`. La UI de Daily **no** tiene tarjeta de aprobación. El agente `Daily-Enhanced` v10 **no tiene herramientas MCP** (solo `azure_ai_search`). No comprueban propietario. |
| Fuente de verdad de conversaciones | Foundry (Conversations API). `ConversationStore` se instancia por request, así que su "respaldo" nunca persiste: es código muerto que además inventa un GUID si Foundry falla. |
| Variables | `AI_AGENT_ENDPOINT`, `AI_AGENT_ID`, `AI_AGENT_VERSION`, `MANAGED_IDENTITY_CLIENT_ID`: `AgentFrameworkService.cs:79-99`. `AZURE_CLIENT_ID`: **no se usa en el código** (está en la Container App, añadida fuera de Bicep). `AI_AGENT_VERSION` tampoco está en Bicep. |
| Secretos en el repositorio | Ninguno encontrado (búsqueda de claves, cadenas de conexión, Instrumentation Keys, SAS, JWT y claves privadas). |

## 7. Verificaciones de Azure (solo lectura)

| Punto | Resultado |
|---|---|
| Foundry → Search | La conexión `azureairesourceprueba2` (compartida, a nivel de cuenta y de proyecto) usa **`ApiKey`**, no identidad. |
| Search Index Data Reader | La identidad del proyecto de Foundry lo tiene sobre el servicio de Search, pero hoy no se usa. |
| `authOptions` / `disableLocalAuth` de Search | `aadOrApiKey` (`http401WithBearerChallenge`) / `false`. Acceso público, SKU free, identidad del sistema. |
| Cuenta de Foundry | `disableLocalAuth=false`, acceso público. |
| Identidad de la Container App | Solo asignada por el usuario: `id-web-daily-enhanced-dev` (clientId `1c1e6a9a…`). Roles: Azure AI Developer, Cognitive Services OpenAI Contributor y Cognitive Services User sobre el **proyecto**; AcrPull sobre el ACR. |
| Variables desplegadas | `MANAGED_IDENTITY_CLIENT_ID` = `AZURE_CLIENT_ID` = clientId de la identidad; `AI_AGENT_ID=Daily-Enhanced`; `AI_AGENT_VERSION=10`; `AI_AGENT_ENDPOINT` = endpoint del proyecto. Ninguna usa `secretRef`. |
| Vectorizador del índice | `daily-vectorizer`: `azureOpenAI`, `resourceUri` `https://prj-daily-enhanced-dev-resource.openai.azure.com`, `text-embedding-3-small` (deployment y modelo), sin `apiKey`, sin `authIdentity`. Campo `chunk_vector` de 1536 dimensiones. **Verificado.** |
| Indexador | **Sin programación** (`schedule: null`); se ejecuta a demanda. Último resultado: success, 3/3, 0 errores, 6 advertencias. |
| Skillset | Coincide con lo documentado (SplitSkill pages/2000/300/`es`; embedding sin clave; 32 campos proyectados exactos). |
| Origen de datos | `azuresql`, `[dbo].[vw_DailyConocimiento_Search]`, sin directivas de detección de cambios ni de borrados. |
| Agente Foundry | `Daily-Enhanced` v10, gpt-5.1; herramienta `azure_ai_search` sobre el asset `index-dailyprueba5vectorsearch/versions/2` con `query_type: simple`, top 5 (**sin búsqueda vectorial ni semántica**, al contrario que Copilot Studio). |

Las lecturas de Search usan `az rest --method get --resource https://search.azure.com` con `api-version=2024-07-01` y `--query` para excluir cualquier propiedad con credenciales.

## 8. Decisiones

- CI: las auditorías no bloquean (pasos informativos). Lint y tests sí bloquean.
- Node fijado a 22.23.4 (misma rama mayor que `deployment/docker/frontend.Dockerfile`), publicada hace más de 14 días.
- Acciones de GitHub fijadas por etiqueta (`@v4`). Fijarlas por SHA queda como recomendación.

## 9. Riesgos pendientes

- No se puede desactivar la autenticación local de Search: Foundry y Copilot Studio usan claves.
- La CI estará en rojo (lint) hasta el lote 4.

## 10. Acciones para el equipo

Ver [acciones-azure.md](acciones-azure.md).

## 11. Sin modificar a propósito

Azure, Entra, RBAC, Copilot Studio, dependencias y código funcional.
