<template>
  <div>
    <h1>AI Chatbot</h1>
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