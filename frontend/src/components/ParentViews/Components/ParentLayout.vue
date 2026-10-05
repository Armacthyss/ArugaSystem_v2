<!--
  ParentLayout.vue — the same page frame on every parent page, so the side
  margins are equal on a phone (16 px) and the content never runs off the
  screen. On a computer the content is centered and capped at 1152 px.
-->
<template>
  <div class="min-h-screen w-full overflow-x-hidden bg-slate-50 font-sans antialiased text-slate-900">
    <div class="mx-auto w-full max-w-6xl px-4 py-4 sm:px-6 sm:py-6 lg:px-8">
      <slot />
    </div>
  </div>
</template>
