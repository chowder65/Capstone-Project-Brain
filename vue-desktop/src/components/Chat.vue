<template>
  <div>
    <div>
      <h3>Chats</h3>
      <ul>
        <li v-for="chat in chats" :key="chat.id" @click="selectChat(chat)">
          {{ chat.chatName }}
        </li>
      </ul>
      <input v-model="newChatName" placeholder="New Chat Name" />
      <button @click="createChat">Add Chat</button>
    </div>
    <div v-if="currentChat">
      <h3>{{ currentChat.chatName }}</h3>
      <div v-for="message in currentChat.messages" :key="message.timestamp">
        <p>{{ message.isUser ? 'You' : 'AI' }}: {{ message.content }}</p>
      </div>
      <input v-model="newMessage" @keyup.enter="sendMessage" placeholder="Type a message" />
      <button @click="sendMessage">Send</button>
    </div>
  </div>
</template>

<script>
export default {
  name: 'ChatComponent',
  data() {
    return {
      newChatName: '',
      newMessage: '',
    };
  },
  computed: {
    chats() {
      return this.$store.state.chats || [];
    },
    currentChat() {
      return this.$store.state.currentChat;
    },
  },
  mounted() {
    this.$store.dispatch('fetchChats');
  },
  methods: {
    async createChat() {
      if (this.newChatName) {
        const chatId = await this.$store.dispatch('createChat', this.newChatName);
        await this.$store.dispatch('fetchChatHistory', chatId);
        this.newChatName = '';
      }
    },
    async selectChat(chat) {
        console.log('Selecting chat:', chat);
        console.log('Chat ID:', chat.id);
      await this.$store.dispatch('fetchChatHistory', chat.id);
    },
    async sendMessage() {
      if (this.newMessage && this.currentChat) {
        await this.$store.dispatch('sendMessage', {
          chatId: this.currentChat.id,
          message: this.newMessage,
        });
        this.newMessage = '';
      }
    },
  },
};
</script>

<style scoped>
ul {
  list-style: none;
  padding: 0;
}
li {
  cursor: pointer;
}
</style>