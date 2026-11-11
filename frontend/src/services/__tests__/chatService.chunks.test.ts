import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { Dispatch } from 'react';
import { ChatService } from '../chatService';
import { appReducer } from '../../reducers/appReducer';
import { initialAppState, type AppAction, type AppState } from '../../types/appState';

/**
 * Every SSE `chunk` is a text delta and must be appended as received. A previous implementation
 * dropped a chunk when it was identical to the one before, corrupting the answer ("A","A","B" → "AB").
 * These tests run the dispatched actions through the real reducer, so they check the text the
 * user actually sees.
 */

vi.mock('../telemetry', () => ({ trackException: vi.fn() }));

function sseResponse(chunks: string[]): Response {
  const encoder = new TextEncoder();
  const events = [
    { type: 'conversationId', conversationId: 'conv_1' },
    ...chunks.map((content) => ({ type: 'chunk', content })),
    { type: 'usage', promptTokens: 1, completionTokens: 2, totalTokens: 3, duration: 10 },
    { type: 'done' },
  ];
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const e of events) controller.enqueue(encoder.encode(`data: ${JSON.stringify(e)}\n\n`));
      controller.close();
    },
  });
  return new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream' } });
}

describe('ChatService streaming: chunks are appended, never deduplicated by content', () => {
  let state: AppState;
  let service: ChatService;

  const dispatch = (action: AppAction) => {
    state = appReducer(state, action);
  };

  const assistantText = () =>
    state.chat.messages.filter((m) => m.role === 'assistant').map((m) => m.content).join('');

  beforeEach(() => {
    state = initialAppState;
    service = new ChatService('/api', vi.fn().mockResolvedValue('token'), dispatch as Dispatch<AppAction>);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  async function stream(chunks: string[]): Promise<string> {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(sseResponse(chunks)));
    await service.sendMessage('pregunta', null);
    return assistantText();
  }

  it('keeps two identical consecutive chunks: ["A", "A", "B"] -> "AAB"', async () => {
    expect(await stream(['A', 'A', 'B'])).toBe('AAB');
  });

  it('keeps two consecutive line breaks', async () => {
    expect(await stream(['Hola', '\n', '\n', 'mundo'])).toBe('Hola\n\nmundo');
  });

  it('keeps two consecutive spaces', async () => {
    expect(await stream(['a', ' ', ' ', 'b'])).toBe('a  b');
  });

  it('does not invent content-based idempotency: an event delivered twice is appended twice', async () => {
    // The stream carries no event id, so the client cannot tell a duplicated delivery from a real
    // repeated delta. Idempotency, if required, must come from a server-provided event id.
    expect(await stream(['Hola', 'Hola'])).toBe('HolaHola');
  });

  it('completes the stream normally after repeated chunks', async () => {
    await stream(['x', 'x']);
    expect(state.chat.status).not.toBe('streaming');
    expect(state.chat.error).toBeNull();
  });
});
