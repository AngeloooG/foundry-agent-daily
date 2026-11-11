# Plataforma compartida de Daily (Bicep)

Infraestructura como código de los componentes **compartidos** de Daily: Azure AI Search, Azure SQL, Microsoft Foundry (cuenta, proyecto, despliegues y conexión a Search), Key Vault, identidades, monitorización y alertas, y la red privada opcional.

La web app (Container App, ACR, registro de Entra) sigue en [`infra/main.bicep`](../main.bicep) con `azd`. Esta plataforma **no** forma parte de `azd`, así que `azd provision` y `azd down` no la tocan.

> No se ha desplegado nada. Cualquier despliegue requiere aprobación y **revisión senior** (red, identidad, RBAC). Antes de desplegar, lee [`docs/hardening/red-privada.md`](../../docs/hardening/red-privada.md) y [`docs/hardening/matriz-rbac.md`](../../docs/hardening/matriz-rbac.md).

## Estructura

| Archivo | Contenido |
|---|---|
| `main.bicep` | Alcance de suscripción. Crea `rg-<workload>-<env>-<region>` y orquesta los módulos. Las salidas no contienen secretos |
| `main.dev.bicepparam` | Desarrollo: red pública, Search Basic, SQL sin servidor con pausa automática (oferta gratuita, `AutoPause` al agotar la cuota) |
| `main.prod.bicepparam` | Producción: red privada, Search S1 con 2 réplicas, SQL GP_Gen5_2 aprovisionada, retención de 90 días, `keepSearchPublic = true` durante la migración de Copilot Studio |
| `modules/monitoring.bicep` | Log Analytics (límite diario) y Application Insights basado en área de trabajo |
| `modules/network.bicep` | VNet con `snet-aca` (Container Apps), `snet-pe` (private endpoints) y `snet-agent` (inyección del Agent Service), y las 6 zonas DNS privadas vinculadas |
| `modules/private-endpoint.bicep` | Private endpoint genérico con grupo de zonas DNS |
| `modules/identity.bicep` | Identidad asignada por el usuario de la web app |
| `modules/keyvault.bicep` | Key Vault con RBAC, purga protegida y diagnóstico |
| `modules/sql.bicep` | Servidor con **solo Entra** (`azureADOnlyAuthentication`), administrador = grupo de Entra, TLS 1.2, sin regla `0.0.0.0`, auditoría a Log Analytics |
| `modules/search.bicep` | Search con identidad del sistema, `aadOrApiKey` y vínculos privados compartidos a SQL y Foundry (solo en red privada) |
| `modules/foundry.bicep` | Cuenta `AIServices`, proyecto, despliegues de modelos, conexión `search-daily` con **Entra** (sin clave), inyección de VNet y capability host del proyecto (solo en red privada) |
| `modules/rbac.bicep` | 5 asignaciones de mínimo privilegio con alcance de recurso (ver la matriz RBAC) |
| `modules/alerts.bicep` | Alerta de fallos del indexador (consulta sobre `AzureDiagnostics`) y grupo de acción opcional por correo |

## Parámetros de seguridad

| Parámetro | Dev | Prod | Notas |
|---|---|---|---|
| `enablePrivateNetworking` | `false` | `true` | Private endpoints, DNS privado, vínculos privados compartidos y acceso público deshabilitado |
| `keepSearchPublic` | — | `true` | Search conserva su endpoint público mientras Copilot Studio no tenga soporte de VNet de Power Platform |
| `enableAgentVnetInjection` | — | `true` | Inmutable tras crear la cuenta. Pasar `false` **solo** para el what-if (ver abajo) |
| `disableLocalAuth` | `false` | `false` | Solo `true` cuando el inventario demuestre que ningún consumidor usa claves: hoy Copilot Studio usa una clave de Search y la conexión actual de Foundry también (A1, A6). Afecta a Search, Foundry y el componente de Application Insights del **backend**. El SDK del navegador necesita autenticación local: usar un componente de frontend aparte |

No hay secretos en los parámetros. Se leen variables de entorno (con valores de ejemplo que hay que sustituir):

| Variable | Uso |
|---|---|
| `DAILY_SQL_ADMIN_GROUP_NAME` | Nombre del grupo de Entra administrador de SQL |
| `DAILY_SQL_ADMIN_GROUP_OBJECT_ID` | Object ID de ese grupo. El valor por defecto `0000…` **no** es válido para desplegar |
| `DAILY_ALERT_EMAIL` | Correo para las alertas. Vacío = alerta sin grupo de acción |

## Compilar y validar (solo lectura)

```bash
az bicep build --file infra/platform/main.bicep --stdout > /dev/null
az bicep build-params --file infra/platform/main.dev.bicepparam --stdout > /dev/null
az bicep build-params --file infra/platform/main.prod.bicepparam --stdout > /dev/null

az deployment sub what-if --subscription <sub> --location centralus --template-file infra/platform/main.bicep --parameters infra/platform/main.dev.bicepparam

az deployment sub what-if --subscription <sub> --location centralus --template-file infra/platform/main.bicep --parameters infra/platform/main.prod.bicepparam --parameters enableAgentVnetInjection=false
```

Limitaciones conocidas del what-if:

- Con `enableAgentVnetInjection=true`, ARM devuelve `InternalServerError`: el what-if no sabe analizar `networkInjections`. Se valida con `false` y la inyección se prueba en un despliegue real de una suscripción de pruebas.
- Las 5 asignaciones de rol aparecen como *Unsupported*, porque su nombre depende de identidades que se crean en el mismo despliegue.
- En la suscripción actual, el preflight falla con `InsufficientQuota` para `gpt-5.1` GlobalStandard (uso 500/250 en centralus). Los what-if guardados se ejecutaron con `--parameters modelDeployments='[]'`. Ver la acción A9.

Las salidas guardadas, sin secretos, están en [`docs/hardening/whatif/`](../../docs/hardening/whatif/).

## Desplegar (no ejecutado; requiere aprobación)

1. Revisión senior de red, identidad y RBAC.
2. Definir las variables de entorno anteriores y confirmar la cuota de los modelos (A9).
3. Ejecutar el what-if y revisarlo.
4. Desplegar con una identidad que pueda crear el grupo de recursos y asignar los 5 roles de `rbac.bicep`:

   ```bash
   az deployment sub create --subscription <sub> --location centralus --name daily-platform-<env> --template-file infra/platform/main.bicep --parameters infra/platform/main.<env>.bicepparam
   ```

5. **Pasos del plano de datos** (no son ARM):
   1. Aprobar los vínculos privados compartidos en SQL y en Foundry (solo en producción).
   2. Crear en la base de datos el usuario de Search, conectado como administrador de Entra:

      ```sql
      CREATE USER [<nombre-del-servicio-search>] FROM EXTERNAL PROVIDER;
      GRANT SELECT ON OBJECT::dbo.vw_DailyConocimiento_Search TO [<nombre-del-servicio-search>];
      ```

   3. Recrear el origen de datos (`ResourceId` del servidor nuevo, sin contraseña), el skillset (`resourceUri` de la cuenta nueva, sin `apiKey`), el índice y el indexador con la API REST de Search (`api-version=2024-07-01`) y autenticación Entra. En producción, el indexador lleva `"executionEnvironment": "private"`.
   4. En desarrollo (Search Basic y red pública), añadir a `sqlFirewallRules` la IP de salida del servicio de Search, ejecutando el indexador en el entorno privado (`executionEnvironment: private`) y obteniendo la IP con `nslookup <search>.search.windows.net`. **Nunca** `0.0.0.0` ni `AllowAllWindowsAzureIps`.
   5. Crear el agente en el proyecto nuevo con la herramienta `azure_ai_search` sobre la conexión `search-daily`.
6. Pruebas de [`red-privada.md`](../../docs/hardening/red-privada.md), sección 6, y la [verificación manual](../../docs/hardening/verificacion-manual.md).

## Rollback

El despliegue crea un grupo de recursos nuevo y no modifica `rg-daily-enhanced-dev-centralus`. Para revertir, se deja de usar (la web app y Copilot Studio siguen apuntando a los recursos actuales) y se borra el grupo nuevo, después de comprobar que no contiene datos que conservar.

Key Vault tiene protección de purga: su nombre queda reservado durante el periodo de retención. Las cuentas de Foundry quedan en borrado temporal 48 h.

## Coste (orientativo)

- **Dev:** Search Basic (coste fijo mensual), SQL gratuita con pausa automática, Log Analytics con límite de 1 GB/día y consumo de los modelos.
- **Prod:** Search S1 × 2, SQL GP_Gen5_2, 4 private endpoints, 6 zonas DNS y consumo de los modelos.

Detalle en [`red-privada.md`](../../docs/hardening/red-privada.md), sección 4. Verificar en la calculadora de precios antes de aprobar.
