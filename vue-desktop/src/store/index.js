import { createStore } from 'vuex';
import axios from 'axios';

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
      const url = `/User/LogIn?email=${encodeURIComponent(credentials.useremail)}&password=${encodeURIComponent(credentials.userpassword)}`;
      const response = await axios.post(url);
      commit('setToken', response.data.token);
      commit('setUser', { email: credentials.useremail, firstname: '', lastname: '' });
    },
    async signup(_, userData) {
      await axios.post('/User/Create', {
        email: userData.useremail,
        password: userData.userpassword,
        firstName: userData.firstname,
        lastName: userData.lastname,
      });
    },
    async fetchChats({ commit, state }) {
      const response = await axios.get('/User/Chats', {
        headers: { Authorization: `Bearer ${state.token}` },
      });
      const chats = (response.data || []).map(chat => ({
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
        console.log('Store creating chat with name:', chatName);

        const response = await axios.post(
          '/User/StartChat',
          { chatName: chatName },
          {
            headers: { 
              Authorization: `Bearer ${state.token}`,
              'Content-Type': 'application/json' 
            }
          }
        );
        const chatId = response.data.chatId;
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
        console.error('Error creating chat:', error.response ? error.response.data : error.message);
        throw error;
      }
    },
    async fetchChatHistory({ commit, state }, chatId) {
      const id = typeof chatId === 'string' ? chatId : chatId.toString(); 
      console.log('Fetching history for chatId:', id);
      const response = await axios.get(`/User/Chat/History?chatId=${chatId}`, {
        headers: { Authorization: `Bearer ${state.token}` },
      });
      const chat = {
        id: response.data.id,
        chatName: response.data.chatName, 
        messages: response.data.messages.map((msg, index) => ({
          content: msg.text,
          timestamp: msg.timestamp,
          isUser: index % 2 === 0,
        })),
      };
      commit('setCurrentChat', chat);
    },
    async sendMessage({ state, dispatch }, { chatId, message }) {
      try {
        await axios.post(
          '/User/Chat/AddMessage',
          { chatId, message },
          { headers: { Authorization: `Bearer ${state.token}` } }
        );
        const llmResponse = await axios.post(
          'http://localhost:1234/v1/chat/completions',
          {
            messages: [{ role: 'system', content: '' }, { role: 'user', content: message }],
          },
          { headers: { 'Content-Type': 'application/json' } }
        );
        const aiMessage = llmResponse.data.choices[0].message.content;
        await axios.post(
          '/User/Chat/AddMessage',
          { chatId, message: aiMessage },
          { headers: { Authorization: `Bearer ${state.token}` } }
        );
        await dispatch('fetchChatHistory', chatId);
      } catch (error) {
        console.error('Error sending message:', error);
      }
    },
  },
});