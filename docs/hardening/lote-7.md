# Lote 7 – Infraestructura como código (sin desplegar)

> **LEGACY DESIGN - DO NOT DEPLOY.** Este informe describe el diseño anterior de red privada completa (45 recursos en producción y 22 en desarrollo). Se conserva solo como referencia histórica y arquitectónica. La arquitectura objetivo vigente es **`internal-minimal`**: ver [lote-7-simplificado.md](lote-7-simplificado.md). El inventario de la sección 2 y la evidencia de la sección 5 siguen siendo válidos como registro del estado al 2026-11-10. Las conclusiones sobre el nivel de Search (S1) y el indexador privado en Basic están corregidas en el anexo H de ese documento.

## 1. Objetivo

Describir en Bicep la plataforma compartida de Daily (Search, SQL, Foundry, Key Vault, identidades, monitorización, alertas y red privada opcional), parametrizada por entorno y sin secretos. Además: validarla con `what-if` sin tocar nada, diseñar la red privada y documentar la matriz RBAC de mínimo privilegio.

## 2. Inventario de recursos existentes (`rg-daily-enhanced-dev-centralus`, lecturas del 2026-11-10)

| Recurso | Tipo / SKU | Hallazgos |
|---|---|---|
| `prj-daily-enhanced-dev-resource` | Foundry (`AIServices`, S0) más el proyecto `prj-daily-enhanced-dev` | Público (`defaultAction: Allow`), claves habilitadas, identidad del sistema, sin inyección de VNet |
| `prj-daily-enhanced-dev-resource-appinsights`, `-logs` | Monitorización del proyecto de Foundry | — |
| `cae-blx2jbksxc7iu` / `ca-web-blx2jbksxc7iu` | Entorno y Container App | Sin VNet; ingress externo sin restricciones de IP; réplicas de 0 a 3; solo identidad asignada por el usuario |
| `crblx2jbksxc7iu` | ACR Basic | Público; admin y pull anónimo deshabilitados |
| `appi-blx2jbksxc7iu`, `appi-fe-blx2jbksxc7iu`, `law-blx2jbksxc7iu` | Application Insights del backend y del frontend; Log Analytics | — |
| `knowledgeserver` / `free-sql-db-knowledge-specialist` | SQL, GP_S_Gen5 (oferta gratuita) | Público, Entra-only, TLS 1.2; reglas `AllowAllWindowsAzureIps` y 5 `ClientIPAddress_*` |
| `storageresourceprueba` | Storage Standard_RAGRS | Fuera del alcance de Daily (sin uso en el código) |
| `id-web-daily-enhanced-dev` | Identidad asignada por el usuario | Roles de más sobre el proyecto (ver la matriz RBAC) |
| `azureairesourceprueba2` | Search Free | Público, `aadOrApiKey`, identidad del sistema |

## 3. Archivos modificados y diff resumido

| Archivo | Cambio |
|---|---|
| `infra/platform/main.bicep` | Nuevo. Alcance de suscripción; grupo `rg-daily-<env>-centralus`; nombres sin "prueba" con sufijo estable; 4 private endpoints condicionales; salidas sin secretos; interruptores `enablePrivateNetworking`, `keepSearchPublic`, `enableAgentVnetInjection` y `disableLocalAuth` |
| `infra/platform/main.dev.bicepparam`, `main.prod.bicepparam` | Nuevos. Sin secretos: el grupo administrador de SQL y el correo de alertas salen de variables de entorno |
| `infra/platform/modules/*.bicep` (10) | Nuevos: `monitoring`, `network`, `private-endpoint`, `identity`, `keyvault`, `sql`, `search`, `foundry`, `rbac`, `alerts` |
| `infra/platform/README.md` | Nuevo: descripción, parámetros, validación, despliegue (no ejecutado), pasos del plano de datos, rollback y coste |
| `docs/hardening/red-privada.md` | Nuevo: diseño de red, flujos, limitaciones de Copilot Studio, costes, migración por fases, rollback y pruebas de DNS y conectividad |
| `docs/hardening/matriz-rbac.md` | Nuevo: 6 identidades × 7 destinos con evidencia, rol mínimo, alcance, riesgo, prueba previa y estado |
| `docs/hardening/whatif/dev.txt`, `prod.txt` | Nuevos: salida del what-if (solo identificadores de recurso, sin secretos) |
| `docs/hardening/acciones-azure.md` | Nuevas A9 (cuota), A10 (mínimo privilegio de la web app), A11 (red privada), A12 (firewall de SQL) y A13 (asignaciones privilegiadas) |
| `docs/hardening/README.md` | Nuevo: resumen de los lotes 0–7 (cuerpo del PR) |

## 4. Tests añadidos

Ninguno de código: el lote no cambia la aplicación. La validación de la infraestructura es `az bicep build`, `az bicep build-params` y `az deployment sub what-if`.

## 5. Comandos ejecutados y resultados exactos

| Comando | Resultado |
|---|---|
| `az bicep build --file infra/platform/main.bicep --stdout` | OK, **0 advertencias** (una advertencia BCP037 se suprime de forma justificada en el capability host, igual que la plantilla oficial) |
| `az bicep build-params` (dev y prod) | OK |
| `az bicep build --file infra/main.bicep --stdout` (IaC de la web app, sin cambios) | OK, 5 advertencias BCP318 preexistentes |
| `az deployment sub what-if ... main.dev.bicepparam --parameters modelDeployments='[]'` | `Resource changes: 17 to create, 5 unsupported.` Ningún cambio en recursos existentes |
| `az deployment sub what-if ... main.prod.bicepparam --parameters modelDeployments='[]' enableAgentVnetInjection=false` | `Resource changes: 40 to create, 5 unsupported.` Ningún cambio en recursos existentes |
| what-if de prod con `enableAgentVnetInjection=true` (2 intentos, más 2 pruebas aisladas en una copia temporal con las API `2025-06-01` y `2025-04-01-preview`) | `InternalServerError` de ARM en todos. Sin `networkInjections`, el mismo what-if pasa: el fallo está en el análisis de esa propiedad |
| what-if con los despliegues de modelos de los bicepparam | `InsufficientQuota`: `gpt-5.1` GlobalStandard en centralus, uso 500 frente a límite 250 (A9) |
| Lecturas de inventario (`az resource list`, `az role assignment list --all`, `az acr show`, `az containerapp env show`, `az containerapp show`, `az cognitiveservices account show`, `az sql server show`, `az sql server firewall-rule list`) | Resultados en la sección 2 y en la matriz RBAC. Ninguna devuelve secretos (se usó `--query`) |
| `npm run lint` / `npx tsc -b` | 0 errores / 0 errores |
| `npm run test:run` | `Test Files 13 passed (13)`, `Tests 243 passed (243)` |
| `dotnet build backend/WebApp.sln` | 0 errores |
| `dotnet test backend/WebApp.sln` | Windows bloqueó la ejecución de `WebApp.Api.Tests.exe` (`MSB6003 ... Acceso denegado`), igual que el 06/11; es una política local del equipo, no de la CI. Ejecutado con el host: `dotnet backend/WebApp.Api.Tests/bin/Debug/net10.0/WebApp.Api.Tests.dll` → `failed: 0, succeeded: 134, skipped: 0` |

Los 5 *unsupported* son las asignaciones de rol, cuyo nombre depende de identidades creadas en el mismo despliegue. ARM no puede calcularlas antes de desplegar.

## 6. Fallos preexistentes

- `infra/main.bicep` (IaC de `azd` de la web app): 5 advertencias BCP318 y desalineado con lo desplegado (el grupo `rg-env-DailyEnhanced` que referencia ya no existe). No se ha tocado (ver la sección 9).
- La cuota de `gpt-5.1` impide desplegar la plataforma con los despliegues de modelos previstos (A9).

## 7. Decisiones y por qué

- **Plataforma nueva, en paralelo.** No adopta los recursos actuales: los nombres sin "prueba" exigen recursos nuevos, y tanto la cuenta de Foundry como el entorno de Container Apps no admiten añadir VNet después. El despliegue crea `rg-daily-<env>-centralus` y nunca modifica `rg-daily-enhanced-dev-centralus` (confirmado por el what-if).
- **Separada de `azd`.** Así `azd down` no puede borrar Search, SQL ni Foundry, que comparten Copilot Studio y Power Automate.
- **Search S1 en producción.** Basic no admite un indexador privado con skillset ni vínculos privados compartidos a un recurso de Foundry (Learn, *shared private link resource limits*). En desarrollo, Basic con red pública.
- **Inyección de VNet del Agent Service.** Sin ella, la herramienta `azure_ai_search` no llega a un Search privado. Es inmutable, por eso queda detrás de `enableAgentVnetInjection` (true en producción) y el what-if se ejecuta con false.
- **`keepSearchPublic = true` en producción.** Copilot Studio solo llega a un Search privado con soporte de VNet de Power Platform (entorno administrado, VNets en eastus y westus emparejadas con centralus). Hasta validarlo no se cierra el endpoint público.
- **`disableLocalAuth = false` en los dos entornos**, solo como parámetro. La condición documentada es un inventario de consumidores sin claves. Hoy Copilot Studio usa una clave de Search y la conexión actual Foundry → Search también.
- **Conexión Foundry → Search con `authType: AAD`** desde el principio en la plataforma nueva (A1 aplicado por diseño).
- **SQL Entra-only con un grupo como administrador** (no un usuario), sin regla `0.0.0.0`. En desarrollo, oferta gratuita con `AutoPause` al agotar la cuota, para no facturar.
- **RBAC con alcance de recurso** y solo 5 asignaciones; Reader sobre el servidor SQL para Search añadido tras confirmarlo en Learn.
- **`bypass: AzureServices` en Foundry** como respaldo, porque Learn indica que en algunos escenarios la llamada de embeddings todavía depende de él.

## 8. Riesgos pendientes

- La inyección de VNet no se ha podido validar con what-if: hay que validarla con un despliegue real en una suscripción de pruebas.
- Las consultas KQL de la alerta del indexador deben validarse en desarrollo con datos reales de diagnóstico.
- Copilot Studio en VNet depende de que un administrador de Power Platform confirme la región y el tipo de entorno.
- La migración implica nueva URL e historial vacío para los usuarios.

## 9. Acciones para el equipo

[acciones-azure.md](acciones-azure.md): A9 (cuota), A10 (Foundry User para la web app), A11 (red privada), A12 (firewall de SQL) y A13 (asignaciones privilegiadas). Todas, salvo A9, requieren revisión senior.

## 10. Sin modificar a propósito

- Ningún recurso de Azure, rol, regla de red ni clave.
- `infra/main.bicep` y `infra/main-app.bicep` (IaC de la web app con `azd`). Reconciliarlos con lo desplegado (grupo `rg-daily-enhanced-dev-centralus`, identidad `id-web-daily-enhanced-dev`, `AI_AGENT_VERSION`, entorno de Container Apps con VNet) es el siguiente paso, y conviene hacerlo junto con la fase F2 de la migración. Cambiarlos ahora sin poder desplegar dejaría un IaC que no corresponde ni a lo actual ni a lo nuevo.
- La telemetría del navegador sigue con autenticación local (limitación del SDK).
