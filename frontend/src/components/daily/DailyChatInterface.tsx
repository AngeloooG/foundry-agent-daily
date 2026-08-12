import {
  useEffect,
  useRef,
  useState,
  type KeyboardEvent,
} from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';

import type { DailyTranslation } from '../../config/dailyTranslations';
import type { IChatItem, IFileAttachment } from '../../types/chat';
import type { AppState } from '../../types/appState';
import type { AppError } from '../../types/errors';
import { DailyLogo } from './DailyLogo';
import styles from './DailyChatInterface.module.css';

interface DailyChatInterfaceProps {
  t: DailyTranslation;
  messages: IChatItem[];
  status: AppState['chat']['status'];
  error: AppError | null;
  streamingMessageId?: string;
  recoveredInput?: string;
  recoveredAttachments?: IFileAttachment[];
  pendingMessages?: Array<{ text: string; files?: File[] }>;
  agentName?: string;
  agentDescription?: string;
  starterPrompts?: string[];
  isEditing?: boolean;
  initialDraft?: string;
  onSendMessage: (text: string, files?: File[]) => void;
  onClearError?: () => void;
  onCancelStream?: () => void;
  onRecoveredInputConsumed?: () => void;
  onInitialDraftConsumed?: () => void;
  onRegenerate?: () => void;
  onEditMessage?: (messageId: string, newText: string) => void;
  onCancelEdit?: () => void;
  onFeedback?: (
    messageId: string,
    rating: 'positive' | 'negative'
  ) => void;
}

export function DailyChatInterface({
  t,
  messages,
  status,
  error,
  streamingMessageId,
  recoveredInput,
  recoveredAttachments = [],
  pendingMessages = [],
  agentName = 'Daily',
  agentDescription,
  starterPrompts,
  isEditing,
  initialDraft,
  onSendMessage,
  onClearError,
  onCancelStream,
  onRecoveredInputConsumed,
  onInitialDraftConsumed,
  onRegenerate,
  onEditMessage,
  onCancelEdit,
  onFeedback,
}: DailyChatInterfaceProps) {
  const [input, setInput] = useState(recoveredInput || '');
  const [files, setFiles] = useState<File[]>([]);

  const bottomRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const isStreaming = status === 'streaming';
  const isSending = status === 'sending';
  const isBusy = isStreaming || isSending;

  const prompts = starterPrompts?.length
    ? starterPrompts
    : t.chat.defaultPrompts;

  const visibleAgentDescription =
    agentDescription || t.chat.defaultAgentDescription;

  const selectPrompt = (prompt: string) => {
    setInput(prompt);

    window.requestAnimationFrame(() => {
      textareaRef.current?.focus();
      textareaRef.current?.setSelectionRange(
        prompt.length,
        prompt.length
      );
    });
  };

  useEffect(() => {
    if (!recoveredInput) {
      return;
    }

    setInput(recoveredInput);
    onRecoveredInputConsumed?.();
  }, [recoveredInput, onRecoveredInputConsumed]);

  useEffect(() => {
    if (recoveredAttachments.length === 0) {
      return;
    }

    // Recovered attachments are already represented by the application state.
    // File objects cannot be reconstructed safely from attachment metadata.
    textareaRef.current?.focus();
  }, [recoveredAttachments]);

  useEffect(() => {
    const draft = initialDraft?.trim();

    if (!draft) {
      return;
    }

    setInput(draft);
    onInitialDraftConsumed?.();

    window.requestAnimationFrame(() => {
      textareaRef.current?.focus();
      textareaRef.current?.setSelectionRange(
        draft.length,
        draft.length
      );
    });
  }, [initialDraft, onInitialDraftConsumed]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({
      behavior: 'smooth',
      block: 'end',
    });
  }, [messages, status]);

  const submit = () => {
    const value = input.trim();

    if (!value || isBusy) {
      return;
    }

    onSendMessage(
      value,
      files.length > 0 ? files : undefined
    );

    setInput('');
    setFiles([]);
  };

  const handleKeyDown = (
    event: KeyboardEvent<HTMLTextAreaElement>
  ) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  };

  const handleFiles = (fileList: FileList | null) => {
    if (!fileList) {
      return;
    }

    setFiles((current) => [
      ...current,
      ...Array.from(fileList),
    ]);
  };

  const removeFile = (index: number) => {
    setFiles((current) =>
      current.filter((_, currentIndex) => currentIndex !== index)
    );
  };

  return (
    <div className={styles.shell}>
      <main className={styles.main}>
        <header className={styles.topbar}>
          <div>
            <div className={styles.kicker}>{t.chat.kicker}</div>
            <h1>{agentName}</h1>
            <p>{visibleAgentDescription}</p>
          </div>

          <div className={styles.statusPill}>
            <span className={styles.statusDot} />
            {isStreaming
              ? t.chat.status.responding
              : t.chat.status.online}
          </div>
        </header>

        <section
          className={styles.messagesArea}
          aria-label={t.chat.ariaLabel}
        >
          {messages.length === 0 ? (
            <div className={styles.welcome}>
              <div className={styles.welcomeLogo}>
                <DailyLogo size={30} />
              </div>

              <h2>
                {t.chat.welcome.title} {agentName}
              </h2>

              <p>{t.chat.welcome.description}</p>

              <div className={styles.promptGrid}>
                {prompts.map((prompt, index) => (
                  <button
                    key={`${prompt}-${index}`}
                    type="button"
                    className={styles.promptChip}
                    onClick={() => selectPrompt(prompt)}
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
                const isCurrentStreaming =
                  isStreaming &&
                  message.id === streamingMessageId;

                return (
                  <article
                    key={message.id}
                    className={`${styles.messageRow} ${isUser
                        ? styles.userRow
                        : styles.assistantRow
                      }`}
                  >
                    {!isUser && (
                      <div className={styles.avatar}>
                        <DailyLogo size={13} />
                      </div>
                    )}

                    <div
                      className={`${styles.bubble} ${isUser
                          ? styles.userBubble
                          : styles.assistantBubble
                        }`}
                    >
                      {!isUser && (
                        <div className={styles.assistantMeta}>
                          <span>{agentName}</span>
                          {isCurrentStreaming && (
                            <span>
                              {t.chat.status.generating}
                            </span>
                          )}
                        </div>
                      )}

                      <div className={styles.markdown}>
                        <ReactMarkdown
                          remarkPlugins={[remarkGfm]}
                          rehypePlugins={[rehypeSanitize]}
                        >
                          {message.content || ''}
                        </ReactMarkdown>
                      </div>

                      {!isUser && !isCurrentStreaming && (
                        <div className={styles.messageActions}>
                          <button
                            type="button"
                            onClick={onRegenerate}
                          >
                            {t.chat.actions.regenerate}
                          </button>

                          <button
                            type="button"
                            onClick={() =>
                              onFeedback?.(
                                message.id,
                                'positive'
                              )
                            }
                          >
                            {t.chat.actions.helpful}
                          </button>

                          <button
                            type="button"
                            onClick={() =>
                              onFeedback?.(
                                message.id,
                                'negative'
                              )
                            }
                          >
                            {t.chat.actions.notHelpful}
                          </button>
                        </div>
                      )}

                      {isUser && onEditMessage && (
                        <div className={styles.messageActions}>
                          <button
                            type="button"
                            onClick={() =>
                              onEditMessage(
                                message.id,
                                message.content
                              )
                            }
                          >
                            {t.chat.actions.edit}
                          </button>
                        </div>
                      )}
                    </div>
                  </article>
                );
              })}

              {pendingMessages.length > 0 && (
                <div className={styles.pendingBox}>
                  {pendingMessages.length}{' '}
                  {t.chat.queueSuffix}
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
                : error.originalError?.message ||
                t.chat.fallbackError}
            </span>

            <button
              type="button"
              onClick={onClearError}
            >
              {t.chat.actions.close}
            </button>
          </div>
        )}

        {isEditing && (
          <div className={styles.editBar}>
            <span>{t.chat.editingMode}</span>
            <button
              type="button"
              onClick={onCancelEdit}
            >
              {t.chat.actions.cancel}
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
                  title={t.chat.actions.removeFile}
                  aria-label={`${t.chat.actions.removeFile}: ${file.name}`}
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
              aria-label={t.chat.actions.attach}
            >
              +
            </button>

            <textarea
              ref={textareaRef}
              value={input}
              onChange={(event) => setInput(event.target.value)}
              onKeyDown={handleKeyDown}
              placeholder={t.chat.placeholder}
              rows={1}
              disabled={isBusy}
            />

            {isStreaming ? (
              <button
                type="button"
                className={styles.sendButton}
                onClick={onCancelStream}
              >
                {t.chat.actions.stop}
              </button>
            ) : (
              <button
                type="button"
                className={styles.sendButton}
                onClick={submit}
                disabled={!input.trim() || isSending}
              >
                {t.chat.actions.send}
              </button>
            )}

            <input
              ref={fileInputRef}
              type="file"
              multiple
              hidden
              onChange={(event) =>
                handleFiles(event.target.files)
              }
            />
          </div>
        </footer>
      </main>
    </div>
  );
}
