export type DailyLang = 'es' | 'en';

export const dailyTranslations = {
  es: {
    nav: {
      home: 'Inicio',
      consult: 'Consultar Daily',
      technology: 'Tecnología',
      faq: 'FAQ',
    },
    hero: {
      tag: 'Agente de conocimiento empresarial',
      headlineLine1: 'Daily:',
      headlineLine2: 'el cerebro digital',
      headlineLine3: 'de CONSEIN',
      subtitle:
        'Consulta especialistas, proyectos, soluciones y aprendizajes pasados para resolver problemas actuales con mayor velocidad, contexto y confianza.',
      cta: 'Consultar Daily',
      ctaSecondary: 'Ver cómo funciona',
    },
    home: {
      preview: {
        agentStatus: 'online · CONSEIN',
        userQuestion: '¿Quién ha trabajado con Azure AI Search?',
        assistantAnswer:
          'Encontré 3 proyectos y 2 especialistas con experiencia documentada.',
        projects: [
          { name: 'Proyecto Atlas', relevance: 'Alta' },
          { name: 'Proyecto Meridian', relevance: 'Alta' },
        ],
        placeholder: 'Pregunta sobre un proyecto o especialista...',
        openChatLabel: 'Consultar Daily',
      },
      finalCta: {
        title: 'Consulta la memoria de CONSEIN',
        description:
          'Daily convierte la experiencia acumulada en respuestas accionables.',
        button: 'Consultar Daily',
      },
    },
    whatIs: {
      title: '¿Quién es Daily?',
      subtitle:
        'Daily es un agente de consulta inteligente que accede al conocimiento reutilizable de CONSEIN. Relaciona preguntas actuales con experiencias, proyectos, soluciones y lecciones aprendidas previamente documentadas.',
      cards: [
        {
          title: 'Consulta conocimiento',
          desc: 'Pregunta en lenguaje natural y recibe respuestas accionables extraídas de experiencias reales.',
          tone: 'blue',
        },
        {
          title: 'Encuentra experiencias similares',
          desc: 'Relaciona problemas actuales con soluciones ya implementadas en proyectos anteriores.',
          tone: 'green',
        },
        {
          title: 'Conecta con especialistas',
          desc: 'Identifica personas, tecnologías y proyectos relacionados con tu consulta actual.',
          tone: 'cyan',
        },
      ],
    },
    flow: {
      title: 'De experiencia documentada a respuesta accionable',
      steps: [
        {
          label: 'Kiara captura',
          tag: 'KIARA',
          desc: 'Relatos, problemas, soluciones, métricas y aprendizajes.',
        },
        {
          label: 'CONSEIN valida',
          tag: 'CONSEIN',
          desc: 'El conocimiento se revisa, enriquece y publica.',
        },
        {
          label: 'Daily consulta',
          tag: 'DAILY',
          desc: 'El agente recupera conocimiento relevante para nuevas preguntas.',
        },
      ],
      tag: 'Kiara preserva la experiencia. Daily la convierte en respuestas útiles.',
    },
    howWorks: {
      title: '¿Cómo funciona?',
      steps: [
        { n: '01', title: 'Haz una pregunta', desc: 'El usuario escribe una consulta en lenguaje natural.' },
        { n: '02', title: 'Daily interpreta', desc: 'Identifica intención, tecnología, contexto y posibles similitudes.' },
        { n: '03', title: 'Consulta el índice', desc: 'Búsqueda semántica, vectorial, híbrida y por palabras clave.' },
        { n: '04', title: 'Relaciona casos', desc: 'Encuentra proyectos, tecnologías, lecciones y especialistas.' },
        { n: '05', title: 'Entrega la respuesta', desc: 'Resume hallazgos, recomienda próximos pasos y muestra fuentes.' },
      ],
    },
    sampleQuestions: {
      title: 'Preguntas que Daily puede responder',
      questions: [
        '¿Quién ha trabajado con Azure AI Search?',
        '¿Qué aprendimos en proyectos de Copilot Studio?',
        '¿Qué soluciones reutilizables existen con Power Automate?',
        '¿Qué casos existen sobre migración a Azure SQL?',
        '¿Qué especialista conoce implementaciones de agentes internos?',
        '¿Qué errores debemos evitar al crear un índice semántico?',
        '¿Qué experiencias existen sobre gobierno de datos?',
        '¿Qué proyectos usaron SharePoint y Power Automate?',
      ],
    },
    benefits: {
      title: 'Un agente para cada rol',
      groups: [
        {
          role: 'Especialistas', tone: 'blue',
          items: ['Encuentran soluciones similares más rápido.', 'Consultan experiencias sin depender de memoria individual.', 'Identifican errores, riesgos y recomendaciones previas.'],
        },
        {
          role: 'Ventas y preventa', tone: 'green',
          items: ['Encuentran referencias internas para propuestas.', 'Identifican capacidades demostrables al cliente.', 'Preparan conversaciones con mayor contexto.'],
        },
        {
          role: 'Gerencia', tone: 'cyan',
          items: ['Visualizan el valor del conocimiento acumulado.', 'Reducen dependencia de personas específicas.', 'Aumentan reutilización de soluciones probadas.'],
        },
        {
          role: 'CONSEIN', tone: 'deep',
          items: ['Convierte conocimiento disperso en memoria consultable.', 'Mejora la transferencia de experiencia entre equipos.', 'Acelera resolución de problemas similares.'],
        },
      ],
    },
    tech: {
      architectureLabel: 'ARQUITECTURA',
      title: 'Tecnología detrás de Daily',
      subtitle:
        'Daily combina conocimiento estructurado, búsqueda semántica, búsqueda vectorial y agentes de IA para recuperar experiencias relevantes en segundos.',
      flowTitle: 'Flujo técnico',
      techTitle: 'Stack tecnológico',
      flowSteps: [
        { n: '01', label: 'KIARA', title: 'Kiara captura', desc: 'Relatos, soluciones, aprendizajes y métricas de proyectos.', tone: 'green' },
        { n: '02', label: 'PIPELINE', title: 'Revisión y publicación', desc: 'Los documentos pasan de borrador a publicado tras validación.', tone: 'blue' },
        { n: '03', label: 'PIPELINE', title: 'Power Automate procesa', desc: 'Lleva la información validada a almacenamiento estructurado.', tone: 'blue' },
        { n: '04', label: 'PIPELINE', title: 'Azure SQL organiza', desc: 'Vistas relacionan especialistas, proyectos, tecnologías y aprendizajes.', tone: 'blue' },
        { n: '05', label: 'PIPELINE', title: 'Azure AI Search indexa', desc: 'Indexadores, chunking, embeddings y búsqueda semántica/vectorial.', tone: 'blue' },
        { n: '06', label: 'DAILY', title: 'Daily consulta', desc: 'El usuario pregunta en lenguaje natural a través de la interfaz.', tone: 'cyan' },
        { n: '07', label: 'DAILY', title: 'Daily responde', desc: 'Casos, especialistas, tecnologías, lecciones y recomendaciones.', tone: 'cyan' },
      ],
      stack: [
        { name: 'Azure AI Search', desc: 'Recuperación inteligente de conocimiento indexado.', tone: 'blue' },
        { name: 'Azure SQL', desc: 'Almacenamiento estructurado de vistas y relaciones.', tone: 'blue' },
        { name: 'Embeddings', desc: 'Representación semántica para encontrar similitudes.', tone: 'green' },
        { name: 'Chunking', desc: 'División del contenido para mejorar precisión de consulta.', tone: 'green' },
        { name: 'Semantic Search', desc: 'Búsqueda basada en significado, no solo en palabras.', tone: 'cyan' },
        { name: 'Vector Search', desc: 'Recuperación de documentos por similitud vectorial.', tone: 'cyan' },
        { name: 'Hybrid Search', desc: 'Combina búsqueda clásica y vectorial para mayor precisión.', tone: 'cyan' },
        { name: 'Power Automate', desc: 'Automatización de publicación y procesamiento documental.', tone: 'deep' },
        { name: 'SharePoint', desc: 'Origen documental y almacenamiento de casos formalizados.', tone: 'blue' },
        { name: 'Azure AI Foundry', desc: 'Plataforma de orquestación de agentes e IA empresarial.', tone: 'blue' },
        { name: 'GPT / Azure OpenAI', desc: 'Modelo de lenguaje para generación de respuestas.', tone: 'blue' },
        { name: 'Copilot Studio', desc: 'Orquestación de flujos conversacionales y agentes.', tone: 'blue' },
        { name: 'MCP', desc: 'Protocolo de contexto para agentes interconectados.', tone: 'blue' },
        { name: 'Microsoft 365', desc: 'Ecosistema de productividad e integración documental.', tone: 'blue' },
      ],
      summary: [
        { label: 'Microsoft Azure', desc: 'Plataforma principal de nube e IA.', tone: 'blue' },
        { label: 'Power Platform', desc: 'Automatización y flujos de trabajo.', tone: 'green' },
        { label: 'CONSEIN Kiara', desc: 'Agente de captura y formalización.', tone: 'cyan' },
      ],
    },
    faq: {
      title: 'Preguntas frecuentes',
      subtitle: 'Todo lo que necesitas saber sobre Daily.',
      items: [
        { q: '¿Daily reemplaza a los especialistas?', a: 'No. Daily ayuda a consultar conocimiento documentado y sugerir experiencias relacionadas, pero no reemplaza el criterio experto ni la toma de decisiones humana.' },
        { q: '¿Daily captura conocimiento nuevo?', a: 'No en este MVP. Daily consulta conocimiento previamente registrado y publicado por Kiara. La captura es responsabilidad de Kiara, no de Daily.' },
        { q: '¿Cuál es la diferencia entre Kiara y Daily?', a: 'Kiara captura, formaliza y segmenta experiencias de proyectos. Daily consulta, relaciona y reutiliza ese conocimiento para responder preguntas actuales.' },
        { q: '¿Daily usa datos reales?', a: 'La demo puede usar datos ficticios, controlados o información preparada. Los clientes, proyectos y especialistas mostrados deben validarse antes de una demostración productiva.' },
        { q: '¿Daily puede recomendar especialistas?', a: 'Sí. Daily puede sugerir perfiles o especialistas relacionados con una tecnología, proyecto o aprendizaje detectado en la consulta.' },
        { q: '¿Daily muestra fuentes de la información?', a: 'Sí. La interfaz puede mostrar fuentes como documentos en SharePoint, vistas en Azure SQL o casos relacionados en Azure AI Search.' },
        { q: '¿Daily tiene login o acceso por roles?', a: 'La aplicación base puede usar Microsoft Entra ID. Para una demo abierta o MVP comercial, el acceso por roles puede configurarse según el escenario.' },
        { q: '¿Daily permite editar o eliminar casos?', a: 'No. Daily está diseñado como agente de consulta. La edición, eliminación o publicación del conocimiento debe realizarse desde los flujos autorizados.' },
      ],
      cta: {
        title: '¿Listo para probar Daily?',
        description: 'Consulta experiencias y proyectos de CONSEIN en lenguaje natural.',
        button: 'Consultar Daily',
      },
    },
    chat: {
      kicker: 'Agente de conocimiento empresarial',
      ariaLabel: 'Chat de Daily',
      status: { online: 'online', responding: 'respondiendo', generating: 'generando...' },
      welcome: {
        title: 'Hola, soy',
        description: 'Puedes preguntarme por experiencias, especialistas, proyectos, soluciones, tecnologías o aprendizajes documentados en CONSEIN.',
      },
      defaultAgentDescription: 'Agente de conocimiento empresarial',
      defaultPrompts: [
        '¿Quién ha trabajado con Azure AI Search?',
        '¿Qué aprendimos en Copilot Studio?',
        'Soluciones con Power Automate',
        '¿Casos sobre migración a Azure SQL?',
        '¿Agentes internos en CONSEIN?',
        '¿Errores en índices semánticos?',
      ],
      actions: {
        regenerate: 'Regenerar', helpful: 'Útil', notHelpful: 'No útil', edit: 'Editar', close: 'Cerrar', cancel: 'Cancelar', stop: 'Detener', send: 'Enviar', attach: 'Adjuntar archivo', removeFile: 'Quitar archivo',
      },
      queueSuffix: 'mensaje(s) en cola. Se enviarán al terminar la respuesta actual.',
      fallbackError: 'Ocurrió un error inesperado.',
      editingMode: 'Modo edición activo',
      placeholder: 'Pregunta sobre una solución, tecnología, especialista, proyecto o problema similar...',
    },
  },
  en: {
    nav: { home: 'Home', consult: 'Ask Daily', technology: 'Technology', faq: 'FAQ' },
    hero: {
      tag: 'Enterprise knowledge agent',
      headlineLine1: 'Daily:',
      headlineLine2: "CONSEIN's digital brain",
      headlineLine3: '',
      subtitle: 'Query specialists, projects, solutions and past learnings to solve current problems faster, with more context and confidence.',
      cta: 'Ask Daily',
      ctaSecondary: 'See how it works',
    },
    home: {
      preview: {
        agentStatus: 'online · CONSEIN',
        userQuestion: 'Who has worked with Azure AI Search?',
        assistantAnswer: 'I found 3 projects and 2 specialists with documented experience.',
        projects: [
          { name: 'Project Atlas', relevance: 'High' },
          { name: 'Project Meridian', relevance: 'High' },
        ],
        placeholder: 'Ask about a project or specialist...',
        openChatLabel: 'Ask Daily',
      },
      finalCta: {
        title: "Query CONSEIN's knowledge",
        description: 'Daily turns accumulated experience into actionable answers.',
        button: 'Ask Daily',
      },
    },
    whatIs: {
      title: 'Who is Daily?',
      subtitle: 'Daily is an intelligent query agent that accesses reusable CONSEIN knowledge and connects current questions with previously documented experiences, projects, solutions and lessons learned.',
      cards: [
        { title: 'Query knowledge', desc: 'Ask in natural language and receive actionable answers drawn from real experiences.', tone: 'blue' },
        { title: 'Find similar experiences', desc: 'Connect current problems with solutions already implemented in past projects.', tone: 'green' },
        { title: 'Connect with specialists', desc: 'Identify people, technologies and projects related to your current query.', tone: 'cyan' },
      ],
    },
    flow: {
      title: 'From documented experience to actionable answers',
      steps: [
        { label: 'Kiara captures', tag: 'KIARA', desc: 'Stories, problems, solutions, metrics and learnings.' },
        { label: 'CONSEIN validates', tag: 'CONSEIN', desc: 'Knowledge is reviewed, enriched and published.' },
        { label: 'Daily queries', tag: 'DAILY', desc: 'The agent retrieves relevant knowledge for new questions.' },
      ],
      tag: 'Kiara preserves experience. Daily turns it into useful answers.',
    },
    howWorks: {
      title: 'How does it work?',
      steps: [
        { n: '01', title: 'Ask a question', desc: 'The user types a query in natural language.' },
        { n: '02', title: 'Daily interprets', desc: 'Identifies intent, technology, context and possible similarities.' },
        { n: '03', title: 'Queries the index', desc: 'Semantic, vector, hybrid and keyword search.' },
        { n: '04', title: 'Relates cases', desc: 'Finds projects, technologies, lessons and specialists.' },
        { n: '05', title: 'Delivers the answer', desc: 'Summarizes findings, recommends next steps and shows sources.' },
      ],
    },
    sampleQuestions: {
      title: 'Questions Daily can answer',
      questions: [
        'Who has worked with Azure AI Search?',
        'What did we learn in Copilot Studio projects?',
        'What reusable solutions exist with Power Automate?',
        'What cases exist on Azure SQL migration?',
        'Which specialist knows internal agent implementations?',
        'What errors should we avoid when creating a semantic index?',
        'What experiences exist around data governance?',
        'Which projects used SharePoint and Power Automate?',
      ],
    },
    benefits: {
      title: 'An agent for every role',
      groups: [
        { role: 'Specialists', tone: 'blue', items: ['Find similar solutions faster.', 'Query past experiences without relying on individual memory.', 'Identify prior errors, risks and recommendations.'] },
        { role: 'Sales & Presales', tone: 'green', items: ['Find internal references for proposals.', 'Identify capabilities that can be demonstrated to clients.', 'Prepare conversations with greater context.'] },
        { role: 'Management', tone: 'cyan', items: ['Visualize the value of accumulated knowledge.', 'Reduce dependency on specific individuals.', 'Increase reuse of proven solutions.'] },
        { role: 'CONSEIN', tone: 'deep', items: ['Turns scattered knowledge into queryable memory.', 'Improves experience transfer between teams.', 'Accelerates resolution of similar problems.'] },
      ],
    },
    tech: {
      architectureLabel: 'ARCHITECTURE',
      title: 'Technology behind Daily',
      subtitle: 'Daily combines structured knowledge, semantic search, vector search and AI agents to retrieve relevant experiences in seconds.',
      flowTitle: 'Technical flow',
      techTitle: 'Technology stack',
      flowSteps: [
        { n: '01', label: 'KIARA', title: 'Kiara captures', desc: 'Project stories, solutions, learnings and metrics.', tone: 'green' },
        { n: '02', label: 'PIPELINE', title: 'Review and publication', desc: 'Documents move from draft to published after validation.', tone: 'blue' },
        { n: '03', label: 'PIPELINE', title: 'Power Automate processes', desc: 'Moves validated information into structured storage.', tone: 'blue' },
        { n: '04', label: 'PIPELINE', title: 'Azure SQL organizes', desc: 'Views connect specialists, projects, technologies and learnings.', tone: 'blue' },
        { n: '05', label: 'PIPELINE', title: 'Azure AI Search indexes', desc: 'Indexers, chunking, embeddings and semantic/vector search.', tone: 'blue' },
        { n: '06', label: 'DAILY', title: 'Daily queries', desc: 'The user asks questions in natural language through the interface.', tone: 'cyan' },
        { n: '07', label: 'DAILY', title: 'Daily responds', desc: 'Cases, specialists, technologies, lessons and recommendations.', tone: 'cyan' },
      ],
      stack: [
        { name: 'Azure AI Search', desc: 'Intelligent retrieval of indexed knowledge.', tone: 'blue' },
        { name: 'Azure SQL', desc: 'Structured storage for views and relationships.', tone: 'blue' },
        { name: 'Embeddings', desc: 'Semantic representations used to find similarities.', tone: 'green' },
        { name: 'Chunking', desc: 'Content segmentation that improves query precision.', tone: 'green' },
        { name: 'Semantic Search', desc: 'Meaning-based search instead of keyword-only matching.', tone: 'cyan' },
        { name: 'Vector Search', desc: 'Document retrieval through vector similarity.', tone: 'cyan' },
        { name: 'Hybrid Search', desc: 'Combines traditional and vector search for greater precision.', tone: 'cyan' },
        { name: 'Power Automate', desc: 'Automation for publication and document processing.', tone: 'deep' },
        { name: 'SharePoint', desc: 'Document source and storage for formalized cases.', tone: 'blue' },
        { name: 'Azure AI Foundry', desc: 'Platform for orchestrating enterprise AI agents.', tone: 'blue' },
        { name: 'GPT / Azure OpenAI', desc: 'Language model used to generate responses.', tone: 'blue' },
        { name: 'Copilot Studio', desc: 'Orchestration of conversational flows and agents.', tone: 'blue' },
        { name: 'MCP', desc: 'Context protocol for interconnected agents.', tone: 'blue' },
        { name: 'Microsoft 365', desc: 'Productivity ecosystem and document integration.', tone: 'blue' },
      ],
      summary: [
        { label: 'Microsoft Azure', desc: 'Core cloud and AI platform.', tone: 'blue' },
        { label: 'Power Platform', desc: 'Automation and workflow platform.', tone: 'green' },
        { label: 'CONSEIN Kiara', desc: 'Knowledge capture and formalization agent.', tone: 'cyan' },
      ],
    },
    faq: {
      title: 'Frequently asked questions',
      subtitle: 'Everything you need to know about Daily.',
      items: [
        { q: 'Does Daily replace specialists?', a: 'No. Daily helps query documented knowledge and suggest related experiences, but does not replace expert judgment or human decision-making.' },
        { q: 'Does Daily capture new knowledge?', a: 'Not in this MVP. Daily queries knowledge previously registered and published by Kiara. Kiara is responsible for capturing knowledge.' },
        { q: "What is the difference between Kiara and Daily?", a: 'Kiara captures, formalizes and segments project experiences. Daily queries, connects and reuses that knowledge to answer current questions.' },
        { q: 'Does Daily use real data?', a: 'The demo may use fictional, controlled or prepared information. Client, project and specialist details must be validated before a production demonstration.' },
        { q: 'Can Daily recommend specialists?', a: 'Yes. Daily can suggest profiles or specialists related to a technology, project or learning detected in the query.' },
        { q: 'Does Daily show information sources?', a: 'Yes. The interface can show sources such as SharePoint documents, Azure SQL views or related cases in Azure AI Search.' },
        { q: 'Does Daily support login or role-based access?', a: 'The base application can use Microsoft Entra ID. Role-based access can be configured according to the scenario.' },
        { q: 'Can Daily edit or delete cases?', a: 'No. Daily is designed as a query agent. Editing, deletion and publication must be handled through authorized workflows.' },
      ],
      cta: {
        title: 'Ready to try Daily?',
        description: 'Query CONSEIN experiences and projects in natural language.',
        button: 'Ask Daily',
      },
    },
    chat: {
      kicker: 'Enterprise knowledge agent',
      ariaLabel: 'Daily chat',
      status: { online: 'online', responding: 'responding', generating: 'generating...' },
      welcome: {
        title: "Hello, I'm",
        description: 'Ask me about experiences, specialists, projects, solutions, technologies or learnings documented at CONSEIN.',
      },
      defaultAgentDescription: 'Enterprise knowledge agent',
      defaultPrompts: [
        'Who has worked with Azure AI Search?',
        'What did we learn from Copilot Studio?',
        'Solutions built with Power Automate',
        'Cases involving Azure SQL migration',
        'Internal agents at CONSEIN',
        'Errors in semantic indexes',
      ],
      actions: {
        regenerate: 'Regenerate', helpful: 'Helpful', notHelpful: 'Not helpful', edit: 'Edit', close: 'Close', cancel: 'Cancel', stop: 'Stop', send: 'Send', attach: 'Attach file', removeFile: 'Remove file',
      },
      queueSuffix: 'queued message(s). They will be sent when the current response finishes.',
      fallbackError: 'An unexpected error occurred.',
      editingMode: 'Editing mode active',
      placeholder: 'Ask about a solution, technology, specialist, project or similar problem...',
    },
  },
} as const;

export type DailyTranslations = typeof dailyTranslations;
export type DailyTranslation = DailyTranslations[DailyLang];
