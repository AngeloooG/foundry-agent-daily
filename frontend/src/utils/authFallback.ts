import { BrowserAuthError, InteractionRequiredAuthError } from '@azure/msal-browser';

/**
 * MSAL errors raised when the silent (hidden iframe) token renewal cannot complete. With the
 * backend CSP `frame-ancestors 'none'`, the app cannot be loaded inside that iframe, so silent
 * renewal fails once the refresh token expires; these errors must lead to a full-page redirect.
 * Codes from @azure/msal-browser BrowserAuthErrorCodes.
 */
const SILENT_RENEWAL_FAILURE_CODES = new Set([
  'monitor_window_timeout',
  'timed_out',
  'block_iframe_reload',
  'iframe_closed_prematurely',
]);

/**
 * True when acquireTokenSilent failed in a way that only an interactive redirect can fix.
 * Network errors and other failures are not retried interactively.
 */
export function requiresRedirectFallback(error: unknown): boolean {
  if (error instanceof InteractionRequiredAuthError) {
    return true;
  }
  return error instanceof BrowserAuthError && SILENT_RENEWAL_FAILURE_CODES.has(error.errorCode);
}
