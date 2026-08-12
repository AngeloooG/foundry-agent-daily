import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
} from 'react';

import type { DailyTranslation } from '../../config/dailyTranslations';
import { ChatService } from '../../services/chatService';
import { useAppState } from '../../hooks/useAppState';
import { useAuth } from '../../hooks/useAuth';
import { useAppContext } from '../../contexts/AppContext';
import { trackFeedback } from '../../services/telemetry';
import { DailyChatInterface } from './DailyChatInterface';

interface DailyAgentChatProps {
  t: DailyTranslation;
  agentId: string;
  agentName: string;
  agentDescription?: string;
  agentLogo?: string;
  starterPrompts?: string[];
  initialDraft?: string;
  onInitialDraftConsumed?: () => void;
}

export const DailyAgentChat: React.FC<DailyAgentChatProps> = ({
  t,
  agentName,
  agentDescription,
  starterPrompts,
  initialDraft,
  onInitialDraftConsumed,
}) => {
  const { chat } = useAppState();
  const { dispatch } = useAppContext();
  const { getAccessToken } = useAuth();

  const apiUrl = import.meta.env.VITE_API_URL || '/api';

  const chatService = useMemo(
    () => new ChatService(apiUrl, getAccessToken, dispatch),
    [apiUrl, getAccessToken, dispatch]
  );

  const handleSendMessage = useCallback(
    async (text: string, files?: File[]) => {
      if (
        chat.status === 'streaming' ||
        chat.status === 'sending'
      ) {
        dispatch({
          type: 'CHAT_QUEUE_MESSAGE',
          text,
          files,
        });
        return;
      }

      await chatService.sendMessage(
        text,
        chat.currentConversationId,
        files
      );
    },
    [
      chat.status,
      chat.currentConversationId,
      chatService,
      dispatch,
    ]
  );

  const pendingMessagesRef = useRef(chat.pendingMessages);
  pendingMessagesRef.current = chat.pendingMessages;

  useEffect(() => {
    if (
      chat.status !== 'idle' ||
      pendingMessagesRef.current.length === 0
    ) {
      return;
    }

    const combinedText = pendingMessagesRef.current
      .map((message) => message.text)
      .join('\n\n');

    const combinedFiles = pendingMessagesRef.current.flatMap(
      (message) => message.files || []
    );

    dispatch({ type: 'CHAT_CLEAR_QUEUE' });

    void chatService.sendMessage(
      combinedText,
      chat.currentConversationId,
      combinedFiles.length > 0
        ? combinedFiles
        : undefined
    );
  }, [
    chat.status,
    chat.currentConversationId,
    chatService,
    dispatch,
  ]);

  const handleClearError = useCallback(() => {
    chatService.clearError();
  }, [chatService]);

  const handleCancelStream = useCallback(() => {
    chatService.cancelStream();
  }, [chatService]);

  const handleRecoveredInputConsumed = useCallback(() => {
    dispatch({
      type: 'CHAT_CONSUMED_RECOVERED_INPUT',
    });
  }, [dispatch]);

  const handleRegenerate = useCallback(() => {
    chatService.cancelStream();
    dispatch({ type: 'CHAT_REGENERATE' });
  }, [chatService, dispatch]);

  const handleEditMessage = useCallback(
    (messageId: string, newText: string) => {
      dispatch({
        type: 'CHAT_EDIT_MESSAGE',
        messageId,
        newText,
      });
    },
    [dispatch]
  );

  const handleCancelEdit = useCallback(() => {
    dispatch({ type: 'CHAT_CANCEL_EDIT' });
  }, [dispatch]);

  const handleFeedback = useCallback(
    (
      messageId: string,
      rating: 'positive' | 'negative'
    ) => {
      trackFeedback(
        messageId,
        chat.currentConversationId,
        rating
      );
    },
    [chat.currentConversationId]
  );

  useEffect(() => {
    const regenerateText = chat.regenerateText?.trim();

    if (!regenerateText || chat.status !== 'idle') {
      return;
    }

    dispatch({ type: 'CHAT_CONSUMED_REGENERATE' });

    void chatService.sendMessage(
      regenerateText,
      chat.currentConversationId
    );
  }, [
    chat.regenerateText,
    chat.status,
    chat.currentConversationId,
    chatService,
    dispatch,
  ]);

  return (
    <DailyChatInterface
      t={t}
      messages={chat.messages}
      status={chat.status}
      error={chat.error}
      streamingMessageId={chat.streamingMessageId}
      recoveredInput={chat.recoveredInput}
      recoveredAttachments={chat.recoveredAttachments}
      pendingMessages={chat.pendingMessages}
      agentName={agentName}
      agentDescription={agentDescription}
      starterPrompts={starterPrompts}
      initialDraft={initialDraft}
      isEditing={Boolean(chat.editSnapshot)}
      onSendMessage={handleSendMessage}
      onClearError={handleClearError}
      onCancelStream={handleCancelStream}
      onRecoveredInputConsumed={
        handleRecoveredInputConsumed
      }
      onInitialDraftConsumed={onInitialDraftConsumed}
      onRegenerate={handleRegenerate}
      onEditMessage={handleEditMessage}
      onCancelEdit={handleCancelEdit}
      onFeedback={handleFeedback}
    />
  );
};
