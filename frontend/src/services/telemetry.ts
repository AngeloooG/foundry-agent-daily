import { ApplicationInsights } from '@microsoft/applicationinsights-web';
import { toTelemetryError } from '../utils/errorHandler';

const connectionString = import.meta.env.VITE_APPLICATIONINSIGHTS_CONNECTION_STRING;

let appInsights: ApplicationInsights | null = null;

/**
 * Browser telemetry (Application Insights). No message content, tokens or headers are sent:
 * - enableCorsCorrelation off: the API is same-origin, and correlation headers must not be
 *   attached to third-party requests (e.g. the Entra ID token endpoint).
 * - enableResponseHeaderTracking off: headers can carry file names (Content-Disposition).
 * - enableAutoRouteTracking on: the app uses react-router, so page views are tracked per route.
 */
export function initTelemetry(): void {
  if (!connectionString) return;

  appInsights = new ApplicationInsights({
    config: {
      connectionString,
      enableAutoRouteTracking: true,
      disableFetchTracking: false,
      enableCorsCorrelation: false,
      enableRequestHeaderTracking: false,
      enableResponseHeaderTracking: false,
      enableUnhandledPromiseRejectionTracking: true,
    },
  });

  appInsights.loadAppInsights();
}

/** Track an error. Accepts Errors and AppErrors (plain objects) without leaking user content. */
export function trackException(error: unknown, properties?: Record<string, string>): void {
  appInsights?.trackException({ exception: toTelemetryError(error) }, properties);
}

export function trackFeedback(messageId: string, conversationId: string | null, rating: 'positive' | 'negative'): void {
  appInsights?.trackEvent({
    name: 'message_feedback',
    properties: {
      messageId,
      conversationId: conversationId ?? '',
      rating,
    },
  });
}
