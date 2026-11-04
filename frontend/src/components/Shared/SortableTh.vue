<script setup>
// A column heading you can click to sort the table (see utils/tableSort.js).
// Classes on it go on the <th>, so it looks like the page's other headings.
import { computed } from 'vue'
import { ArrowDown, ArrowUp, ChevronsUpDown } from 'lucide-vue-next'

const props = defineProps({
  sort: { type: Object, required: true },
  column: { type: String, required: true },
})

const active = computed(() => props.sort.sortKey.value === props.column)
const ascending = computed(() => props.sort.sortDir.value === 'asc')
const icon = computed(() => (!active.value ? ChevronsUpDown : ascending.value ? ArrowUp : ArrowDown))
</script>

<template>
  <th :aria-sort="!active ? 'none' : ascending ? 'ascending' : 'descending'">
    <button
      type="button"
      class="group inline-flex items-center gap-1 hover:text-emerald-700 focus:outline-none focus-visible:underline"
      :class="active ? 'text-emerald-700' : ''"
      style="font: inherit; letter-spacing: inherit; text-transform: inherit;"
      title="Sort by this column"
      @click="sort.toggleSort(column)"
    >
      <slot />
      <component :is="icon" class="h-3 w-3 shrink-0 print:hidden" :class="active ? '' : 'opacity-40 group-hover:opacity-100'" aria-hidden="true" />
    </button>
  </th>
</template>
