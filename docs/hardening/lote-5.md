# Lote 5 – Streaming y reintentos

## 1. Objetivo

Que un fallo de red o del agente nunca duplique mensajes ni conversaciones, que la interfaz nunca se quede en "respondiendo", que el usuario no pierda lo que escribió y que "Detener" funcione siempre.

## 2. Archivos modificados y diff resumido

| Archivo | Cambio |
|---|---|
| `frontend/src/services/chatService.ts` | Reescritura de `sendMessage`/`processStream`. **Reintentos idempotentes:** solo si la petición falló antes de que el servidor abriera el stream (error de red o 502/503/504), hasta 3 intentos con backoff, reutilizando el `conversationId` ya recibido. Nunca tras el primer chunk, un evento SSE `error` ni un 4xx (validación 400/413, 429, 404) ni un 500. **Stream sin `done`** = error (antes la UI quedaba en "respondiendo"). `sendMessage` nunca lanza: todo fallo restaura el texto (`CHAT_RECOVER_MESSAGE`). Cancelación limpia (sin error ni recuperación). Ids con `crypto.randomUUID()`. Fetch autenticado en un solo helper (antes repetido 6 veces). Aprobación MCP sin reintento (no es idempotente). |
| `frontend/src/contexts/ChatServiceContext.tsx` | Nuevo. Una instancia de `ChatService` por sesión; lee siempre el `getAccessToken` más reciente; cancela el stream activo al terminar la sesión (desmontar el proveedor). |
| `frontend/src/App.tsx`, `frontend/src/components/daily/DailyAgentChat.tsx` | `ChatServiceProvider` alrededor de `DailyShell`; `DailyAgentChat` usa `useChatService()` (antes creaba un servicio por montaje y, tras navegar y volver, "Detener" no alcanzaba el stream en curso). |
| `frontend/src/components/daily/DailyChatInterface.tsx` | Restaura texto **y adjuntos** cuando un envío falla. |
| `frontend/src/types/errors.ts`, `frontend/src/utils/errorHandler.ts` | Códigos `VALIDATION` (400/413/422; muestra el motivo del servidor) y `RATE_LIMIT` (429; muestra `Retry-After`); `status` en `AppError`; `createAppErrorFromResponse`. |
| `frontend/src/types/appState.ts`, `frontend/src/reducers/appReducer.ts` | `CHAT_RECOVER_MESSAGE` quita los mensajes por id (antes "los dos últimos", que fallaba si había una aprobación MCP en medio) y muestra el motivo real cuando hubo un solo intento. |

## 3. Tests añadidos

- Activados del lote 1: 4xx sin reintento y con input restaurado; sin reintento tras el primer chunk o un error SSE; stream sin `done` libera la UI y restaura el input; reintento transitorio con el mismo `conversationId`; cancelación sin error ni mensajes duplicados.
- `chatService.retries.test.ts`: motivo de validación visible y solo se quitan los mensajes optimistas; 429 sin reintento con el tiempo de espera; 503 → 3 intentos con el mismo `conversationId`; 500 sin reintento; sin token → texto restaurado sin tocar el hilo; `sendMessage` nunca rechaza.
- `ChatServiceContext.test.tsx`: una instancia por sesión; cancela al terminar la sesión; exige el proveedor.
- `DailyChatInterface.test.tsx`: restaura texto y adjuntos tras un fallo.
- `appReducer.test.ts`: recuperación por ids, sin quitar mensajes cuando el fallo ocurre antes de añadirlos, y mensaje con el motivo real.

## 4. Comandos y resultados exactos

| Comando | Resultado |
|---|---|
| Activación de los tests del lote | `3 failed | 7 passed` en `chatService.stream.test.ts` (rojo esperado) |
| `npx tsc -b` | 0 errores |
| `npm run lint` | 0 errores |
| `npx vitest run` | 13 archivos, **243/243** |
| `npm run build` | OK |

## 5. Fallos preexistentes

Ninguno.

## 6. Decisiones

- **Cuándo se reintenta:** el agente añade el mensaje del usuario a la conversación en cuanto acepta la petición; reintentar después duplicaría el mensaje (y antes podía crear hasta 3 conversaciones). Por eso solo se reintenta lo que falló antes de abrir el stream y es transitorio (red, 502, 503, 504). Un 500 es un fallo del servidor, no transitorio.
- **"Cancelar al desmontar":** se cancela al terminar la sesión (desmontaje del proveedor). Salir de la ruta del chat no cancela: la respuesta sigue llegando y, al volver, se ve el progreso y "Detener" funciona.
- **Si el stream falla a mitad,** se quitan de la vista el mensaje del usuario y la respuesta parcial y se restaura el texto. El mensaje puede quedar en el historial de Foundry; al reenviarlo, el agente lo verá dos veces. Es preferible a perder lo escrito.

## 7. Riesgos pendientes

Ninguno nuevo. Mensajes de error genéricos en inglés (`errors.ts`) en una interfaz en español: pendiente de i18n (fuera del alcance de estos lotes).

## 8. Acciones para el equipo

Ninguna.

## 9. Sin modificar a propósito

Cola de mensajes y regenerar/editar (comportamiento funcional; reenvían a la misma conversación y quedan cubiertos por la propiedad del lote 2).
