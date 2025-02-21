const { defineConfig } = require('@vue/cli-service');
const path = require('path');

module.exports = defineConfig({
  configureWebpack: {
    resolve: {
      fallback: {
        fs: false, 
        path: require.resolve('path-browserify'), 
      },
    },
  },
});