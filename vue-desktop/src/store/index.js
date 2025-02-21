import { createStore } from 'vuex';
const { ipcRenderer } = window.require('electron');

export default createStore({
  state: {
    authToken: null
  },
  mutations: {
    setAuthToken(state, token) {
      state.authToken = token;
    }
  },
  actions: {
    async loadAuthToken({ commit }) {
      const token = await ipcRenderer.invoke('store:get', 'authToken');
      commit('setAuthToken', token);
    },
    async saveAuthToken({ commit }, token) {
      await ipcRenderer.invoke('store:set', 'authToken', token);
      commit('setAuthToken', token);
    }
  },
  getters: {
    authToken: (state) => state.authToken
  }
});
