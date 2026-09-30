<template>
  <router-view />
  <!-- Aruga's confirm / message pop-up (utils/dialog.js) -->
  <AppDialog />
</template>

<script setup>
import AppDialog from '@/components/Shared/AppDialog.vue'
</script>
