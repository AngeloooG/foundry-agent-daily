export type DailyPage = 'home' | 'consult' | 'technology' | 'faq';
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
    whatIs: {
      title: '¿Qué es Daily?',
      subtitle:
        'Un agente de consulta inteligente que accede al conocimiento reutilizable de CONSEIN. Relaciona preguntas actuales con experiencias, proyectos, soluciones y lecciones aprendidas previamente documentadas.',
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
      title: 'Cómo funciona',
      steps: [
        {
          n: '01',
          title: 'Haz una pregunta',
          desc: 'El usuario escribe una consulta en lenguaje natural.',
        },
        {
          n: '02',
          title: 'Daily interpreta',
          desc: 'Identifica intención, tecnología, contexto y posibles similitudes.',
        },
        {
          n: '03',
          title: 'Consulta el índice',
          desc: 'Búsqueda semántica, vectorial, híbrida y por palabras clave.',
        },
        {
          n: '04',
          title: 'Relaciona casos',
          desc: 'Encuentra proyectos, tecnologías, lecciones y especialistas.',
        },
        {
          n: '05',
          title: 'Entrega la respuesta',
          desc: 'Resume hallazgos, recomienda próximos pasos y muestra fuentes.',
        },
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
          role: 'Especialistas',
          tone: 'blue',
          items: [
            'Encuentran soluciones similares más rápido.',
            'Consultan experiencias sin depender de memoria individual.',
            'Identifican errores, riesgos y recomendaciones previas.',
          ],
        },
        {
          role: 'Ventas y preventa',
          tone: 'green',
          items: [
            'Encuentran referencias internas para propuestas.',
            'Identifican capacidades demostrables al cliente.',
            'Preparan conversaciones con mayor contexto.',
          ],
        },
        {
          role: 'Gerencia',
          tone: 'cyan',
          items: [
            'Visualizan el valor del conocimiento acumulado.',
            'Reducen dependencia de personas específicas.',
            'Aumentan reutilización de soluciones probadas.',
          ],
        },
        {
          role: 'CONSEIN',
          tone: 'deep',
          items: [
            'Convierte conocimiento disperso en memoria consultable.',
            'Mejora la transferencia de experiencia entre equipos.',
            'Acelera resolución de problemas similares.',
          ],
        },
      ],
    },
    tech: {
      title: 'Tecnología detrás de Daily',
      subtitle:
        'Daily combina conocimiento estructurado, búsqueda semántica, búsqueda vectorial y agentes de IA para recuperar experiencias relevantes en segundos.',
      flowTitle: 'Flujo técnico',
      techTitle: 'Stack tecnológico',
      summary: [
        {
          label: 'Microsoft Azure',
          desc: 'Plataforma principal de nube e IA.',
          tone: 'blue',
        },
        {
          label: 'Power Platform',
          desc: 'Automatización y flujos de trabajo.',
          tone: 'green',
        },
        {
          label: 'CONSEIN Kiara',
          desc: 'Agente de captura y formalización.',
          tone: 'cyan',
        },
      ],
    },
    faq: {
      title: 'Preguntas frecuentes',
      subtitle: 'Todo lo que necesitas saber sobre Daily.',
      items: [
        {
          q: '¿Daily reemplaza a los especialistas?',
          a: 'No. Daily ayuda a consultar conocimiento documentado y sugerir experiencias relacionadas, pero no reemplaza el criterio experto ni la toma de decisiones humana.',
        },
        {
          q: '¿Daily captura conocimiento nuevo?',
          a: 'No en este MVP. Daily consulta conocimiento previamente registrado y publicado por Kiara. La captura es responsabilidad de Kiara, no de Daily.',
        },
        {
          q: '¿Cuál es la diferencia entre Kiara y Daily?',
          a: 'Kiara captura, formaliza y segmenta experiencias de proyectos. Daily consulta, relaciona y reutiliza ese conocimiento para responder preguntas actuales.',
        },
        {
          q: '¿Daily usa datos reales?',
          a: 'La demo puede usar datos ficticios, controlados o información preparada. Los clientes, proyectos y especialistas mostrados deben validarse antes de una demostración productiva.',
        },
        {
          q: '¿Daily puede recomendar especialistas?',
          a: 'Sí. Daily puede sugerir perfiles o especialistas relacionados con una tecnología, proyecto o aprendizaje detectado en la consulta.',
        },
        {
          q: '¿Daily muestra fuentes de la información?',
          a: 'Sí. La interfaz puede mostrar fuentes como documentos en SharePoint, vistas en Azure SQL o casos relacionados en Azure AI Search.',
        },
        {
          q: '¿Daily tiene login o acceso por roles?',
          a: 'La aplicación base puede usar Microsoft Entra ID. Para una demo abierta o MVP comercial, el acceso por roles puede configurarse según el escenario.',
        },
        {
          q: '¿Daily permite editar o eliminar casos?',
          a: 'No. Daily está diseñado como agente de consulta. La edición, eliminación o publicación del conocimiento debe realizarse desde los flujos autorizados.',
        },
      ],
    },
  },
  en: {
    nav: {
      home: 'Home',
      consult: 'Ask Daily',
      technology: 'Technology',
      faq: 'FAQ',
    },
    hero: {
      tag: 'Enterprise knowledge agent',
      headlineLine1: 'Daily:',
      headlineLine2: "CONSEIN's digital brain",
      headlineLine3: '',
      subtitle:
        'Query specialists, projects, solutions and past learnings to solve current problems faster, with more context and confidence.',
      cta: 'Ask Daily',
      ctaSecondary: 'See how it works',
    },
    whatIs: {
      title: 'What is Daily?',
      subtitle:
        'An intelligent query agent that accesses reusable CONSEIN knowledge and connects current questions with previously documented experiences.',
      cards: [
        {
          title: 'Query knowledge',
          desc: 'Ask in natural language and receive actionable answers drawn from real experiences.',
          tone: 'blue',
        },
        {
          title: 'Find similar experiences',
          desc: 'Connect current problems with solutions already implemented in past projects.',
          tone: 'green',
        },
        {
          title: 'Connect with specialists',
          desc: 'Identify people, technologies and projects related to your current query.',
          tone: 'cyan',
        },
      ],
    },
    flow: {
      title: 'From documented experience to actionable answer',
      steps: [
        {
          label: 'Kiara captures',
          tag: 'KIARA',
          desc: 'Stories, problems, solutions, metrics and learnings.',
        },
        {
          label: 'CONSEIN validates',
          tag: 'CONSEIN',
          desc: 'Knowledge is reviewed, enriched and published.',
        },
        {
          label: 'Daily queries',
          tag: 'DAILY',
          desc: 'The agent retrieves relevant knowledge for new questions.',
        },
      ],
      tag: 'Kiara preserves experience. Daily turns it into useful answers.',
    },
    howWorks: {
      title: 'How it works',
      steps: [
        {
          n: '01',
          title: 'Ask a question',
          desc: 'The user types a query in natural language.',
        },
        {
          n: '02',
          title: 'Daily interprets',
          desc: 'Identifies intent, technology, context and possible similarities.',
        },
        {
          n: '03',
          title: 'Queries the index',
          desc: 'Semantic, vector, hybrid and keyword search.',
        },
        {
          n: '04',
          title: 'Relates cases',
          desc: 'Finds projects, technologies, lessons and specialists.',
        },
        {
          n: '05',
          title: 'Delivers the answer',
          desc: 'Summarizes findings, recommends next steps and shows sources.',
        },
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
        'What errors should we avoid creating a semantic index?',
        'What experiences exist about data governance?',
        'Which projects used SharePoint and Power Automate?',
      ],
    },
    benefits: {
      title: 'An agent for every role',
      groups: [
        {
          role: 'Specialists',
          tone: 'blue',
          items: [
            'Find similar solutions faster.',
            'Query past experiences without relying on individual memory.',
            'Identify prior errors, risks and recommendations.',
          ],
        },
        {
          role: 'Sales & Presales',
          tone: 'green',
          items: [
            'Find internal references for proposals.',
            'Identify demonstrable capabilities to clients.',
            'Prepare conversations with greater context.',
          ],
        },
        {
          role: 'Management',
          tone: 'cyan',
          items: [
            'Visualize the value of accumulated knowledge.',
            'Reduce dependency on specific individuals.',
            'Increase reuse of proven solutions.',
          ],
        },
        {
          role: 'CONSEIN',
          tone: 'deep',
          items: [
            'Converts scattered knowledge into queryable memory.',
            'Improves experience transfer between teams.',
            'Accelerates resolution of similar problems.',
          ],
        },
      ],
    },
    tech: {
      title: 'Technology behind Daily',
      subtitle:
        'Daily combines structured knowledge, semantic search, vector search and AI agents to retrieve relevant experiences in seconds.',
      flowTitle: 'Technical flow',
      techTitle: 'Technology stack',
      summary: [
        {
          label: 'Microsoft Azure',
          desc: 'Core cloud and AI platform.',
          tone: 'blue',
        },
        {
          label: 'Power Platform',
          desc: 'Automation and workflow.',
          tone: 'green',
        },
        {
          label: 'CONSEIN Kiara',
          desc: 'Capture and formalization agent.',
          tone: 'cyan',
        },
      ],
    },
    faq: {
      title: 'Frequently asked questions',
      subtitle: 'Everything you need to know about Daily.',
      items: [
        {
          q: 'Does Daily replace specialists?',
          a: 'No. Daily helps query documented knowledge and suggest related experiences, but does not replace expert judgment or human decision-making.',
        },
        {
          q: 'Does Daily capture new knowledge?',
          a: 'Not in this MVP. Daily queries knowledge previously registered and published by Kiara.',
        },
        {
          q: "What's the difference between Kiara and Daily?",
          a: 'Kiara captures, formalizes and segments project experiences. Daily queries, connects and reuses that knowledge.',
        },
        {
          q: 'Does Daily use real data?',
          a: 'The demo may use fictional, controlled or prepared information. Details should be validated before production use.',
        },
        {
          q: 'Can Daily recommend specialists?',
          a: 'Yes. Daily can suggest profiles or specialists related to a technology, project or learning detected in the query.',
        },
        {
          q: 'Does Daily show information sources?',
          a: 'Yes. The interface can show sources such as SharePoint documents, Azure SQL views or related cases in Azure AI Search.',
        },
        {
          q: 'Does Daily have login or role-based access?',
          a: 'The base application can use Microsoft Entra ID. Role-based access can be configured depending on the scenario.',
        },
        {
          q: 'Can Daily edit or delete cases?',
          a: 'No. Daily is designed as a query agent. Editing, deletion or publication should be handled by authorized workflows.',
        },
      ],
    },
  },
} as const;