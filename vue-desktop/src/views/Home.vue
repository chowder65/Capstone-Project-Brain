<template>
  <div>
    <h1>AI Chatbot</h1>
    <button @click="$router.push('/settings')">Account Settings</button>
    <button @click="$store.commit('logout'); $router.push('/auth')">Logout</button>
    <ChatComponent />
  </div>
</template>

<script>
import ChatComponent from '../components/Chat.vue';

export default {
  name: 'HomeView',
  components: { ChatComponent },
  mounted() {
    this.$store.dispatch('fetchChats');
  },
};
</script>