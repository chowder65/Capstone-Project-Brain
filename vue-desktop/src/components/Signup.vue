<template>
  <form @submit.prevent="signup" class="max-w-md mx-auto p-6 bg-gray-900 rounded-lg shadow-md text-green-100">
    <div class="mb-4">
      <input
        v-model="form.useremail"
        type="email"
        placeholder="Email"
        required
        class="w-full p-3 bg-gray-800 border border-gray-700 rounded-md focus:outline-none focus:ring-2 focus:ring-green-500 text-green-100 placeholder-gray-400 box-border"
      />
    </div>
    <div class="mb-4">
      <input
        v-model="form.userpassword"
        type="password"
        placeholder="Password"
        required
        class="w-full p-3 bg-gray-800 border border-gray-700 rounded-md focus:outline-none focus:ring-2 focus:ring-green-500 text-green-100 placeholder-gray-400 box-border"
      />
    </div>
    <div class="mb-4">
      <input
        v-model="form.firstname"
        placeholder="First Name"
        required
        class="w-full p-3 bg-gray-800 border border-gray-700 rounded-md focus:outline-none focus:ring-2 focus:ring-green-500 text-green-100 placeholder-gray-400 box-border"
      />
    </div>
    <div class="mb-6">
      <input
        v-model="form.lastname"
        placeholder="Last Name"
        required
        class="w-full p-3 bg-gray-800 border border-gray-700 rounded-md focus:outline-none focus:ring-2 focus:ring-green-500 text-green-100 placeholder-gray-400 box-border"
      />
    </div>
    <div class="flex space-x-4">
      <button
        type="submit"
        class="flex-1 bg-green-600 text-white p-3 rounded-md hover:bg-green-700 transition-colors"
      >
        Signup
      </button>
      <button
        type="button"
        @click="$emit('toggle')"
        class="flex-1 bg-gray-700 text-green-100 p-3 rounded-md hover:bg-gray-600 transition-colors"
      >
        Switch to Login
      </button>
    </div>
  </form>
</template>

<script>
export default {
  name: 'SignupComponent',
  data() {
    return {
      form: { useremail: '', userpassword: '', firstname: '', lastname: '' },
    };
  },
  methods: {
    async signup() {
      await this.$store.dispatch('signup', this.form);
      this.$router.push('/auth');
    },
  },
};
</script>

<style scoped>
form {
  max-width: 28rem;
  margin: 0 auto;
  padding: 1.5rem;
  background: #1f2937;
  border-radius: 0.5rem;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
  color: #86efac;
  overflow: hidden; /* Contain children */
}

input {
  width: 100%;
  padding: 0.75rem;
  margin-bottom: 1rem;
  background: #374151;
  border: 1px solid #4b5563;
  border-radius: 0.375rem;
  color: #86efac;
  outline: none;
  box-sizing: border-box; /* Fix overflow */
}

input::placeholder {
  color: #9ca3af;
}

input:focus {
  border-color: #22c55e;
  box-shadow: 0 0 0 2px rgba(34, 197, 94, 0.5);
}

button {
  width: 48%;
  padding: 0.75rem;
  border: none;
  border-radius: 0.375rem;
  cursor: pointer;
  transition: background-color 0.3s;
}

button[type="submit"] {
  background: #16a34a;
  color: white;
}

button[type="submit"]:hover {
  background: #15803d;
}

button[type="button"] {
  background: #4b5563;
  color: #86efac;
}

button[type="button"]:hover {
  background: #374151;
}

.flex {
  display: flex;
  gap: 1rem;
}
</style>