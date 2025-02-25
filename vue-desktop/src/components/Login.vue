<template>
  <form @submit.prevent="login">
    <input v-model="form.useremail" type="email" placeholder="Email" required />
    <input v-model="form.userpassword" type="password" placeholder="Password" required />
    <button type="submit">Login</button>
    <button type="button" @click="$emit('toggle')">Switch to Signup</button>
  </form>
</template>

<script>
export default {
  name: 'LoginComponent',
  data() {
    return {
      form: { useremail: '', userpassword: '' },
    };
  },
  methods: {
    async login() {
      try {
        await this.$store.dispatch('login', this.form);
        this.$router.push('/');
      } catch (error) {
        console.error('Login failed:', error);
      }
    },
  },
};
</script>