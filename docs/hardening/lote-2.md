# Lote 2 – Propiedad y autorización por recurso

> **Clasificación: CURRENT.** Registro de la propiedad y la autorización por recurso (implementado en la rama; se despliega con A2).

## 1. Objetivo

Que cada usuario del inquilino CONSEIN acceda solo a sus propias conversaciones, mensajes, streams, aprobaciones MCP y archivos, con respuesta **404** si no es el dueño, y que todo endpoint quede protegido por defecto.

## 2. Archivos modificados y diff resumido

Commits: `391438c` (backend), `fd04eb1` (eliminación de UI no alcanzable), y el de este informe (frontend y documentación).

| Archivo | Cambio |
|---|---|
| `backend/WebApp.Api/Security/UserIdentity.cs` | Nuevo. `IUserIdentityAccessor`; propietario = `SHA256("{tid}:{oid}")` en hexadecimal en minúsculas; `IsAllowedTenant` (falla cerrado si no hay inquilino configurado). |
| `backend/WebApp.Api/Security/ConversationOwnership.cs` | Nuevo. `IConversationOwnershipStore`, `ConversationOwnerRequirement`, `ConversationOwnerAuthorizationHandler` y `ConversationMetadata` (helpers puros: owner, título, archivos autorizados en `files0..files9`, formato de id). |
| `backend/WebApp.Api/Services/AgentFrameworkService.cs` | Implementa `IConversationOwnershipStore` sobre la metadata de Foundry. `CreateConversationAsync(ownerKey, …)` guarda `owner` y `title`. `ListConversationsAsync(ownerKey, …)` filtra por owner (máx. 500 revisadas). Se eliminan `ConversationStore` y sus respaldos (inventaban ids desconocidos para Foundry y ocultaban errores) y `ListUploadedFilesAsync`/`CleanupUploadedFilesAsync`. |
| `backend/WebApp.Api/Program.cs` | `FallbackPolicy` = autenticado + `tid` CONSEIN + `Chat.ReadWrite`; `MapGroup("/api")`; comprobación de propietario en envío/stream (incluye edición y regeneración, que reenvían a la misma conversación), aprobaciones MCP (exigen `conversationId`), lectura de mensajes, borrado y descarga; registro de archivos citados al terminar el stream; `GET /api/files/{fileId}?conversationId=` exige propietario y archivo asociado; rutas `/api` desconocidas → 404 ProblemDetails. Eliminados `POST /api/files/cleanup` y `GET /api/files/uploaded`. |
| `backend/WebApp.ServiceDefaults/Extensions.cs` | `/health` y `/alive` con `AllowAnonymous()`. |
| `backend/WebApp.Api/Models/UploadedFilesModels.cs`, `Services/ConversationStore.cs`, `WebApp.Api.Tests/ConversationStoreTests.cs` | Eliminados. |
| `frontend/src/services/chatService.ts` | `downloadFile(fileId, conversationId, …)`; ids codificados en URLs; eliminados `getUploadedFilesInfo` y `cleanupUploadedFiles`. |
| `frontend/src/components/**` (44 archivos) | Eliminada la UI genérica no alcanzable desde `main.tsx` (único consumidor de los métodos retirados). Bundle de producción idéntico. Se conserva `utils/citationParser.ts`. |

## 3. Tests añadidos

- Activados del lote 1: B recibe 404 al leer, al enviar/continuar el stream y al aprobar MCP de A; B no descarga archivos de A; la descarga exige `conversationId`; el listado solo devuelve lo propio y oculta conversaciones sin owner; cleanup/uploads no expuestos; `tid` de otro inquilino → 403; ruta `/api` desconocida → 404 ProblemDetails (antes pasaba solo porque en tests no hay `index.html`).
- Nuevos de integración: owner guardado con hash y sin `tid`/`oid` en claro; el dueño descarga un archivo citado; el dueño no descarga un archivo no citado; el dueño aprueba su MCP; aprobación MCP sin conversación → 400; B recibe 404 al borrar.
- Nuevos unitarios: `UserIdentityTests` (hash exacto, tipos de claim cortos y mapeados, inquilino) y `ConversationMetadataTests` (título, archivos, sin duplicados, límites de Foundry de 16 claves y 512 caracteres, ids peligrosos, formato de id).
- Frontend: `downloadFile` envía `conversationId` y reporta 404 como error de API.

## 4. Comandos y resultados exactos

| Comando | Resultado |
|---|---|
| Activación de los tests del lote (antes de implementar) | `Error: 9, Correcto: 65, Omitido: 3, Total: 77` (rojo esperado) |
| `dotnet build backend/WebApp.sln` | 0 errores, 0 advertencias |
| `dotnet test backend/WebApp.sln` | `Error: 0, Correcto: 92, Omitido: 3, Total: 95` |
| `npx tsc -b` | 0 errores |
| `npx vitest run` | 178 pasados, 6 omitidos, 3 todo |
| `npm run build` | OK, mismo bundle (876,64 kB) |
| `npm run lint` | 5 errores preexistentes |

## 5. Fallos preexistentes

Los 5 errores de lint.

## 6. Decisiones

- **Fuente de verdad:** la metadata de la conversación en Foundry. No hay store propio.
- **404 y no 403** para conversaciones ajenas o sin owner: no se revela su existencia.
- **Archivos autorizados en metadata:** `files0..files9`, cada valor ≤ 512 caracteres con entradas `fileId` o `containerId|fileId`. Si se llena, se descartan los más antiguos (sus archivos dejan de poder descargarse). La actualización de metadata en Foundry reemplaza el diccionario completo, así que se lee y se combina (conserva `owner` y `title`). Si el registro falla, se registra una advertencia y el stream termina igual (disponibilidad antes que una función secundaria).
- **Escritura concurrente:** dos streams simultáneos en la misma conversación pueden perder un registro de archivo (última escritura gana). Riesgo bajo: la UI no permite dos envíos a la vez.
- **Listado:** filtrado en la app sobre las 500 conversaciones más recientes del agente. Si el volumen crece, mover el índice de propiedad a Cosmos DB.
- **`/api/files/uploaded`:** ya no existe como listado. Esa ruta coincide ahora con la descarga (`fileId = "uploaded"`) y se rechaza con 400 por falta de `conversationId`.
- **DELETE de conversación:** sigue devolviendo 501 (el SDK tipado no lo expone); ahora solo para el dueño.
- **UI no alcanzable eliminada** en este lote porque era el único consumidor de los métodos retirados: adaptarla habría sido trabajo sobre código muerto.

## 7. Riesgos pendientes

- La limpieza de imágenes subidas (`webapp-upload-*`) deja de tener endpoint. Queda como tarea operativa (ver [acciones-azure.md](acciones-azure.md), A3).

## 8. Acciones para el equipo

### Nota operativa antes del despliegue (avisar a los usuarios)

> Con esta versión, cada conversación de Daily queda ligada a quien la creó. **Las conversaciones anteriores a la actualización no tienen propietario y dejarán de verse en Daily**: no se borran, pero ya no se pueden abrir desde la aplicación. No es posible asignarlas a sus autores porque nunca se registró quién las creó. Hoy Daily no muestra historial de conversaciones, así que el efecto visible es nulo; lo será si se reactiva el historial.

Ver también A3 en [acciones-azure.md](acciones-azure.md).

## 9. Sin modificar a propósito

App roles, `appRoleAssignmentRequired`, listas de usuarios; manejo de errores y cabeceras (lote 3); asignación retroactiva de owner.
