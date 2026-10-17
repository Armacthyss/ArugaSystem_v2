<template>
  <router-view />
  <!-- Aruga's confirm / message pop-up (utils/dialog.js) -->
  <AppDialog />
  <!-- Staff are signed out after 15 minutes of no activity -->
  <IdleSignOut />
</template>

<script setup>
import AppDialog from '@/components/Shared/AppDialog.vue'
import IdleSignOut from '@/components/Shared/IdleSignOut.vue'
</script>
