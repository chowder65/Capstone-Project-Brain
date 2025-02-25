<template>
  <div>
    <h1>{{ isLogin ? 'Login' : 'Signup' }}</h1>
    <component :is="isLogin ? 'LoginComponent' : 'SignupComponent'" @toggle="isLogin = !isLogin" />
  </div>
</template>

<script>
import LoginComponent from '../components/Login.vue';
import SignupComponent from '../components/Signup.vue';

export default {
  name: 'AuthView',
  components: { LoginComponent, SignupComponent },
  data() {
    return { isLogin: true };
  },
};
</script>