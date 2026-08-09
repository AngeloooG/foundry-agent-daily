import React, { useCallback, useEffect, useMemo, useRef } from 'react';
import { ChatService } from '../../services/chatService';
import { useAppState } from '../../hooks/useAppState';
import { useAuth } from '../../hooks/useAuth';
import { useAppContext } from '../../contexts/AppContext';
import { exportAsMarkdown, downloadMarkdown } from '../../utils/exportConversation';
import { trackFeedback } from '../../services/telemetry';
import type { IChatItem } from '../../types/chat';
import { DailyChatInterface } from './DailyChatInterface';

interface DailyAgentChatProps {
  agentId: string;
  agentName: string;
  agentDescription?: string;
  agentLogo?: string;
  starterPrompts?: string[];
}

export const DailyAgentChat: React.FC<DailyAgentChatProps> = ({
  agentName,
  agentDescription,
  agentLogo,
  starterPrompts,
}) => {
  const { chat, state } = useAppState();
  const { dispatch } = useAppContext();
  const { getAccessToken } = useAuth();

  const apiUrl = import.meta.env.VITE_API_URL || '/api';

  const chatService = useMemo(() => {
    return new ChatService(apiUrl, getAccessToken, dispatch);
  }, [apiUrl, getAccessToken, dispatch]);

  const handleSendMessage = async (text: string, files?: File[]) => {
    if (chat.status === 'streaming' || chat.status === 'sending') {
      dispatch({ type: 'CHAT_QUEUE_MESSAGE', text, files });
      return;
    }

    await chatService.sendMessage(text, chat.currentConversationId, files);
  };

  const pendingRef = useRef(chat.pendingMessages);
  pendingRef.current = chat.pendingMessages;

  useEffect(() => {
    if (chat.status === 'idle' && pendingRef.current.length > 0) {
      const combinedText = pendingRef.current.map((m) => m.text).join('\n\n');
      const combinedFiles = pendingRef.current.flatMap((m) => m.files || []);

      dispatch({ type: 'CHAT_CLEAR_QUEUE' });

      chatService.sendMessage(
        combinedText,
        chat.currentConversationId,
        combinedFiles.length > 0 ? combinedFiles : undefined
      );
    }
  }, [chat.status, chat.currentConversationId, chatService, dispatch]);

  const loadConversations = useCallback(async () => {
    dispatch({ type: 'CONVERSATIONS_LOADING' });

    try {
      const result = await chatService.listConversations();
      dispatch({
        type: 'CONVERSATIONS_SET_LIST',
        conversations: result.conversations,
        hasMore: result.hasMore,
      });
    } catch (error) {
      console.error('Failed to load conversations:', error);
      dispatch({ type: 'CONVERSATIONS_SET_LIST', conversations: [], hasMore: false });
    }
  }, [chatService, dispatch]);

  useEffect(() => {
    void loadConversations();
  }, [loadConversations]);

  useEffect(() => {
    if (chat.currentConversationId || chat.status === 'idle') {
      void loadConversations();
    }
  }, [chat.currentConversationId, chat.status, loadConversations]);

  const handleClearError = () => {
    chatService.clearError();
  };

  const handleNewChat = () => {
    chatService.cancelStream();
    chatService.clearChat();
  };

  const handleCancelStream = () => {
    chatService.cancelStream();
  };

  const handleRecoveredInputConsumed = () => {
    dispatch({ type: 'CHAT_CONSUMED_RECOVERED_INPUT' });
  };

  const handleRegenerate = useCallback(() => {
    chatService.cancelStream();
    dispatch({ type: 'CHAT_REGENERATE' });
  }, [chatService, dispatch]);

  const handleEditMessage = useCallback(
    (messageId: string, newText: string) => {
      dispatch({ type: 'CHAT_EDIT_MESSAGE', messageId, newText });
    },
    [dispatch]
  );

  const handleCancelEdit = useCallback(() => {
    dispatch({ type: 'CHAT_CANCEL_EDIT' });
  }, [dispatch]);

  const handleFeedback = useCallback(
    (messageId: string, rating: 'positive' | 'negative') => {
      trackFeedback(messageId, chat.currentConversationId, rating);
    },
    [chat.currentConversationId]
  );

  const handleDownloadFile = useCallback(
    async (fileId: string, fileName: string, containerId?: string) => {
      try {
        await chatService.downloadFile(fileId, fileName, containerId);
      } catch (err) {
        dispatch({
          type: 'CHAT_ERROR',
          error: {
            code: 'NETWORK',
            message: `Failed to download ${fileName}: ${
              err instanceof Error ? err.message : 'Unknown error'
            }`,
            recoverable: true,
          },
        });
      }
    },
    [chatService, dispatch]
  );

  useEffect(() => {
    if (chat.regenerateText?.trim() && chat.status === 'idle') {
      const text = chat.regenerateText;

      dispatch({ type: 'CHAT_CONSUMED_REGENERATE' });
      chatService.sendMessage(text, chat.currentConversationId);
    }
  }, [chat.regenerateText, chat.status, chat.currentConversationId, chatService, dispatch]);

  const handleExportConversation = useCallback(() => {
    const md = exportAsMarkdown(chat.messages, agentName);
    downloadMarkdown(md);
  }, [chat.messages, agentName]);

  const handleLoadConversation = useCallback(
    async (conversationId: string) => {
      try {
        chatService.cancelStream();

        const messages = await chatService.getConversationMessages(conversationId);

        const chatItems: IChatItem[] = messages
          .filter((msg) => msg.role === 'user' || msg.role === 'assistant')
          .map((msg, index) => ({
            id: `${conversationId}-${index}`,
            role: msg.role as 'user' | 'assistant',
            content: msg.content,
            more: { time: new Date().toISOString() },
          }));

        dispatch({
          type: 'CHAT_LOAD_CONVERSATION',
          conversationId,
          messages: chatItems,
        });
      } catch (error) {
        console.error('Failed to load conversation:', error);
      }
    },
    [chatService, dispatch]
  );

  return (
    <DailyChatInterface
      messages={chat.messages}
      status={chat.status}
      error={chat.error}
      streamingMessageId={chat.streamingMessageId}
      recoveredInput={chat.recoveredInput}
      recoveredAttachments={chat.recoveredAttachments}
      pendingMessages={chat.pendingMessages}
      currentConversationId={chat.currentConversationId}
      conversations={state.conversations.list}
      sidebarOpen={state.conversations.sidebarOpen}
      agentName={agentName}
      agentDescription={agentDescription}
      agentLogo={agentLogo}
      starterPrompts={starterPrompts}
      isEditing={!!chat.editSnapshot}
      onSendMessage={handleSendMessage}
      onClearError={handleClearError}
      onNewChat={handleNewChat}
      onCancelStream={handleCancelStream}
      onRecoveredInputConsumed={handleRecoveredInputConsumed}
      onRegenerate={handleRegenerate}
      onEditMessage={handleEditMessage}
      onCancelEdit={handleCancelEdit}
      onFeedback={handleFeedback}
      onDownloadFile={handleDownloadFile}
      onExportConversation={handleExportConversation}
      onLoadConversation={handleLoadConversation}
    />
  );
};