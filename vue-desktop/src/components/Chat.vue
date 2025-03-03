<template>
  <div class="chat-container">
    <!-- Sidebar -->
    <div :class="['sidebar', { collapsed: isCollapsed }]">
      <button class="collapse-btn" @click="toggleSidebar">
        {{ isCollapsed ? '>' : '<' }}
      </button>
      <h3 v-if="!isCollapsed">Chats</h3>
      <ul v-if="!isCollapsed">
        <li v-for="chat in chats" :key="chat.id" @click="selectChat(chat)">
          {{ chat.chatName }}
        </li>
      </ul>
      <input v-if="!isCollapsed" v-model="newChatName" placeholder="New Chat Name" />
      <button v-if="!isCollapsed" @click="createChat">Add Chat</button>

      <router-link v-if="!isCollapsed" to="/settings" class="settings-btn">⚙️ Account Settings</router-link>
    </div>

    <div class="chat-window">
      <div v-if="currentChat">
        <h3>{{ currentChat.chatName }}</h3>

        <div class="messages">
          <div
            v-for="message in currentChat.messages"
            :key="message.timestamp"
            class="message"
            :class="{ 'user-message': message.isUser, 'ai-message': !message.isUser }"
          >
            <p>{{ message.content }}</p>
          </div>
        </div>

        <div class="input-box">
          <input v-model="newMessage" @keyup.enter="sendMessage" placeholder="Type a message" />
          <button @click="sendMessage">Send</button>
        </div>
      </div>
      <div v-else class="no-chat-selected">
        <p>Select a chat to start messaging</p>
      </div>
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
      isCollapsed: false, // Sidebar collapse state
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
    toggleSidebar() {
      this.isCollapsed = !this.isCollapsed;
    },
    async createChat() {
      if (this.newChatName) {
        console.log('Creating chat with name:', this.newChatName); // Add this log
        const chatId = await this.$store.dispatch('createChat', this.newChatName);
        console.log('Created chat ID:', chatId); // Add this log
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
/* Chat Layout */
.chat-container {
  display: flex;
  height: 100vh;
}

/* Sidebar */
.sidebar {
  width: 250px;
  background: #2c3e50;
  color: white;
  padding: 10px;
  transition: width 0.3s;
  display: flex;
  flex-direction: column;
}
.sidebar.collapsed {
  width: 50px;
}
.collapse-btn {
  background: none;
  border: none;
  color: white;
  font-size: 18px;
  cursor: pointer;
}
ul {
  list-style: none;
  padding: 0;
}
li {
  cursor: pointer;
  padding: 5px;
}
.settings-btn {
  margin-top: auto;
  text-decoration: none;
  color: white;
  background: #34495e;
  padding: 8px;
  text-align: center;
  border-radius: 4px;
}
.settings-btn:hover {
  background: #1f2a36;
}

/* Chat Window */
.chat-window {
  flex: 1;
  padding: 20px;
  display: flex;
  flex-direction: column;
  background: #f9f9f9;
}

/* Messages */
.messages {
  flex-grow: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  padding: 10px;
}

/* User Messages (Align Right) */
.user-message {
  align-self: flex-end;
  background: #007bff;
  color: white;
  padding: 10px;
  border-radius: 10px;
  margin: 5px 0;
  max-width: 60%;
}

/* AI Messages (Align Left) */
.ai-message {
  align-self: flex-start;
  background: #e4e6eb;
  padding: 10px;
  border-radius: 10px;
  margin: 5px 0;
  max-width: 60%;
}

/* Input Box */
.input-box {
  display: flex;
  gap: 10px;
  padding-top: 10px;
}

input {
  flex-grow: 1;
  padding: 8px;
}

button {
  padding: 8px;
  cursor: pointer;
}

/* No Chat Selected Message */
.no-chat-selected {
  text-align: center;
  margin-top: 20px;
  color: gray;
}
</style>
