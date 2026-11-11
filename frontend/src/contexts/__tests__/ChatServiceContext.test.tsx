import { describe, it, expect, vi, afterEach } from 'vitest';
import { cleanup, render } from '@testing-library/react';
import { ChatServiceProvider, useChatService } from '../ChatServiceContext';
import type { ChatService } from '../../services/chatService';

vi.mock('../AppContext', () => ({ useAppContext: () => ({ dispatch: vi.fn() }) }));
vi.mock('../../hooks/useAuth', () => ({ useAuth: () => ({ getAccessToken: vi.fn().mockResolvedValue('token') }) }));

const seen: ChatService[] = [];
function Probe() {
  seen.push(useChatService());
  return null;
}

describe('ChatServiceProvider (lote 5)', () => {
  afterEach(() => {
    cleanup();
    seen.length = 0;
  });

  it('keeps one service instance for the whole session (survives re-renders and remounted children)', () => {
    const { rerender } = render(<ChatServiceProvider><Probe /></ChatServiceProvider>);
    rerender(<ChatServiceProvider><Probe key="remounted" /></ChatServiceProvider>);

    expect(seen.length).toBeGreaterThanOrEqual(2);
    expect(new Set(seen).size).toBe(1);
  });

  it('cancels the active stream when the session ends', () => {
    const { unmount } = render(<ChatServiceProvider><Probe /></ChatServiceProvider>);
    const cancel = vi.spyOn(seen[0], 'cancelStream');

    unmount();

    expect(cancel).toHaveBeenCalled();
  });

  it('requires the provider', () => {
    expect(() => render(<Probe />)).toThrow('useChatService must be used within ChatServiceProvider');
  });
});
