/**
 * Error types and interfaces for structured error handling
 */

export type ErrorCode = 
  | 'NETWORK'      // Connection/fetch failures, 5xx errors
  | 'AUTH'         // 401/403 authentication errors
  | 'STREAM'       // SSE streaming errors
  | 'SERVER'       // 5xx and unexpected server responses
  | 'API'          // REST API errors (conversation management, etc.)
  | 'VALIDATION'   // 400/413: the message or its attachments were rejected (server explains why)
  | 'RATE_LIMIT'   // 429: too many requests for this user
  | 'UNKNOWN';     // Unclassified errors

export interface AppError {
  code: ErrorCode;
  message: string;
  recoverable: boolean;
  action?: {
    label: string;
    handler: () => void;
  };
  originalError?: Error;
  /** HTTP status of the failed request, when the error came from an HTTP response. */
  status?: number;
}

/**
 * Error messages for different error codes
 */
export const ERROR_MESSAGES: Record<ErrorCode, string> = {
  NETWORK: 'Unable to connect to the server. Please check your internet connection and try again.',
  AUTH: 'Your session has expired. Please sign in again to continue.',
  STREAM: 'The response was interrupted. Click Retry to continue your conversation.',
  SERVER: 'The server encountered an error. This has been logged and our team will investigate.',
  API: 'The request failed. Please try again.',
  VALIDATION: 'The message or its attachments could not be accepted. Please review them and try again.',
  RATE_LIMIT: 'You have sent too many messages in a short time. Please wait a moment and try again.',
  UNKNOWN: 'An unexpected error occurred. Please try again or contact support if the issue persists.',
};

/**
 * Detailed user-friendly messages with recovery hints
 */
export const DETAILED_ERROR_MESSAGES: Record<ErrorCode, { title: string; description: string; hint: string }> = {
  NETWORK: {
    title: 'Connection Lost',
    description: 'Unable to reach the server.',
    hint: 'Check your internet connection and try again.'
  },
  AUTH: {
    title: 'Session Expired',
    description: 'Your authentication session has timed out.',
    hint: 'Click "Sign in again" below to continue.'
  },
  STREAM: {
    title: 'Response Interrupted',
    description: 'The AI response was interrupted unexpectedly.',
    hint: 'Click "Retry" to resend your message.'
  },
  SERVER: {
    title: 'Server Error',
    description: 'The server encountered an unexpected error.',
    hint: 'Please try again in a few moments.'
  },
  API: {
    title: 'Request Failed',
    description: 'The API request could not be completed.',
    hint: 'Please try again.'
  },
  VALIDATION: {
    title: 'Invalid Request',
    description: 'The message or its attachments could not be accepted.',
    hint: 'Review the message and attachments and try again.'
  },
  RATE_LIMIT: {
    title: 'Too Many Requests',
    description: 'You have sent too many messages in a short time.',
    hint: 'Please wait a moment and try again.'
  },
  UNKNOWN: {
    title: 'Unexpected Error',
    description: 'Something went wrong.',
    hint: 'Try refreshing the page or contact support if this continues.'
  }
};

/**
 * Determine if an error is recoverable
 */
export function isRecoverableError(code: ErrorCode): boolean {
  // AUTH requires re-login, UNKNOWN may indicate critical failure
  // NETWORK, STREAM, and SERVER errors are typically recoverable with retry
  return code !== 'AUTH' && code !== 'UNKNOWN';
}

/**
 * Type guard to check if an unknown value is an AppError
 */
export function isAppError(error: unknown): error is AppError {
  return (
    error !== null &&
    typeof error === 'object' &&
    'code' in error &&
    'message' in error &&
    'recoverable' in error
  );
}
