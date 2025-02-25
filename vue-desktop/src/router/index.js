import { createRouter, createWebHistory } from 'vue-router';
import HomeView from '../views/Home.vue';
import AuthView from '../views/Auth.vue';
import SettingsView from '../views/Settings.vue';

const routes = [
  { path: '/', name: 'Home', component: HomeView, meta: { requiresAuth: true } },
  { path: '/auth', name: 'Auth', component: AuthView },
  { path: '/settings', name: 'Settings', component: SettingsView, meta: { requiresAuth: true } },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

router.beforeEach((to, from, next) => {
  const isAuthenticated = !!localStorage.getItem('token');
  console.log('Navigating to:', to.path, 'Authenticated:', isAuthenticated);
  if (to.meta.requiresAuth && !isAuthenticated) {
    console.log('Redirecting to /auth due to lack of token');
    next('/auth');
  } else {
    next();
  }
});

export default router;