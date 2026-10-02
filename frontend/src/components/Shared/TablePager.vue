<script setup>
// "Show [10] entries · 1–10 of 45   Prev 1 2 3 … 5 Next" under a table.
// The page owns the rows; this only tracks the page and page size:
//   <TablePager :total="rows.length" v-model:page="page" v-model:page-size="pageSize" />
//   rows.slice((page - 1) * pageSize, page * pageSize)
import { computed, watch } from 'vue'

const props = defineProps({
  total: { type: Number, required: true },
  page: { type: Number, default: 1 },
  pageSize: { type: Number, default: 10 },
  sizes: { type: Array, default: () => [10, 20, 30, 50] },
})
const emit = defineEmits(['update:page', 'update:pageSize'])

const totalPages = computed(() => Math.max(1, Math.ceil(props.total / props.pageSize)))

// Back to a page that exists when the list shrinks (search, filters)
watch([() => props.total, () => props.pageSize], () => {
  if (props.page > totalPages.value) emit('update:page', totalPages.value)
})

const rangeLabel = computed(() => {
  if (props.total === 0) return '0 of 0'
  const start = (props.page - 1) * props.pageSize + 1
  return `${start}–${Math.min(props.page * props.pageSize, props.total)} of ${props.total}`
})

// 1 … 4 5 6 … 12
const pageNumbers = computed(() => {
  const last = totalPages.value, cur = props.page
  if (last <= 7) return Array.from({ length: last }, (_, i) => i + 1)
  const pages = [1]
  if (cur > 3) pages.push('...')
  for (let n = Math.max(2, cur - 1); n <= Math.min(last - 1, cur + 1); n++) pages.push(n)
  if (cur < last - 2) pages.push('...')
  pages.push(last)
  return pages
})

function go(n) {
  emit('update:page', Math.min(Math.max(1, n), totalPages.value))
}
function setSize(e) {
  emit('update:pageSize', Number(e.target.value))
  emit('update:page', 1)
}
</script>

<template>
  <div v-if="total > 0" class="flex flex-wrap items-center justify-between gap-3 px-5 py-3 border-t border-slate-200">
    <div class="flex items-center gap-2 text-xs text-slate-500">
      <span>Show</span>
      <select :value="pageSize" @change="setSize" class="rounded-lg border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500" aria-label="Rows per page">
        <option v-for="n in sizes" :key="n" :value="n">{{ n }}</option>
      </select>
      <span>entries · {{ rangeLabel }}</span>
    </div>
    <div class="flex items-center gap-1">
      <button @click="go(page - 1)" :disabled="page === 1" class="px-2.5 py-1.5 rounded-lg text-xs font-medium text-slate-500 hover:bg-slate-100 disabled:opacity-40 disabled:hover:bg-transparent">Prev</button>
      <template v-for="(n, i) in pageNumbers" :key="i">
        <span v-if="n === '...'" class="px-2 text-xs text-slate-400">...</span>
        <button v-else @click="go(n)" class="min-w-[30px] px-2.5 py-1.5 rounded-lg text-xs font-medium"
          :class="n === page ? 'bg-emerald-600 text-white' : 'text-slate-600 hover:bg-slate-100'">{{ n }}</button>
      </template>
      <button @click="go(page + 1)" :disabled="page === totalPages" class="px-2.5 py-1.5 rounded-lg text-xs font-medium text-slate-500 hover:bg-slate-100 disabled:opacity-40 disabled:hover:bg-transparent">Next</button>
    </div>
  </div>
</template>
