# Daily | Memoria empresarial inteligente de CONSEIN

> **Daily transforma conocimiento validado de proyectos, especialistas y soluciones en respuestas empresariales accionables.**

Daily es un agente de consulta inteligente diseñado para recuperar y relacionar conocimiento reutilizable de CONSEIN. Permite formular preguntas en lenguaje natural y obtener respuestas sustentadas en experiencias previamente capturadas, revisadas y publicadas.

El MVP se enfoca en conocimiento de consultoría, proyectos y tecnologías Microsoft. Sin embargo, su arquitectura está concebida para ampliarse a otros dominios empresariales, como ventas, preventa, administración, operaciones, soporte, recursos humanos o gestión documental.

## Contenido

- [Objetivo](#objetivo)
- [Problema que resuelve](#problema-que-resuelve)
- [¿Qué puede consultar Daily?](#qué-puede-consultar-daily)
- [Relación entre Kiara y Daily](#relación-entre-kiara-y-daily)
- [Flujo funcional](#flujo-funcional)
- [Capacidades principales](#capacidades-principales)
- [Arquitectura conceptual](#arquitectura-conceptual)
- [Tecnologías](#tecnologías)
- [Experiencia web](#experiencia-web)
- [Escalabilidad por dominio](#escalabilidad-por-dominio)
- [Seguridad y gobierno](#seguridad-y-gobierno)
- [Ejecución y configuración](#ejecución-y-configuración)
- [Estado del MVP](#estado-del-mvp)
- [Documentación comercial](#documentación-comercial)

## Objetivo

Daily busca convertir la experiencia acumulada de la organización en una memoria empresarial consultable. Su propósito no es reemplazar a los especialistas, sino ayudarlos a encontrar antecedentes, personas, tecnologías, decisiones y aprendizajes relevantes antes de resolver un problema nuevo.

## Problema que resuelve

El conocimiento de una empresa suele quedar fragmentado entre documentos, conversaciones, repositorios, sitios de SharePoint y la memoria individual de sus colaboradores. Esto genera:

- Dependencia de personas específicas.
- Repetición de errores ya conocidos.
- Dificultad para localizar especialistas y experiencias similares.
- Tiempo perdido buscando documentos o reconstruyendo decisiones anteriores.
- Baja reutilización de soluciones y activos existentes.
- Pérdida de contexto cuando cambian los equipos.

Daily centraliza la consulta de ese conocimiento y lo presenta de forma contextual, trazable y útil para la toma de decisiones.

## ¿Qué puede consultar Daily?

Entre otras preguntas, Daily puede ayudar a responder:

- ¿Quién ha trabajado con Azure AI Search?
- ¿Qué aprendimos en proyectos de Copilot Studio?
- ¿Qué soluciones reutilizables existen con Power Automate?
- ¿Qué casos existen sobre migración a Azure SQL?
- ¿Qué especialista conoce implementaciones de agentes internos?
- ¿Qué errores debemos evitar al crear un índice semántico?
- ¿Qué experiencias existen sobre gobierno de datos?
- ¿Qué proyectos utilizaron SharePoint y Power Automate?

La respuesta puede relacionar proyectos, tecnologías, especialistas, problemas, soluciones, riesgos, métricas y lecciones aprendidas. Cuando las fuentes están disponibles, la interfaz puede mostrarlas para que el usuario valide el contexto original.

## Relación entre Kiara y Daily

Daily forma parte de un ciclo de gestión del conocimiento en el que cada agente tiene una responsabilidad clara:

1. **Kiara captura:** recopila relatos, problemas, soluciones, métricas y aprendizajes de los proyectos.
2. **CONSEIN valida:** revisa, enriquece y autoriza la publicación del conocimiento.
3. **Daily consulta:** recupera, relaciona y presenta conocimiento relevante para nuevas preguntas.

> Kiara preserva la experiencia. Daily la convierte en respuestas útiles.

Daily no debe asumir funciones de captura, edición, eliminación o publicación. Estas operaciones pertenecen a procesos autorizados y separados del agente de consulta.

## Flujo funcional

1. El usuario realiza una pregunta en lenguaje natural.
2. Daily interpreta la intención, el dominio, las tecnologías y el contexto.
3. El agente consulta los orígenes e índices autorizados.
4. La capa de recuperación combina coincidencias semánticas, vectoriales, híbridas y por palabras clave.
5. Daily relaciona casos, proyectos, especialistas y aprendizajes.
6. El agente sintetiza una respuesta accionable.
7. La interfaz presenta recomendaciones y fuentes disponibles.

## Capacidades principales

### Consulta conversacional

Permite consultar conocimiento empresarial sin depender de rutas de navegación rígidas ni de conocer previamente el nombre de un archivo, proyecto o especialista.

### Recuperación de experiencias similares

Relaciona un problema actual con proyectos o situaciones previamente documentadas, incluso cuando la redacción usada en la consulta no coincide exactamente con la fuente.

### Localización de especialistas

Identifica perfiles vinculados con tecnologías, proyectos, industrias o aprendizajes relevantes. La recomendación debe entenderse como una referencia basada en conocimiento publicado, no como una asignación automática de responsabilidad.

### Respuestas con trazabilidad

Puede exponer fuentes relacionadas, como elementos de conocimiento publicados, documentos de SharePoint, registros estructurados o resultados indexados en Azure AI Search.

### Recomendaciones accionables

Además de resumir antecedentes, Daily puede proponer siguientes pasos, riesgos que revisar y preguntas que deben validarse con un especialista.

### Experiencia bilingüe

La interfaz contempla navegación y contenido en español e inglés mediante recursos de traducción tipados.

## Arquitectura conceptual

```text
Especialistas y proyectos
          |
          v
    Kiara captura
          |
          v
Revisión y publicación autorizada
          |
          +-------------------+
          |                   |
          v                   v
      Azure SQL          SharePoint / Word
  conocimiento estructurado   documentos y evidencias
          |                   |
          +---------+---------+
                    |
                    v
             Azure AI Search
   búsqueda semántica, vectorial, híbrida y textual
                    |
                    v
       Agente de IA en Microsoft Foundry
                    |
                    v
        API de aplicación y experiencia web
                    |
                    v
     Respuesta, recomendaciones y fuentes
```

La implementación concreta puede variar según el entorno, los permisos y la configuración de los servicios. El diagrama representa la separación lógica de responsabilidades del producto.

## Tecnologías

### Microsoft Foundry

Proporciona la capacidad de razonamiento y orquestación del agente. Daily utiliza instrucciones, modelos y herramientas para interpretar preguntas, consultar conocimiento autorizado y construir respuestas contextualizadas.

### Azure AI Search

Funciona como capa de recuperación de conocimiento. Permite combinar:

- Búsqueda de texto completo.
- Búsqueda vectorial por similitud conceptual.
- Búsqueda híbrida.
- Clasificación semántica, cuando esté configurada.
- Filtros por metadatos, dominio, tecnología, proyecto o estado de publicación.

### Azure SQL

Almacena conocimiento estructurado y relaciones empresariales, por ejemplo:

- Casos o experiencias.
- Proyectos.
- Especialistas.
- Tecnologías.
- Problemas y soluciones.
- Métricas y aprendizajes.
- Estados editoriales y referencias de publicación.

### SharePoint

Puede actuar como capa documental y editorial para archivos, evidencias, plantillas, revisión y publicación controlada. SharePoint no sustituye necesariamente al índice de consulta ni al modelo relacional, sino que participa como origen documental y espacio de colaboración.

### Microsoft Word

Permite trabajar con documentos empresariales estandarizados, plantillas y activos documentales que pueden almacenarse en SharePoint y formar parte del ciclo de conocimiento autorizado.

### Power Automate y Power Platform

Automatizan procesos como aprobación, publicación, sincronización, movimiento de archivos, generación documental y comunicación entre las capas editoriales y los servicios de datos.

### Aplicación web

La rama `Develop-Daily` incorpora una experiencia web orientada a presentar el producto y consultar el agente. El frontend contiene páginas para:

- Inicio.
- Consulta de Daily.
- Tecnología.
- Preguntas frecuentes.

También incluye contenido localizado en español e inglés.

## Experiencia web

La propuesta de interfaz comunica cinco ideas principales:

1. Daily es el cerebro digital de CONSEIN.
2. El conocimiento proviene de experiencias previamente documentadas.
3. La información pasa por un proceso de validación antes de reutilizarse.
4. El agente ayuda a especialistas, ventas, preventa y gerencia.
5. Las respuestas deben conservar contexto y referencias verificables.

## Escalabilidad por dominio

Daily no está limitado al conocimiento técnico de consultores. El mismo patrón puede configurarse para otros dominios, siempre que cada uno cuente con fuentes, metadatos, permisos, reglas de recuperación e instrucciones propias.

### Ejemplos

- **Ventas:** propuestas anteriores, objeciones, referencias, industrias, productos y casos de éxito.
- **Preventa:** arquitecturas, estimaciones, restricciones, riesgos y activos reutilizables.
- **Administración:** políticas, procedimientos, solicitudes y preguntas operativas frecuentes.
- **Operaciones:** incidentes, resoluciones, manuales y recomendaciones preventivas.
- **Recursos humanos:** procesos internos, capacitación, roles y conocimiento organizacional autorizado.
- **Soporte:** problemas conocidos, diagnósticos, soluciones y escalamiento.

La extensión a nuevos dominios no debe resolverse mezclando indiscriminadamente todo el contenido. Debe aplicarse segmentación lógica, filtros, permisos y gobierno por audiencia.

## Seguridad y gobierno

Daily debe operar bajo los siguientes principios:

- **Acceso mínimo necesario:** cada usuario consulta únicamente contenido autorizado.
- **Identidad empresarial:** Microsoft Entra ID puede utilizarse para autenticación y control de acceso.
- **Separación de responsabilidades:** Daily consulta; los flujos autorizados capturan, editan, aprueban y publican.
- **Contenido validado:** el índice productivo debe priorizar información revisada y publicada.
- **Trazabilidad:** las respuestas deben conservar referencias a sus fuentes cuando sea posible.
- **Protección de datos:** no se deben exponer secretos, credenciales ni información sensible en el frontend, logs o respuestas.
- **Gobierno por dominio:** cada expansión debe definir propietarios, políticas de retención, metadatos y reglas de acceso.
- **Supervisión humana:** las respuestas apoyan decisiones, pero no sustituyen el criterio profesional.

## Ejecución y configuración

> Los nombres exactos de variables y comandos deben validarse contra la configuración vigente de la rama `Develop-Daily`. No almacenes secretos reales en el repositorio.

### Requisitos generales

- Git.
- Node.js y el administrador de paquetes definido por el frontend.
- SDK de .NET requerido por el backend.
- Azure CLI.
- Azure Developer CLI, si el despliegue utiliza `azd`.
- Acceso autorizado a los recursos de Microsoft Foundry y Azure.

### Flujo recomendado para desarrollo local

```bash
git clone https://github.com/AngeloooG/FoundryAgents-webapp-mvp.git
cd FoundryAgents-webapp-mvp
git checkout Develop-Daily
```

Después de clonar:

1. Revisa los archivos de configuración de ejemplo del repositorio.
2. Configura los identificadores y endpoints requeridos mediante variables de entorno o secretos locales.
3. Instala las dependencias del frontend y backend.
4. Inicia ambos componentes con los scripts definidos por el proyecto.
5. Verifica la autenticación y la conexión con el agente antes de probar consultas.

Si el repositorio conserva el flujo de Azure Developer CLI, utiliza la documentación y scripts incluidos como fuente de verdad para los comandos de aprovisionamiento y despliegue.

## Estado del MVP

El alcance comunicado por la interfaz contempla:

- Consulta de conocimiento previamente registrado y publicado.
- Relación de experiencias, proyectos, tecnologías y especialistas.
- Presentación de respuestas y fuentes.
- Interfaz bilingüe.
- Posibilidad de autenticación empresarial y control por roles según el escenario.

### Fuera del alcance de Daily

- Capturar conocimiento nuevo durante la consulta.
- Editar o eliminar casos.
- Publicar contenido sin validación.
- Sustituir la decisión de un especialista.
- Garantizar que los datos de demostración sean productivos o estén actualizados.

La demo puede utilizar datos ficticios, controlados o preparados. Antes de una implementación productiva deben validarse la calidad de los datos, los permisos, la seguridad, las fuentes y las reglas de recuperación.

## Documentación comercial

Consulta [`VENTAS.md`](./VENTAS.md) para revisar la propuesta de valor, los beneficios por rol, los casos de uso y el enfoque de adopción empresarial.
