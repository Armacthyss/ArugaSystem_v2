<!--
  PageGuide.vue — the "How to use this page" card under the top bar of every
  Staff / Nurse and Admin / Doctor page (text in utils/pageGuides.js).
  "Hide tips" hides it on that page for this browser; the Help button in the
  top bar (StaffTopbar / AppHeader) shows it again.
-->
<template>
  <div v-if="guide?.steps && !isGuideHidden(route.path)" class="print:hidden" :class="inset">
    <section class="rounded-2xl border border-emerald-100 bg-emerald-50/60 px-4 py-3.5" aria-label="How to use this page">
      <div class="flex items-center justify-between gap-3">
        <p class="flex items-center gap-2 text-[13px] font-semibold text-emerald-900">
          <Lightbulb :size="16" class="text-emerald-700 shrink-0" /> How to use this page
        </p>
        <button @click="setGuideHidden(route.path, true)"
          class="flex items-center gap-1 rounded-lg px-2 py-1 text-[12px] font-medium text-emerald-800 hover:bg-emerald-100/70">
          <X :size="14" /> Hide tips
        </button>
      </div>
      <ol class="mt-3 grid gap-2.5" :class="COLUMNS[guide.steps.length] || COLUMNS[4]">
        <li v-for="(s, i) in guide.steps" :key="s.title" class="flex gap-2.5 rounded-xl border border-emerald-100 bg-white px-3 py-2.5">
          <span class="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-emerald-700 text-[12px] font-bold text-white">{{ i + 1 }}</span>
          <div class="min-w-0">
            <p class="text-[12.5px] font-semibold text-stone-800">{{ s.title }}</p>
            <p class="text-[12px] leading-snug text-stone-600">{{ s.text }}</p>
          </div>
        </li>
      </ol>
      <p v-if="guide.tip" class="mt-2.5 text-[12px] text-emerald-900"><span class="font-semibold">Tip:</span> {{ guide.tip }}</p>
    </section>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { Lightbulb, X } from 'lucide-vue-next'
import { guideFor, isGuideHidden, setGuideHidden } from '@/utils/pageGuides'

// Spacing around the card, to line up with the page's own padding
defineProps({ inset: { type: String, default: 'px-4 md:px-8 pt-5' } })

const route = useRoute()
const guide = computed(() => guideFor(route.path))

const COLUMNS = {
  2: 'sm:grid-cols-2',
  3: 'md:grid-cols-3',
  4: 'sm:grid-cols-2 xl:grid-cols-4',
}
</script>
