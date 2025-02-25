<template>
  <div>
    <h2>{{ user.firstname }} {{ user.lastname }}</h2>
    <p>Email: {{ user.email }}</p>
    <!-- Add form to update settings if needed -->
  </div>
</template>

<script>
export default {
    name: 'AccountSettings',
  computed: {
    user() {
      return this.$store.state.user || {};
    },
  },
};
</script>