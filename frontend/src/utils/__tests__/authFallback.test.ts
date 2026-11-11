import { describe, it, expect } from 'vitest';
import { BrowserAuthError, InteractionRequiredAuthError } from '@azure/msal-browser';
import { requiresRedirectFallback } from '../authFallback';

describe('requiresRedirectFallback', () => {
  it('redirects when interaction is required', () => {
    expect(requiresRedirectFallback(new InteractionRequiredAuthError('interaction_required'))).toBe(true);
  });

  it.each(['monitor_window_timeout', 'timed_out', 'block_iframe_reload', 'iframe_closed_prematurely'])(
    'redirects when the silent iframe fails (%s), e.g. blocked by frame-ancestors',
    (code) => {
      expect(requiresRedirectFallback(new BrowserAuthError(code))).toBe(true);
    }
  );

  it.each(['no_network_connectivity', 'interaction_in_progress', 'popup_window_error'])(
    'does not redirect for other browser errors (%s)',
    (code) => {
      expect(requiresRedirectFallback(new BrowserAuthError(code))).toBe(false);
    }
  );

  it('does not redirect for non-MSAL errors', () => {
    expect(requiresRedirectFallback(new Error('boom'))).toBe(false);
    expect(requiresRedirectFallback(undefined)).toBe(false);
  });
});
