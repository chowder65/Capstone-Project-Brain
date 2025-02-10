import { createRouter, createWebHistory } from 'vue-router';
import Chat from '@/components/ChatInterface.vue';
import HelloWorld from '@/components/HelloWorld.vue';

const routes = [
  {
    path: '/chat',
    name: 'Chat',
    component: Chat,
  },
  {
    path: '/home',
    name: 'Home',
    component: HelloWorld,
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
});

export default router;
