<template>
  <div>
    <h1>Account Settings</h1>
    <AccountSettings />

    <button class="logout-btn" @click="logout">Logout</button>

    <button @click="$router.push('/')">Back to Chat</button>
  </div>
</template>

<script>
import AccountSettings from '../components/AccountSettings.vue';

export default {
  name: 'SettingsView',
  components: { AccountSettings },
  methods: {
    logout() {
      this.$store.commit('logout');
      this.$router.push('/auth');
    },
  },
};
</script>

<style scoped>
.logout-btn {
  background: red;
  color: white;
  border: none;
  padding: 10px 15px;
  margin-top: 10px;
  cursor: pointer;
  border-radius: 4px;
}
.logout-btn:hover {
  background: darkred;
}
</style>
