<template>
  <div class="chat-container bg-gray-900 text-green-100">
    <div :class="['sidebar', { collapsed: isCollapsed }]">
      <button class="collapse-btn" @click="toggleSidebar">
        {{ isCollapsed ? '>' : '<' }}
      </button>
      <h3 v-if="!isCollapsed" class="text-xl font-semibold mb-4">Chats</h3>
      <ul v-if="!isCollapsed" class="space-y-2">
        <li
          v-for="chat in chats"
          :key="chat.id"
          @click="selectChat(chat)"
          class="p-2 rounded-md hover:bg-gray-700 cursor-pointer"
        >
          {{ chat.chatName }}
        </li>
      </ul>
      <input
        v-if="!isCollapsed"
        v-model="newChatName"
        placeholder="New Chat Name"
        class="w-full p-2 mt-4 bg-gray-800 border border-gray-700 rounded-md text-green-100 placeholder-gray-400 box-border"
      />
      <button
        v-if="!isCollapsed"
        @click="createChat"
        class="w-full mt-2 p-2 bg-green-600 text-white rounded-md hover:bg-green-700"
      >
        Add Chat
      </button>
      <router-link
        v-if="!isCollapsed"
        to="/settings"
        class="settings-btn mt-auto block text-center p-2 bg-gray-700 rounded-md hover:bg-gray-600"
      >
        ⚙️ Account Settings
      </router-link>
    </div>

    <div class="chat-window bg-gray-800">
      <div v-if="currentChat" class="flex flex-col h-full">
        <h3 class="text-xl font-semibold mb-4 px-4">{{ currentChat.chatName }}</h3>
        <div class="messages flex-1 overflow-y-auto p-4 space-y-4">
          <div
            v-for="message in currentChat.messages"
            :key="message.timestamp"
            class="message relative p-3 max-w-[60%] rounded-xl"
            :class="{
              'self-end bg-green-600 text-white': message.isUser,
              'self-start bg-gray-700 text-green-100': !message.isUser
            }"
          >
            <p class="m-0">{{ message.content }}</p>
            <span
              class="absolute bottom-0 w-0 h-0 border-t-[10px] border-b-[10px]"
              :class="{
                'right-[-10px] border-l-[10px] border-l-green-600 border-t-transparent border-b-transparent': message.isUser,
                'left-[-10px] border-r-[10px] border-r-gray-700 border-t-transparent border-b-transparent': !message.isUser
              }"
            ></span>
          </div>
        </div>
        <div class="input-box flex gap-4 p-4">
          <input
            v-model="newMessage"
            @keyup.enter="sendMessage"
            placeholder="Type a message"
            class="flex-1 p-3 bg-gray-900 border border-gray-700 rounded-md text-green-100 placeholder-gray-400 box-border"
          />
          <button
            @click="sendMessage"
            class="p-3 bg-green-600 text-white rounded-md hover:bg-green-700"
          >
            Send
          </button>
        </div>
      </div>
      <div v-else class="no-chat-selected flex-1 flex items-center justify-center text-gray-400 bg-gray-800">
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
      isCollapsed: false,
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
        console.log('Creating chat with name:', this.newChatName);
        const chatId = await this.$store.dispatch('createChat', this.newChatName);
        console.log('Created chat ID:', chatId);
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
.chat-container {
  display: flex;
  height: 100vh;
  background: #1f2937;
  color: #86efac;
}

.sidebar {
  width: 250px;
  background: #111827;
  padding: 10px;
  transition: width 0.3s;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.sidebar.collapsed {
  width: 50px;
}

.collapse-btn {
  background: none;
  border: none;
  color: #86efac;
  font-size: 18px;
  cursor: pointer;
}

h3 {
  font-size: 1.25rem;
  font-weight: 600;
  margin-bottom: 1rem;
  padding-left: 1rem;
  padding-right: 1rem;
}

ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

li {
  padding: 0.5rem;
  border-radius: 0.375rem;
  cursor: pointer;
}

li:hover {
  background: #374151;
}

input {
  width: 100%;
  padding: 0.5rem;
  margin-top: 1rem;
  background: #374151;
  border: 1px solid #4b5563;
  border-radius: 0.375rem;
  color: #86efac;
  box-sizing: border-box;
}

input::placeholder {
  color: #9ca3af;
}

button {
  width: 100%;
  padding: 0.5rem;
  margin-top: 0.5rem;
  background: #16a34a;
  color: white;
  border: none;
  border-radius: 0.375rem;
  cursor: pointer;
}

button:hover {
  background: #15803d;
}

.settings-btn {
  margin-top: auto;
  text-decoration: none;
  color: #86efac;
  background: #4b5563;
  padding: 0.5rem;
  text-align: center;
  border-radius: 0.375rem;
}

.settings-btn:hover {
  background: #374151;
}

.chat-window {
  flex: 1;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  background: #374151;
  overflow: hidden;
}

.messages {
  flex-grow: 1;
  overflow-y: auto; /* Scrollable */
  padding: 1rem;
}

.message {
  position: relative;
  margin-bottom: 1rem;
  padding: 0.75rem;
  border-radius: 0.75rem; /* Slightly larger for iMessage feel */
  max-width: 60%;
  box-sizing: border-box;
}

.user-message {
  align-self: flex-end;
  background: #16a34a;
  color: white;
}

.user-message::before {
  content: '';
  position: absolute;
  bottom: 0;
  right: -10px;
  width: 0;
  height: 0;
  border-top: 10px solid transparent;
  border-bottom: 10px solid transparent;
  border-left: 10px solid #16a34a;
}

.ai-message {
  align-self: flex-start;
  background: #4b5563;
  color: #86efac;
}

.ai-message::before {
  content: '';
  position: absolute;
  bottom: 0;
  left: -10px;
  width: 0;
  height: 0;
  border-top: 10px solid transparent;
  border-bottom: 10px solid transparent;
  border-right: 10px solid #4b5563;
}

p {
  margin: 0;
}

.input-box {
  display: flex;
  gap: 0.625rem;
  padding-top: 0.625rem;
  padding-left: 1rem;
  padding-right: 1rem;
}

.input-box input {
  flex-grow: 1;
  padding: 0.75rem;
  margin: 0;
  background: #1f2937;
}

.input-box button {
  padding: 0.75rem;
  margin: 0;
}

.no-chat-selected {
  text-align: center;
  margin-top: 1.25rem;
  color: #9ca3af;
  flex-grow: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #374151;
}
</style>