import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { ChatService } from '../services/chatService';
import { useAppContext } from './AppContext';
import { useAuth } from '../hooks/useAuth';

const ChatServiceContext = createContext<ChatService | null>(null);

/**
 * Owns a single ChatService for the signed-in session.
 *
 * Previously each mount of the chat route created a new service, so after navigating away and
 * back the "Stop" button talked to a service that did not own the running stream. A session-wide
 * instance keeps the AbortController reachable; the stream is cancelled when the session ends.
 */
export function ChatServiceProvider({ children }: { children: ReactNode }) {
  const { dispatch } = useAppContext();
  const { getAccessToken } = useAuth();

  // The service reads the latest token getter through a ref, so it never needs to be recreated.
  const getAccessTokenRef = useRef(getAccessToken);
  useEffect(() => {
    getAccessTokenRef.current = getAccessToken;
  }, [getAccessToken]);

  const [chatService] = useState(
    () => new ChatService(import.meta.env.VITE_API_URL || '/api', () => getAccessTokenRef.current(), dispatch)
  );

  useEffect(() => () => chatService.cancelStream(), [chatService]);

  return <ChatServiceContext.Provider value={chatService}>{children}</ChatServiceContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useChatService(): ChatService {
  const chatService = useContext(ChatServiceContext);
  if (!chatService) {
    throw new Error('useChatService must be used within ChatServiceProvider');
  }
  return chatService;
}
