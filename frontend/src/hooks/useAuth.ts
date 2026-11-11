import { useMsal } from "@azure/msal-react";
import { silentTokenRequest, tokenRequest } from "../config/authConfig";
import { requiresRedirectFallback } from "../utils/authFallback";
import { useCallback, useMemo } from "react";

/**
 * Authentication hook for MSAL-based authentication.
 *
 * Token acquisition: `acquireTokenSilent` first (cached token or refresh token; hidden iframe when
 * the refresh token expired). When interaction is required, or the silent iframe fails/times out
 * (the CSP forbids framing the app), it falls back to `acquireTokenRedirect`, which navigates away
 * and completes the request when the app reloads. No popups (often blocked), no tokens or claims logged.
 *
 * @example
 * ```tsx
 * const { getAccessToken } = useAuth();
 * const token = await getAccessToken(); // null while redirecting or on failure
 * ```
 */
export const useAuth = () => {
  const { instance, accounts } = useMsal();

  const getAccessToken = useCallback(async (): Promise<string | null> => {
    // Prefer the active account set at sign-in (main.tsx); fall back to the first cached one.
    const account = instance.getActiveAccount() ?? accounts[0];
    if (!account) {
      return null;
    }

    try {
      const response = await instance.acquireTokenSilent({ ...silentTokenRequest, account });
      return response.accessToken;
    } catch (error) {
      if (requiresRedirectFallback(error)) {
        // Full-page redirect (never uses the silent redirect page).
        await instance.acquireTokenRedirect({ ...tokenRequest, account });
        return null;
      }
      console.error("Token acquisition failed");
      return null;
    }
  }, [instance, accounts]);

  const isAuthenticated = useMemo(() => accounts.length > 0, [accounts.length]);

  const user = useMemo(() => instance.getActiveAccount() ?? accounts[0], [instance, accounts]);

  return useMemo(
    () => ({
      getAccessToken,
      isAuthenticated,
      user,
    }),
    [getAccessToken, isAuthenticated, user]
  );
};
