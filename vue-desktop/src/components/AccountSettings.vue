<template>
  <div class="max-w-md mx-auto p-6 bg-gray-900 rounded-lg shadow-md text-green-100">
    <h2 class="text-2xl font-bold mb-4">{{ user.firstname }} {{ user.lastname }}</h2>
    <p class="text-green-200">Email: {{ user.email }}</p>
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

<style scoped>
div {
  max-width: 28rem;
  margin: 0 auto;
  padding: 1.5rem;
  background: #1f2937;
  border-radius: 0.5rem;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
  color: #86efac;
  overflow: hidden; /* Contain text */
}

h2 {
  font-size: 1.5rem;
  font-weight: bold;
  margin-bottom: 1rem;
}

p {
  color: #bbf7d0;
}
</style>