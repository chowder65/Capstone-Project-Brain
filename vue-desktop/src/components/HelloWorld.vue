<template>
  <div id="app">
    <div class="loginBox">
      <div class="inner">
        <!-- Sign In / Register view -->
        <div class="signIn" v-if="showLogin && signIn">
          <div class="top">
            <img
              class="logo"
              src="https://res.cloudinary.com/dc3c8nrut/image/upload/v1685298768/logo-placeholder_l3yodl.png"
            />
            <div class="title">Sign in</div>
            <div class="subtitle">
              Don't have an account?
              <span class="subtitle-action" @click="signIn = !signIn">
                Create Account
              </span>
            </div>
          </div>
          <form @submit.prevent="onLoginSuccess">
            <div class="form">
              <input
                required
                aria-required="true"
                aria-invalid="false"
                aria-label="E-mail"
                type="email"
                pattern="^[\w\.-]+@[\w\.-]+\.\w+$"
                class="w100"
                :class="{ invalid: email.error }"
                ref="email"
                placeholder="Email"
                autofocus
                @blur="validateEmail"
                @keydown="validateEmail"
                v-model="email.value"
              />

              <input
                required
                aria-required="true"
                type="password"
                class="w100"
                :class="{ invalid: password.error }"
                placeholder="Password"
                v-model="password.value"
                @blur="validatePassword"
                @keydown="validatePassword"
              />
            </div>

            <input
              type="submit"
              value="Submit"
              class="action"
              :class="{ 'action-disabled': !loginValid }"
            />
          </form>
        </div>

        <div class="register" v-else-if="showLogin">
          <div class="top">
            <img
              class="logo"
              src="https://res.cloudinary.com/dc3c8nrut/image/upload/v1685298768/logo-placeholder_l3yodl.png"
            />
            <div class="title">Create an Account</div>
            <div class="subtitle">
              Already have an account?
              <span class="subtitle-action" @click="signIn = !signIn">
                Sign In
              </span>
            </div>
          </div>

          <div class="form">
            <input
              type="text"
              placeholder="First name"
              autofocus
              v-model="firstName"
              class="w100"
            />

            <input
              type="text"
              placeholder="Last name"
              v-model="lastName"
              class="w100"
            />

            <input
              type="text"
              class="w100"
              placeholder="Email"
              v-model="email.value"
            />
            <input
              type="password"
              class="w100"
              placeholder="Password"
              v-model="password.value"
            />
          </div>

          <button class="action" :class="{ 'action-disabled': !registerValid }">
            Create Account
          </button>
        </div>

        <!-- ChatInterface will be conditionally rendered after login -->
        <ChatInterface v-if="showChatInterface" />
      </div>
    </div>
  </div>
</template>

<script>
// Import the ChatInterface component
import ChatInterface from '@/components/ChatInterface.vue'; // Adjust the path if necessary

export default {
  data() {
    return {
      emailRegex: /^[\w.-]+@[\w.-]+\.\w+$/,
      passwordRegex: /^(?=.*[0-9])(?=.*[!@#$%^&*])[a-zA-Z0-9!@#$%^&*]{8,}$/,

      firstName: "",
      lastName: "",

      password: {
        value: "",
        error: false
      },

      email: {
        value: "",
        error: false
      },

      signIn: true,
      showLogin: true, // Controls visibility of login/register page
      showChatInterface: false // Controls visibility of the ChatInterface
    };
  },

  components: {
    // Register the ChatInterface component here
    ChatInterface
  },

  methods: {
    validateEmail() {
      this.email.error = this.email.value === "";
    },

    validatePassword() {
      this.password.error = this.password.value === "";
    },

    // Method to handle successful login
    async onLoginSuccess() {
      if (!this.loginValid) {
        alert("Please check your email and password fields.");
        return;
      }

      try {
        // Make a POST request to the login endpoint
        const response = await fetch('http://localhost:5000/User/LogIn', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            userName: this.email.value, // Assuming email is used as the username
            password: this.password.value,
          }),
        });

        // Check if the response is successful (status code 200)
        if (response.ok) {
          const data = await response.json();
          if (data.token) {
            // Save the token to localStorage or a Vuex store
            localStorage.setItem('token', data.token);

            // Hide login/register page and show ChatInterface
            this.showLogin = false;
            this.showChatInterface = true;
          } else {
            alert('Login failed: No token received.');
          }
        } else {
          // Handle login failure
          const errorData = await response.json();
          alert(`Login failed: ${errorData.message || 'Invalid credentials'}`);
        }
      } catch (error) {
        console.error('Error during login:', error);
        alert('An error occurred during login. Please try again.');
      }
    }
  },

  computed: {
    validFirstName() {
      return this.firstName.length > 0;
    },

    validLastName() {
      return this.lastName.length > 0;
    },

    emailValid() {
      return this.emailRegex.test(this.email.value);
    },

    passwordValid() {
      return this.password.value.length > 0;
    },

    loginValid() {
      return this.emailValid && this.passwordValid;
    },

    registerValid() {
      return (
        this.emailValid &&
        this.passwordValid &&
        this.validFirstName &&
        this.validLastName
      );
    }
  }
};
</script>

<style lang="scss">
/* Your existing styles remain the same */
</style>
