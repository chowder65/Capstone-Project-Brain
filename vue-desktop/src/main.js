import { createApp } from 'vue';
import App from './App.vue';
import router from './router';
import store from './store';
import axios from 'axios';

axios.defaults.baseURL = 'http://localhost:5168';

const app = createApp(App);
app.use(router);
app.use(store);
app.mount('#app');