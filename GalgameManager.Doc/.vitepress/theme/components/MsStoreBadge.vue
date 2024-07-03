<!-- MsStoreBadge.vue -->
<template>
    <div id="ms-store-badge-container"></div>
  </template>
  
  <script>
  export default {
    mounted() {
      const script = document.createElement('script');
      script.type = 'module';
      script.src = 'https://get.microsoft.com/badge/ms-store-badge.bundled.js';
      script.onload = () => {
        const badge = document.createElement('ms-store-badge');
        badge.setAttribute('productid', '9p9cbkd5hr3w');
        badge.setAttribute('productname', 'PotatoVN');
        badge.setAttribute('window-mode', 'full');
        badge.setAttribute('theme', 'auto');
        badge.setAttribute('language', 'zh-cn');
        badge.setAttribute('animation', 'off');
        this.$el.appendChild(badge);
      };
      document.head.appendChild(script);
    }
  }
  </script>