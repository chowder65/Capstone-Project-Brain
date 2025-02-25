<template>
  <div>
    <h1>Account Settings</h1>
    <AccountSettings />
    <button @click="$router.push('/')">Back to Chat</button>
  </div>
</template>

<script>
import AccountSettings from '../components/AccountSettings.vue';

export default {
  name: 'SettingsView',
  components: { AccountSettings },
};
</script>