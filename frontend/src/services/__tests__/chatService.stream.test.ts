import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { Dispatch } from 'react';
import { ChatService } from '../chatService';
import type { AppAction } from '../../types/appState';
import { validateDocumentFile } from '../../utils/fileAttachments';

/**
 * Integration tests of the critical chat path (sendMessage → SSE → reducer actions), created in
 * Lote 1 BEFORE refactoring. Active tests characterize behavior to preserve; `it.skip` tests define
 * the target behavior of later lotes (each lote turns its tests on).
 */

vi.mock('../telemetry', () => ({ trackException: vi.fn() }));

function sseResponse(events: object[], { endWithoutDone = false } = {}): Response {
  const encoder = new TextEncoder();
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const e of events) controller.enqueue(encoder.encode(`data: ${JSON.stringify(e)}\n\n`));
      if (!endWithoutDone) controller.enqueue(encoder.encode('data: {"type":"done"}\n\n'));
      controller.close();
    },
  });
  return new Response(body, { status: 200, headers: { 'Content-Type': 'text/event-stream' } });
}

function problemResponse(status: number, detail: string): Response {
  return new Response(JSON.stringify({ title: 'Error', detail, status }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

const HAPPY_EVENTS = [
  { type: 'conversationId', conversationId: 'conv_1' },
  { type: 'chunk', content: 'Hola' },
  { type: 'usage', promptTokens: 1, completionTokens: 2, totalTokens: 3, duration: 10 },
];

type LooseAction = { type: string; messageText?: string; error?: { code?: string; message?: string } };

describe('ChatService.sendMessage (critical path)', () => {
  let dispatch: ReturnType<typeof vi.fn>;
  let service: ChatService;

  const actions = () => dispatch.mock.calls.map(([a]) => (a as AppAction).type);
  const find = (type: string) =>
    dispatch.mock.calls.map(([a]) => a as LooseAction).find((a) => a.type === type);

  beforeEach(() => {
    dispatch = vi.fn();
    service = new ChatService('/api', vi.fn().mockResolvedValue('token'), dispatch as unknown as Dispatch<AppAction>);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  // ---------- preserved behavior (active) ----------

  it('streams a response and completes', async () => {
    const fetchMock = vi.fn().mockResolvedValue(sseResponse(HAPPY_EVENTS));
    vi.stubGlobal('fetch', fetchMock);

    await service.sendMessage('Hola', null);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(actions()).toEqual([
      'CHAT_SEND_MESSAGE',
      'CHAT_ADD_ASSISTANT_MESSAGE',
      'CHAT_START_STREAM',
      'CHAT_START_STREAM', // conversation id received from the server
      'CHAT_STREAM_CHUNK',
      'CHAT_STREAM_COMPLETE',
    ]);
  });

  it('sends the existing conversation id', async () => {
    const fetchMock = vi.fn().mockResolvedValue(sseResponse(HAPPY_EVENTS));
    vi.stubGlobal('fetch', fetchMock);

    await service.sendMessage('Hola', 'conv_existing');

    const body = JSON.parse((fetchMock.mock.calls[0][1] as RequestInit).body as string);
    expect(body.conversationId).toBe('conv_existing');
  });

  // ---------- Lote 5: retries and streaming (target behavior) ----------

  it.skip('Lote 5: does not retry 4xx responses and restores the input', async () => {
    const fetchMock = vi.fn().mockResolvedValue(problemResponse(400, 'File 1 (a.pdf): too big.'));
    vi.stubGlobal('fetch', fetchMock);

    await service.sendMessage('mira esto', 'conv_1');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(find('CHAT_RECOVER_MESSAGE')?.messageText).toBe('mira esto');
  });

  it.skip('Lote 5: does not retry after the first chunk or an SSE error event', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      sseResponse([
        { type: 'conversationId', conversationId: 'conv_new' },
        { type: 'chunk', content: 'parcial' },
        { type: 'error', message: 'falló' },
      ])
    );
    vi.stubGlobal('fetch', fetchMock);

    await service.sendMessage('hola', null);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(find('CHAT_RECOVER_MESSAGE')?.messageText).toBe('hola');
  });

  it.skip('Lote 5: a stream that ends without "done" releases the UI and restores the input', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      sseResponse([{ type: 'conversationId', conversationId: 'conv_1' }, { type: 'chunk', content: 'x' }], { endWithoutDone: true })
    ));

    await service.sendMessage('hola', null);

    expect(find('CHAT_RECOVER_MESSAGE')?.messageText).toBe('hola');
  });

  it.skip('Lote 5: retries a transient failure before the first chunk reusing the conversation id', async () => {
    vi.useFakeTimers();
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce(sseResponse(HAPPY_EVENTS));
    vi.stubGlobal('fetch', fetchMock);

    const sending = service.sendMessage('hola', 'conv_1');
    await vi.runAllTimersAsync();
    await sending;

    expect(fetchMock).toHaveBeenCalledTimes(2);
    const bodies = fetchMock.mock.calls.map(([, init]) => JSON.parse((init as RequestInit).body as string));
    expect(bodies.every((b) => b.conversationId === 'conv_1')).toBe(true);
  });

  it.skip('Lote 5: cancelling does not report an error nor duplicate messages', async () => {
    const fetchMock = vi.fn().mockImplementation((_url: string, init: RequestInit) =>
      new Promise((_resolve, reject) => {
        init.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
      })
    );
    vi.stubGlobal('fetch', fetchMock);

    const sending = service.sendMessage('hola', null);
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());
    service.cancelStream();
    await sending;

    expect(find('CHAT_RECOVER_MESSAGE')).toBeUndefined();
    expect(actions().filter((t) => t === 'CHAT_SEND_MESSAGE')).toHaveLength(1);
  });
});

describe('attachments (Lote 4 target behavior)', () => {
  const file = (name: string, type: string, size = 10) => {
    const f = new File(['x'], name, { type });
    Object.defineProperty(f, 'size', { value: size });
    return f;
  };

  it('Lote 4: text/html is rejected', () => {
    expect(validateDocumentFile(file('page.html', 'text/html')).valid).toBe(false);
  });

});

describe('ChatService.downloadFile (lote 2 contract)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('sends the conversation id the backend uses to authorize the download', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(new Blob(['x']), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {});
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    const service = new ChatService('/api', vi.fn().mockResolvedValue('token'), vi.fn());

    await service.downloadFile('file 1', 'conv_1', 'a.csv', 'cntr_1');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/files/file%201?conversationId=conv_1&containerId=cntr_1');
  });

  it('reports a failed download as an API error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 404 })));
    const service = new ChatService('/api', vi.fn().mockResolvedValue('token'), vi.fn());

    await expect(service.downloadFile('f', 'conv_1')).rejects.toMatchObject({ code: 'API' });
  });
});
