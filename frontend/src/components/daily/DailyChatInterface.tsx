import { useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';
import type { IChatItem, IFileAttachment } from '../../types/chat';
import type { AppState } from '../../types/appState';
import type { AppError } from '../../types/errors';
import { DailyLogo } from './DailyLogo';
import styles from './DailyChatInterface.module.css';

interface DailyChatInterfaceProps {
  messages: IChatItem[];
  status: AppState['chat']['status'];
  error: AppError | null;
  streamingMessageId?: string;
  recoveredInput?: string;
  recoveredAttachments?: IFileAttachment[];
  pendingMessages?: Array<{ text: string; files?: File[] }>;
  currentConversationId?: string | null;
  conversations?: Array<{ id: string; title?: string | null; createdAt?: string | number | null }>;
  sidebarOpen?: boolean;
  agentName?: string;
  agentDescription?: string;
  agentLogo?: string;
  starterPrompts?: string[];
  isEditing?: boolean;
  onSendMessage: (text: string, files?: File[]) => void;
  onClearError?: () => void;
  onNewChat?: () => void;
  onCancelStream?: () => void;
  onRecoveredInputConsumed?: () => void;
  onRegenerate?: () => void;
  onEditMessage?: (messageId: string, newText: string) => void;
  onCancelEdit?: () => void;
  onFeedback?: (messageId: string, rating: 'positive' | 'negative') => void;
  onDownloadFile?: (fileId: string, fileName: string, containerId?: string) => void;
  onExportConversation?: () => void;
  onLoadConversation?: (conversationId: string) => void;
}

const DEFAULT_PROMPTS = [
  '¿Quién ha trabajado con Azure AI Search?',
  '¿Qué aprendimos en Copilot Studio?',
  'Soluciones con Power Automate',
  '¿Casos sobre migración a Azure SQL?',
  '¿Agentes internos en CONSEIN?',
  '¿Errores en índices semánticos?',
];

export function DailyChatInterface({
  messages,
  status,
  error,
  streamingMessageId,
  recoveredInput,
  pendingMessages = [],
  conversations = [],
  currentConversationId,
  agentName = 'Daily',
  agentDescription = 'Agente de conocimiento empresarial',
  starterPrompts,
  isEditing,
  onSendMessage,
  onClearError,
  onNewChat,
  onCancelStream,
  onRecoveredInputConsumed,
  onRegenerate,
  onEditMessage,
  onCancelEdit,
  onFeedback,
  onExportConversation,
  onLoadConversation,
}: DailyChatInterfaceProps) {
  const [input, setInput] = useState(recoveredInput || '');
  const [files, setFiles] = useState<File[]>([]);
  const bottomRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const isStreaming = status === 'streaming';
  const isSending = status === 'sending';
  const isBusy = isStreaming || isSending;
  const prompts = starterPrompts?.length ? starterPrompts : DEFAULT_PROMPTS;

  useEffect(() => {
    if (recoveredInput) {
      setInput(recoveredInput);
      onRecoveredInputConsumed?.();
    }
  }, [recoveredInput, onRecoveredInputConsumed]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [messages, status]);

  const submit = (text?: string) => {
    const value = (text ?? input).trim();

    if (!value || isSending) return;

    onSendMessage(value, files.length > 0 ? files : undefined);
    setInput('');
    setFiles([]);
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  };

  const handleFiles = (fileList: FileList | null) => {
    if (!fileList) return;
    setFiles((current) => [...current, ...Array.from(fileList)]);
  };

  const removeFile = (index: number) => {
    setFiles((current) => current.filter((_, i) => i !== index));
  };

  return (
    <div className={styles.shell}>
      <aside className={styles.sidebar}>
        <div className={styles.brand}>
          <div className={styles.logoBox}>
            <DailyLogo size={20} />
          </div>

          <div>
            <div className={styles.brandTitle}>{agentName}</div>
            <div className={styles.brandSubtitle}>CONSEIN · Agente</div>
          </div>
        </div>

        <button type="button" className={styles.newChatButton} onClick={onNewChat}>
          Nuevo chat
        </button>

        <div className={styles.sidebarSectionTitle}>Conversaciones</div>

        <div className={styles.conversationList}>
          {conversations.length === 0 ? (
            <div className={styles.emptyConversations}>Sin conversaciones cargadas</div>
          ) : (
            conversations.map((conversation) => {
              const isActive = conversation.id === currentConversationId;

              return (
                <button
                  key={conversation.id}
                  type="button"
                  className={`${styles.conversationItem} ${isActive ? styles.conversationItemActive : ''}`}
                  onClick={() => onLoadConversation?.(conversation.id)}
                >
                  <span>{conversation.title || 'Conversación'}</span>
                </button>
              );
            })
          )}
        </div>

        <div className={styles.sidebarFooter}>
          <button type="button" className={styles.sidebarAction} onClick={onExportConversation}>
            Exportar Markdown
          </button>
        </div>
      </aside>

      <main className={styles.main}>
        <header className={styles.topbar}>
          <div>
            <div className={styles.kicker}>Agente de conocimiento empresarial</div>
            <h1>{agentName}</h1>
            <p>{agentDescription}</p>
          </div>

          <div className={styles.statusPill}>
            <span className={styles.statusDot} />
            {isStreaming ? 'respondiendo' : 'online'}
          </div>
        </header>

        <section className={styles.messagesArea} aria-label="Chat de Daily">
          {messages.length === 0 ? (
            <div className={styles.welcome}>
              <div className={styles.welcomeLogo}>
                <DailyLogo size={30} />
              </div>

              <h2>Hola, soy {agentName}</h2>

              <p>
                Puedes preguntarme por experiencias, especialistas, proyectos, soluciones,
                tecnologías o aprendizajes documentados en CONSEIN.
              </p>

              <div className={styles.promptGrid}>
                {prompts.map((prompt, index) => (
                  <button
                    key={`${prompt}-${index}`}
                    type="button"
                    className={styles.promptChip}
                    onClick={() => submit(prompt)}
                    disabled={isBusy}
                  >
                    {prompt}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <div className={styles.thread}>
              {messages.map((message) => {
                const isUser = message.role === 'user';
                const isCurrentStreaming = isStreaming && message.id === streamingMessageId;

                return (
                  <article
                    key={message.id}
                    className={`${styles.messageRow} ${isUser ? styles.userRow : styles.assistantRow}`}
                  >
                    {!isUser && (
                      <div className={styles.avatar}>
                        <DailyLogo size={13} />
                      </div>
                    )}

                    <div className={`${styles.bubble} ${isUser ? styles.userBubble : styles.assistantBubble}`}>
                      {!isUser && (
                        <div className={styles.assistantMeta}>
                          <span>{agentName}</span>
                          {isCurrentStreaming && <span>generando...</span>}
                        </div>
                      )}

                      <div className={styles.markdown}>
                        <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSanitize]}>
                          {message.content || ''}
                        </ReactMarkdown>
                      </div>

                      {!isUser && !isCurrentStreaming && (
                        <div className={styles.messageActions}>
                          <button type="button" onClick={onRegenerate}>
                            Regenerar
                          </button>

                          <button type="button" onClick={() => onFeedback?.(message.id, 'positive')}>
                            Útil
                          </button>

                          <button type="button" onClick={() => onFeedback?.(message.id, 'negative')}>
                            No útil
                          </button>
                        </div>
                      )}

                      {isUser && onEditMessage && (
                        <div className={styles.messageActions}>
                          <button type="button" onClick={() => onEditMessage(message.id, message.content)}>
                            Editar
                          </button>
                        </div>
                      )}
                    </div>
                  </article>
                );
              })}

              {pendingMessages.length > 0 && (
                <div className={styles.pendingBox}>
                  {pendingMessages.length} mensaje(s) en cola. Se enviarán al terminar la respuesta actual.
                </div>
              )}

              <div ref={bottomRef} />
            </div>
          )}
        </section>

        {error && (
          <div className={styles.errorBar}>
            <span>
              {typeof error.message === 'string'
                ? error.message
                : error.originalError?.message || 'Ocurrió un error inesperado.'}
            </span>

            <button type="button" onClick={onClearError}>
              Cerrar
            </button>
          </div>
        )}

        {isEditing && (
          <div className={styles.editBar}>
            <span>Modo edición activo</span>
            <button type="button" onClick={onCancelEdit}>
              Cancelar
            </button>
          </div>
        )}

        <footer className={styles.inputArea}>
          {files.length > 0 && (
            <div className={styles.fileList}>
              {files.map((file, index) => (
                <button
                  key={`${file.name}-${index}`}
                  type="button"
                  className={styles.fileChip}
                  onClick={() => removeFile(index)}
                  title="Quitar archivo"
                >
                  {file.name} ×
                </button>
              ))}
            </div>
          )}

          <div className={styles.inputBox}>
            <button
              type="button"
              className={styles.iconButton}
              onClick={() => fileInputRef.current?.click()}
              disabled={isBusy}
              aria-label="Adjuntar archivo"
            >
              +
            </button>

            <textarea
              value={input}
              onChange={(event) => setInput(event.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Pregunta sobre una solución, tecnología, especialista, proyecto o problema similar..."
              rows={1}
              disabled={isSending}
            />

            {isStreaming ? (
              <button type="button" className={styles.sendButton} onClick={onCancelStream}>
                Detener
              </button>
            ) : (
              <button
                type="button"
                className={styles.sendButton}
                onClick={() => submit()}
                disabled={!input.trim() || isSending}
              >
                Enviar
              </button>
            )}

            <input
              ref={fileInputRef}
              type="file"
              multiple
              hidden
              onChange={(event) => handleFiles(event.target.files)}
            />
          </div>
        </footer>
      </main>
    </div>
  );
}