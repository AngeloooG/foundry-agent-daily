import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { Dispatch } from 'react';
import { ChatService } from '../chatService';
import type { AppAction } from '../../types/appState';

vi.mock('../telemetry', () => ({ trackException: vi.fn() }));

function problemResponse(status: number, detail: string, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify({ title: 'Error', detail, status }), {
    status,
    headers: { 'Content-Type': 'application/problem+json', ...headers },
  });
}

type RecoverAction = Extract<AppAction, { type: 'CHAT_RECOVER_MESSAGE' }>;

describe('ChatService retry policy (lote 5)', () => {
  let dispatch: ReturnType<typeof vi.fn>;
  let service: ChatService;
  const recover = () =>
    dispatch.mock.calls.map(([a]) => a as AppAction).find((a): a is RecoverAction => a.type === 'CHAT_RECOVER_MESSAGE');

  beforeEach(() => {
    dispatch = vi.fn();
    service = new ChatService('/api', vi.fn().mockResolvedValue('token'), dispatch as unknown as Dispatch<AppAction>);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('shows the server validation reason and removes only the optimistic messages', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(problemResponse(400, 'File 1 (a.pdf): too big.')));

    await service.sendMessage('mira', 'conv_1');

    expect(recover()?.error.code).toBe('VALIDATION');
    expect(recover()?.error.message).toBe('File 1 (a.pdf): too big.');
    expect(recover()?.messageIds).toHaveLength(2);
  });

  it('does not retry rate limiting and tells the user how long to wait', async () => {
    const fetchMock = vi.fn().mockResolvedValue(problemResponse(429, 'slow down', { 'Retry-After': '30' }));
    vi.stubGlobal('fetch', fetchMock);

    await service.sendMessage('hola', null);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(recover()?.error.code).toBe('RATE_LIMIT');
    expect(recover()?.error.message).toContain('30s');
  });

  it('retries transient gateway errors up to 3 times with the same conversation id', async () => {
    vi.useFakeTimers();
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(problemResponse(503, 'busy')));
    vi.stubGlobal('fetch', fetchMock);

    const sending = service.sendMessage('hola', 'conv_existing');
    await vi.runAllTimersAsync();
    await sending;

    expect(fetchMock).toHaveBeenCalledTimes(3);
    for (const [, init] of fetchMock.mock.calls) {
      expect(JSON.parse((init as RequestInit).body as string).conversationId).toBe('conv_existing');
    }
    expect(recover()?.retryCount).toBe(3);
  });

  it('never retries a 500 (server bug, not transient)', async () => {
    const fetchMock = vi.fn().mockResolvedValue(problemResponse(500, 'boom'));
    vi.stubGlobal('fetch', fetchMock);

    await service.sendMessage('hola', null);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('restores the text without touching the thread when no token can be obtained', async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    service = new ChatService('/api', vi.fn().mockResolvedValue(null), dispatch as unknown as Dispatch<AppAction>);

    await service.sendMessage('hola', null);

    expect(fetchMock).not.toHaveBeenCalled();
    expect(dispatch.mock.calls.map(([a]) => (a as AppAction).type)).toEqual(['AUTH_TOKEN_EXPIRED', 'CHAT_RECOVER_MESSAGE']);
    expect(recover()?.messageIds).toEqual([]);
  });

  it('sendMessage never rejects (errors go to state)', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('unexpected')));

    await expect(service.sendMessage('hola', null)).resolves.toBeUndefined();
    expect(recover()).toBeDefined();
  });
});
