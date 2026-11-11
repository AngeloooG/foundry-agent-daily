import type { Dispatch } from 'react';
import type { AppAction } from '../types/appState';
import type { ConversationSummary, ConversationMessageInfo } from '../types/appState';
import type { IChatItem } from '../types/chat';
import type { AppError } from '../types/errors';
import { isAppError } from '../types/errors';
import { trackException } from './telemetry';
import {
  createAppError,
  createAppErrorFromResponse,
  getErrorCodeFromMessage,
} from '../utils/errorHandler';
import {
  convertFilesToDataUris,
  createAttachmentMetadata,
} from '../utils/fileAttachments';
import { parseSseLine, splitSseBuffer } from '../utils/sseParser';

type FileDataUri = { dataUri: string; fileName: string; mimeType: string };

/** Mutable progress of one stream attempt, shared with the retry policy. */
interface StreamProgress {
  /** Conversation id known so far (sent by the server in the first SSE event). */
  conversationId: string | null;
  /** True once the server accepted the request and opened the SSE stream. */
  started: boolean;
}

/** Statuses that mean "the request did not reach the agent"; safe to retry. */
const RETRYABLE_STATUSES = new Set([502, 503, 504]);
const MAX_ATTEMPTS = 3;
const RETRY_BASE_DELAY_MS = 1000;

function newId(): string {
  return typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

/**
 * ChatService handles all chat-related API operations and dispatches AppContext actions.
 *
 * One instance lives for the whole signed-in session (see ChatServiceProvider), so the active
 * stream can always be cancelled. `sendMessage` never throws: failures are reported through
 * state (CHAT_RECOVER_MESSAGE restores the user's text so nothing typed is lost).
 *
 * Retry policy (idempotency): a message is retried only when the request failed before the server
 * opened the stream (network error or 502/503/504). Once the stream started, the agent has
 * received the message, so retrying would duplicate it in the conversation.
 */
export class ChatService {
  private apiUrl: string;
  private getAccessToken: () => Promise<string | null>;
  private dispatch: Dispatch<AppAction>;
  private currentStreamAbort?: AbortController;

  constructor(
    apiUrl: string,
    getAccessToken: () => Promise<string | null>,
    dispatch: Dispatch<AppAction>
  ) {
    this.apiUrl = apiUrl;
    this.getAccessToken = getAccessToken;
    this.dispatch = dispatch;
  }

  /**
   * Acquire an access token (silent, then interactive — see useAuth).
   * @throws {AppError} AUTH when no token can be obtained
   */
  private async ensureAuthToken(): Promise<string> {
    const token = await this.getAccessToken();
    if (!token) {
      throw createAppError(new Error('Failed to acquire access token'), 'AUTH');
    }
    return token;
  }

  /**
   * Authenticated fetch against the API. Non-OK responses become AppErrors (status preserved).
   * @throws {AppError}
   */
  private async apiFetch(path: string, init: RequestInit = {}, signal?: AbortSignal): Promise<Response> {
    const token = await this.ensureAuthToken();
    const response = await fetch(`${this.apiUrl}${path}`, {
      ...init,
      signal,
      headers: {
        ...(init.headers ?? {}),
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw await createAppErrorFromResponse(response);
    }
    return response;
  }

  /**
   * Convert attached files to data URIs and separate images from documents.
   * @throws {AppError} VALIDATION when a file is not supported
   */
  private async prepareMessagePayload(files?: File[]): Promise<{
    imageDataUris: string[];
    fileDataUris: FileDataUri[];
    attachments: IChatItem['attachments'];
  }> {
    if (!files || files.length === 0) {
      return { imageDataUris: [], fileDataUris: [], attachments: undefined };
    }

    try {
      const results = await convertFilesToDataUris(files);
      return {
        imageDataUris: results.filter((r) => r.mimeType.startsWith('image/')).map((r) => r.dataUri),
        fileDataUris: results
          .filter((r) => !r.mimeType.startsWith('image/'))
          .map((r) => ({ dataUri: r.dataUri, fileName: r.name, mimeType: r.mimeType })),
        attachments: createAttachmentMetadata(results),
      };
    } catch (error) {
      const appError = createAppError(error, 'VALIDATION');
      if (error instanceof Error && error.message) {
        appError.message = error.message;
      }
      throw appError;
    }
  }

  private constructRequestBody(
    message: string,
    conversationId: string | null,
    imageDataUris: string[],
    fileDataUris: FileDataUri[]
  ): Record<string, unknown> {
    return {
      message,
      conversationId,
      imageDataUris: imageDataUris.length > 0 ? imageDataUris : undefined,
      fileDataUris: fileDataUris.length > 0 ? fileDataUris : undefined,
    };
  }

  private async initiateStream(body: Record<string, unknown>, signal: AbortSignal): Promise<Response> {
    return this.apiFetch(
      '/chat/stream',
      { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) },
      signal
    );
  }

  /** Retry only failures that happened before the server accepted the message. */
  private isRetryable(error: unknown, progress: StreamProgress): boolean {
    if (progress.started) {
      return false;
    }
    if (isAppError(error)) {
      return error.status !== undefined && RETRYABLE_STATUSES.has(error.status);
    }
    // fetch() rejects with TypeError on network failures (request never got a response).
    return error instanceof TypeError;
  }

  private toAppError(error: unknown): AppError {
    return isAppError(error) ? error : createAppError(error, getErrorCodeFromMessage(error));
  }

  /**
   * Send a message and stream the agent's response.
   * Never throws: errors are dispatched to state and the user's text is restored.
   *
   * @param messageText - The user's message text
   * @param currentConversationId - Current conversation ID (null for a new conversation)
   * @param files - Optional attachments (images and documents)
   */
  async sendMessage(
    messageText: string,
    currentConversationId: string | null,
    files?: File[]
  ): Promise<void> {
    // Only one stream at a time.
    this.cancelStream();

    // 1. Prepare everything that can fail before touching the conversation UI.
    let payload: Awaited<ReturnType<ChatService['prepareMessagePayload']>>;
    try {
      payload = await this.prepareMessagePayload(files);
      await this.ensureAuthToken();
    } catch (error) {
      const appError = this.toAppError(error);
      if (appError.code === 'AUTH') {
        this.dispatch({ type: 'AUTH_TOKEN_EXPIRED' });
      }
      this.dispatch({ type: 'CHAT_RECOVER_MESSAGE', messageText, error: appError, retryCount: 1, messageIds: [] });
      return;
    }

    // 2. Optimistic UI: user message + empty assistant message.
    const userMessageId = newId();
    const assistantMessageId = newId();
    this.dispatch({
      type: 'CHAT_SEND_MESSAGE',
      message: {
        id: userMessageId,
        role: 'user',
        content: messageText,
        attachments: payload.attachments,
        more: { time: new Date().toISOString() },
      },
    });
    this.dispatch({ type: 'CHAT_ADD_ASSISTANT_MESSAGE', messageId: assistantMessageId });
    this.dispatch({
      type: 'CHAT_START_STREAM',
      conversationId: currentConversationId || undefined,
      messageId: assistantMessageId,
    });

    // 3. Stream, retrying only while it is safe to do so.
    const progress: StreamProgress = { conversationId: currentConversationId, started: false };
    let lastError: unknown;
    let attempt = 0;

    while (attempt < MAX_ATTEMPTS) {
      attempt++;
      const abort = new AbortController();
      this.currentStreamAbort = abort;

      try {
        if (attempt > 1) {
          this.dispatch({ type: 'CHAT_STREAM_RETRY', messageId: assistantMessageId, attempt, maxRetries: MAX_ATTEMPTS });
          await new Promise((resolve) => setTimeout(resolve, RETRY_BASE_DELAY_MS * 2 ** (attempt - 2)));
          if (abort.signal.aborted) return;
        }

        progress.started = false;
        const body = this.constructRequestBody(messageText, progress.conversationId, payload.imageDataUris, payload.fileDataUris);
        const response = await this.initiateStream(body, abort.signal);
        progress.started = true;

        await this.processStream(response, assistantMessageId, progress, abort.signal);
        return;
      } catch (error) {
        if (abort.signal.aborted || isAbortError(error)) {
          return; // user cancelled; CHAT_CANCEL_STREAM already dispatched
        }
        lastError = error;
        if (!this.isRetryable(error, progress)) {
          break;
        }
      } finally {
        if (this.currentStreamAbort === abort) {
          this.currentStreamAbort = undefined;
        }
      }
    }

    const appError = this.toAppError(lastError);
    trackException(appError, { context: 'sendMessage', attempts: String(attempt) });
    if (appError.code === 'AUTH') {
      this.dispatch({ type: 'AUTH_TOKEN_EXPIRED' });
    }
    this.dispatch({
      type: 'CHAT_RECOVER_MESSAGE',
      messageText,
      error: appError,
      retryCount: attempt,
      messageIds: [userMessageId, assistantMessageId],
    });
  }

  /**
   * Process the Server-Sent Events stream.
   * Resolves on `done`; throws on an `error` event or if the stream ends without `done`
   * (otherwise the UI would stay in "responding" forever).
   */
  private async processStream(
    response: Response,
    messageId: string,
    progress: StreamProgress,
    signal: AbortSignal
  ): Promise<void> {
    const reader = response.body?.getReader();
    if (!reader) {
      throw createAppError(new Error('Response body is not readable'), 'STREAM');
    }

    const decoder = new TextDecoder();
    let lastChunkContent: string | undefined;
    let buffer = '';
    let completed = false;

    try {
      while (!signal.aborted) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const [lines, remaining] = splitSseBuffer(buffer);
        buffer = remaining;

        for (const line of lines) {
          const event = parseSseLine(line);
          if (!event) continue;

          switch (event.type) {
            case 'conversationId':
              if (event.data.conversationId && event.data.conversationId !== progress.conversationId) {
                progress.conversationId = event.data.conversationId;
                this.dispatch({ type: 'CHAT_START_STREAM', conversationId: event.data.conversationId, messageId });
              }
              break;

            case 'chunk':
              if (event.data.content !== lastChunkContent) {
                this.dispatch({ type: 'CHAT_STREAM_CHUNK', messageId, content: event.data.content });
                lastChunkContent = event.data.content;
              }
              break;

            case 'annotations':
              if (event.data.annotations?.length > 0) {
                this.dispatch({ type: 'CHAT_STREAM_ANNOTATIONS', messageId, annotations: event.data.annotations });
              }
              break;

            case 'toolUse':
              if (event.data.toolName) {
                this.dispatch({ type: 'CHAT_STREAM_TOOL_USE', messageId, toolName: event.data.toolName });
              }
              break;

            case 'mcpApprovalRequest':
              if (event.data.approvalRequest) {
                this.dispatch({
                  type: 'CHAT_MCP_APPROVAL_REQUEST',
                  messageId,
                  approvalRequest: event.data.approvalRequest,
                  previousResponseId: event.data.approvalRequest.previousResponseId ?? '',
                });
              }
              break;

            case 'usage':
              completed = true;
              this.dispatch({
                type: 'CHAT_STREAM_COMPLETE',
                usage: {
                  promptTokens: event.data.promptTokens,
                  completionTokens: event.data.completionTokens,
                  totalTokens: event.data.totalTokens,
                  duration: event.data.duration,
                },
              });
              break;

            case 'done':
              if (!completed) {
                this.dispatch({
                  type: 'CHAT_STREAM_COMPLETE',
                  usage: { promptTokens: 0, completionTokens: 0, totalTokens: 0, duration: 0 },
                });
              }
              return;

            case 'error': {
              // The server already sanitizes this message for production.
              const error = createAppError(new Error('Stream error event'), 'STREAM');
              if (typeof event.data.message === 'string' && event.data.message) {
                error.message = event.data.message;
              }
              throw error;
            }
          }
        }
      }

      if (!signal.aborted) {
        throw createAppError(new Error('Stream ended before completion'), 'STREAM');
      }
    } finally {
      try {
        reader.releaseLock();
      } catch {
        // Reader may already be released
      }
    }
  }

  /**
   * Send the user's decision for an MCP tool call. Not retried: an approval is not idempotent.
   */
  async sendMcpApproval(
    approvalRequestId: string,
    approved: boolean,
    previousResponseId: string,
    conversationId: string
  ): Promise<void> {
    this.cancelStream();
    const assistantMessageId = newId();
    const abort = new AbortController();
    this.currentStreamAbort = abort;

    try {
      this.dispatch({ type: 'CHAT_ADD_ASSISTANT_MESSAGE', messageId: assistantMessageId });
      this.dispatch({ type: 'CHAT_START_STREAM', conversationId, messageId: assistantMessageId });

      const response = await this.initiateStream(
        {
          message: approved ? 'Approved' : 'Rejected',
          conversationId,
          previousResponseId,
          mcpApproval: { approvalRequestId, approved },
        },
        abort.signal
      );

      await this.processStream(response, assistantMessageId, { conversationId, started: true }, abort.signal);
    } catch (error) {
      if (abort.signal.aborted || isAbortError(error)) {
        return;
      }
      const appError = this.toAppError(error);
      trackException(appError, { context: 'sendMcpApproval' });
      this.dispatch({ type: 'CHAT_ERROR', error: appError });
    } finally {
      if (this.currentStreamAbort === abort) {
        this.currentStreamAbort = undefined;
      }
    }
  }

  clearChat(): void {
    this.dispatch({ type: 'CHAT_CLEAR' });
  }

  clearError(): void {
    this.dispatch({ type: 'CHAT_CLEAR_ERROR' });
  }

  /** Cancel the active stream, if any. Safe to call when nothing is streaming. */
  cancelStream(): void {
    if (this.currentStreamAbort) {
      const abort = this.currentStreamAbort;
      this.currentStreamAbort = undefined;
      abort.abort();
      this.dispatch({ type: 'CHAT_CANCEL_STREAM' });
    }
  }

  /**
   * Download a file cited or generated in a conversation the user owns.
   * The backend only serves files referenced by that conversation.
   */
  async downloadFile(fileId: string, conversationId: string, fileName?: string, containerId?: string): Promise<void> {
    const params = new URLSearchParams({ conversationId });
    if (containerId) {
      params.set('containerId', containerId);
    }

    const response = await this.apiFetchAsApi(`/files/${encodeURIComponent(fileId)}?${params.toString()}`);
    const url = URL.createObjectURL(await response.blob());
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName || fileId;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(url), 100);
  }

  /** List the signed-in user's conversations. */
  async listConversations(limit: number = 20): Promise<{ conversations: ConversationSummary[]; hasMore: boolean }> {
    const response = await this.apiFetchAsApi(`/conversations?limit=${limit}`);
    return response.json();
  }

  /** Messages of a conversation owned by the signed-in user. */
  async getConversationMessages(conversationId: string): Promise<ConversationMessageInfo[]> {
    const response = await this.apiFetchAsApi(`/conversations/${encodeURIComponent(conversationId)}/messages`);
    return response.json();
  }

  async deleteConversation(conversationId: string): Promise<void> {
    await this.apiFetchAsApi(`/conversations/${encodeURIComponent(conversationId)}`, { method: 'DELETE' });
  }

  /** REST calls report failures with the 'API' code (validation/auth codes are kept). */
  private async apiFetchAsApi(path: string, init?: RequestInit): Promise<Response> {
    try {
      return await this.apiFetch(path, init);
    } catch (error) {
      if (isAppError(error) && (error.code === 'SERVER' || error.code === 'UNKNOWN')) {
        error.code = 'API';
      }
      throw error;
    }
  }
}
