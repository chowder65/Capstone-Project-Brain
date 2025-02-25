<template>
  <form @submit.prevent="signup">
    <input v-model="form.useremail" type="email" placeholder="Email" required />
    <input v-model="form.userpassword" type="password" placeholder="Password" required />
    <input v-model="form.firstname" placeholder="First Name" required />
    <input v-model="form.lastname" placeholder="Last Name" required />
    <button type="submit">Signup</button>
    <button type="button" @click="$emit('toggle')">Switch to Login</button>
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