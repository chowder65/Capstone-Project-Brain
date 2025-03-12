import { createStore } from 'vuex';
import axios from 'axios';

async function pollResult(correlationId) {
  let retries = 0;
  const maxRetries = 5;
  const delay = 2000;

  while (retries < maxRetries) {
    console.log('Polling with CorrelationId:', correlationId); 
    const response = await axios.get(`http://localhost:5168/api/Result?correlationId=${correlationId}`);
    
    console.log('Poll response:', JSON.stringify(response.data)); 
    
    if (response.data.status === 'completed') {
      return response.data;
    }

    await new Promise(resolve => setTimeout(resolve, delay));
    retries++;
  }

  throw new Error('Polling failed: No result received');
}


export default createStore({
  state: {
    user: null,
    token: localStorage.getItem('token') || null,
    chats: [],
    currentChat: null,
  },
  mutations: {
    setUser(state, user) {
      state.user = user;
    },
    setToken(state, token) {
      state.token = token;
      localStorage.setItem('token', token);
    },
    setChats(state, chats) {
      state.chats = Array.isArray(chats) ? chats : [];
    },
    setCurrentChat(state, chat) {
      state.currentChat = chat;
    },
    logout(state) {
      state.user = null;
      state.token = null;
      state.chats = [];
      state.currentChat = null;
      localStorage.removeItem('token');
    },
  },
  actions: {
    async login({ commit }, credentials) {
      try {
        const response = await axios.post(
          `User/LogIn?email=${encodeURIComponent(credentials.useremail)}&password=${encodeURIComponent(credentials.userpassword)}`
        );
        console.log('Login response:', JSON.stringify(response.data));
        
        const correlationId = response.data.correlationId;
        console.log('Login queued with CorrelationId:', correlationId);
    
        let result;
        try {
          result = await pollResult(correlationId);
          console.log('Poll result received:', JSON.stringify(result));
        } catch (pollError) {
          console.error('Polling error:', pollError);
          throw pollError;
        }
    
        if (result && result.result && result.result.Token) {
          console.log('Token received:', result.result.Token);
          commit('setToken', result.result.Token);
          commit('setUser', { email: credentials.useremail, firstname: '', lastname: '' });
        } else {
          console.error('Login failed: No token received', result);
          throw new Error('Login failed: No token received');
        }
      } catch (error) {
        console.error('Login error:', error.message);
        throw error;
      }
    },

    async signup(_, userData) {
      try {
        const response = await axios.post(
          'User/Create',
          {
            email: userData.useremail,
            password: userData.userpassword,
            firstName: userData.firstname,
            lastName: userData.lastname,
          },
          { headers: { 'Content-Type': 'application/json' } }
        );
        const correlationId = response.data.CorrelationId;
        console.log('Signup queued with CorrelationId:', correlationId);

        await pollResult(correlationId);
      } catch (error) {
        console.error('Signup error:', error.message);
        throw error;
      }
    },

    async fetchChats({ commit, state }) {
      const response = await axios.get('User/Chats', {
        headers: { Authorization: `Bearer ${state.token}` },
      });
      const correlationId = response.data.correlationId;
      console.log('Fetch chats queued with CorrelationId:', correlationId);

      const result = await pollResult(correlationId);
      const chats = (result || []).map(chat => ({
        id: chat.id,
        chatName: chat.chatName,
        messages: chat.messages.map(msg => ({
          content: msg.text,
          timestamp: msg.timestamp,
          isUser: true,
        })),
      }));
      console.log('fetchChats chats:', chats);
      commit('setChats', chats);
    },

    async createChat({ commit, state }, chatName) {
      try {
        const chatRequest = {
          userEmail: state.user.email,
          initialMessage: chatName,
        };
        const response = await axios.post(
          'Chat/StartChat',
          chatRequest,
          {
            headers: {
              Authorization: `Bearer ${state.token}`,
              'Content-Type': 'application/json',
            },
          }
        );
        const correlationId = response.data.correlationId;
        console.log('Chat creation queued with CorrelationId:', correlationId);

        const chatId = await pollResult(correlationId);
        const newChat = {
          id: chatId,
          chatName: chatName,
          messages: [],
        };
        console.log('New chat object:', newChat);
        const updatedChats = [...state.chats, newChat];
        commit('setChats', updatedChats);
        return chatId;
      } catch (error) {
        console.error('Error creating chat:', error.message);
        throw error;
      }
    },

    async fetchChatHistory({ commit, state }, chatId) {
      const id = typeof chatId === 'string' ? chatId : chatId.toString();
      console.log('Fetching history for chatId:', id);
      const response = await axios.get(`User/Chat/History?chatId=${id}`, {
        headers: { Authorization: `Bearer ${state.token}` },
      });
      const correlationId = response.data.correlationId;
      console.log('Chat history fetch queued with CorrelationId:', correlationId);

      const result = await pollResult(correlationId);
      const chat = {
        id: result.id,
        chatName: result.chatName,
        messages: result.messages.map((msg, index) => ({
          content: msg.text,
          timestamp: msg.timestamp,
          isUser: index % 2 === 0,
        })),
      };
      commit('setCurrentChat', chat);
    },

    async sendMessage({ commit, state }, { chatId, message }) {
      try {
        const sendMessageRequest = {
          chatId,
          message,
        };
        const response = await axios.post(
          'Chat/SendMessage',
          sendMessageRequest,
          {
            headers: {
              Authorization: `Bearer ${state.token}`,
              'Content-Type': 'application/json',
            },
          }
        );
        const correlationId = response.data.correlationId;
        console.log('Message queued with CorrelationId:', correlationId);

        const userResult = await pollResult(correlationId);
        console.log('User message result:', userResult);

        if (state.currentChat && state.currentChat.id === chatId) {
          const updatedMessages = [
            ...state.currentChat.messages,
            { content: message, timestamp: new Date().toISOString(), isUser: true },
          ];
          commit('setCurrentChat', {
            ...state.currentChat,
            messages: updatedMessages,
          });
        }

        const llmResult = await pollResult(correlationId);
        console.log('LLM response result:', llmResult);

        if (state.currentChat && state.currentChat.id === chatId) {
          const updatedMessages = [
            ...state.currentChat.messages,
            { content: llmResult.response, timestamp: new Date().toISOString(), isUser: false, emotion: llmResult.detectedEmotion },
          ];
          commit('setCurrentChat', {
            ...state.currentChat,
            messages: updatedMessages,
          });
        }

        return { response: llmResult.response, detectedEmotion: llmResult.detectedEmotion };
      } catch (error) {
        console.error('Error sending message:', error.message);
        throw error;
      }
    },
  },
});