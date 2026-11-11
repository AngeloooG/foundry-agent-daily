import { memo } from 'react';
import ReactMarkdown, { type Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSanitize from 'rehype-sanitize';

import { isAllowedImageSource, isSafeLinkTarget } from '../../utils/markdownSafety';

/**
 * Markdown renderer for model output (untrusted).
 *
 * - rehype-sanitize (GitHub schema) removes raw HTML and dangerous URLs (javascript:, etc.).
 * - Images: only inline data:/blob: or same-origin sources are rendered; remote images are not
 *   loaded (zero-click exfiltration channel). The backend CSP (img-src 'self' data: blob:)
 *   enforces the same rule as defense in depth.
 * - Links: only http(s)/mailto, opened in a new tab with noopener/noreferrer so the chat (kept
 *   in memory) is not lost and the target page cannot reach window.opener.
 * - Memoized: during streaming only the message that changes is re-parsed.
 */

const REMARK_PLUGINS = [remarkGfm];
const REHYPE_PLUGINS = [rehypeSanitize];

const COMPONENTS: Components = {
  a: ({ href, children }) =>
    isSafeLinkTarget(href) ? (
      <a href={href} target="_blank" rel="noopener noreferrer">
        {children}
      </a>
    ) : (
      <span>{children}</span>
    ),
  img: ({ src, alt }) =>
    isAllowedImageSource(src) ? (
      <img src={src} alt={alt ?? ''} loading="lazy" />
    ) : (
      // Show the alt text instead of contacting an external host.
      <span title="External image not loaded">[{alt || 'image'}]</span>
    ),
};

interface SafeMarkdownProps {
  content: string;
}

export const SafeMarkdown = memo(function SafeMarkdown({ content }: SafeMarkdownProps) {
  return (
    <ReactMarkdown remarkPlugins={REMARK_PLUGINS} rehypePlugins={REHYPE_PLUGINS} components={COMPONENTS}>
      {content}
    </ReactMarkdown>
  );
});
