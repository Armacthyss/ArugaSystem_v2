<!--
  ChildTabs.vue — the row of children ("👧 Bianca  👶 Joaquin") under the
  navigation bar, replacing the old Family Profiles sidebar. The selected
  child's age and a "Health info" link sit on the right; the child's details
  open in a small window instead of filling every page.
-->
<template>
  <div v-if="children.length" class="mb-4 flex flex-wrap items-center justify-between gap-x-4 gap-y-2 sm:mb-6">
    <!-- One row that scrolls sideways when there are more children than fit:
         arrows at the ends and a soft fade show there's more, and the mouse
         wheel scrolls it too. (The "Search children" box finds anyone.) -->
    <div class="relative -mx-1 min-w-0 max-w-full">
      <div ref="rowRef" @scroll="updateEdges" @wheel="onWheel"
        class="no-scrollbar flex min-w-0 max-w-full gap-2 overflow-x-auto scroll-smooth px-1 py-1">
        <button
          v-for="child in children"
          :key="child.childID"
          :data-child="child.childID"
          type="button"
          @click="$emit('select-child', child)"
          :aria-pressed="isSelected(child)"
          class="flex shrink-0 items-center gap-2 rounded-full border-2 px-4 py-2 text-sm transition-colors sm:px-5 sm:text-base"
          :class="isSelected(child)
            ? 'border-emerald-700 bg-white font-bold text-slate-900 shadow-sm'
            : 'border-transparent bg-white/70 font-medium text-slate-500 hover:bg-white hover:text-slate-700'"
        >
          <span aria-hidden="true">{{ child.sex === 'Female' ? '👧' : '👶' }}</span>
          {{ child.firstName }}
        </button>
      </div>
      <template v-if="canLeft">
        <div class="pointer-events-none absolute inset-y-0 left-0 w-12 bg-gradient-to-r from-slate-50 to-transparent"></div>
        <button type="button" @click="scrollRow(-1)" aria-label="Show earlier children"
          class="absolute left-0 top-1/2 flex h-9 w-9 -translate-y-1/2 items-center justify-center rounded-full border border-slate-200 bg-white text-lg text-slate-600 shadow-md hover:bg-slate-50">‹</button>
      </template>
      <template v-if="canRight">
        <div class="pointer-events-none absolute inset-y-0 right-0 w-12 bg-gradient-to-l from-slate-50 to-transparent"></div>
        <button type="button" @click="scrollRow(1)" aria-label="Show more children"
          class="absolute right-0 top-1/2 flex h-9 w-9 -translate-y-1/2 items-center justify-center rounded-full border border-slate-200 bg-white text-lg text-slate-600 shadow-md hover:bg-slate-50">›</button>
      </template>
    </div>

    <p v-if="selectedChild" class="shrink-0 text-sm text-slate-500">
      <span v-if="ageText">{{ ageText }} old · </span>
      <button type="button" @click="openInfo" class="font-semibold text-emerald-700 hover:underline">Health info</button>
    </p>
  </div>

  <!-- Health info: a sheet from the bottom on a phone, a centered window on a computer -->
  <Teleport to="body">
    <Transition name="sheet">
      <div v-if="showInfo && selectedChild" class="fixed inset-0 z-300 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" @click.self="showInfo = false">
        <div class="flex max-h-[85vh] w-full flex-col overflow-hidden rounded-t-3xl bg-white shadow-2xl sm:max-w-lg sm:rounded-3xl" role="dialog" aria-modal="true" aria-labelledby="health-info-title">
          <div class="flex items-center gap-3 border-b border-slate-100 px-5 py-4 sm:px-6">
            <span class="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-emerald-50 text-2xl">{{ selectedChild.sex === 'Female' ? '👧' : '👶' }}</span>
            <div class="min-w-0 flex-1">
              <h2 id="health-info-title" class="truncate text-base font-bold text-slate-900">{{ fullName }}</h2>
              <p class="text-xs text-slate-500">Health info{{ selectedChild.familyNo ? ` · Family No. ${selectedChild.familyNo}` : '' }}</p>
            </div>
            <button type="button" @click="showInfo = false" aria-label="Close" class="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-slate-500 hover:bg-slate-100">✕</button>
          </div>

          <div class="flex-1 space-y-4 overflow-y-auto px-5 py-5 sm:px-6">
            <!-- Allergies and conditions first: what a parent may need to tell the nurse -->
            <div class="rounded-2xl px-4 py-3" :class="hasAlerts ? 'bg-red-50' : 'bg-slate-50'">
              <p class="text-xs font-semibold uppercase tracking-wide" :class="hasAlerts ? 'text-red-600' : 'text-slate-500'">Allergies and conditions</p>
              <p v-if="loadingDetails" class="mt-1 text-sm text-slate-400">Loading…</p>
              <template v-else>
                <p class="mt-1 text-sm text-slate-800"><span class="font-semibold">Allergies:</span> {{ details?.allergies || 'None recorded' }}</p>
                <p class="text-sm text-slate-800"><span class="font-semibold">Existing conditions:</span> {{ details?.existingConditions || 'None recorded' }}</p>
              </template>
            </div>

            <dl class="grid grid-cols-2 gap-3">
              <div v-for="item in facts" :key="item.label" class="rounded-2xl bg-slate-50 px-4 py-3" :class="item.wide ? 'col-span-2' : ''">
                <dt class="text-[11px] font-semibold uppercase tracking-wide text-slate-500">{{ item.label }}</dt>
                <dd class="mt-0.5 break-words text-sm font-semibold text-slate-800">{{ item.value }}</dd>
              </div>
            </dl>

            <p class="text-xs text-slate-500">To correct any of these details, please tell the health center staff on your next visit.</p>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup>
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import { format } from 'date-fns'
import { ageParts } from '@/utils/format'
import { barangayLabel } from '@/utils/barangays'
import api from '../Composables/api'

const props = defineProps({
  children: { type: Array, default: () => [] },
  selectedChild: { type: Object, default: null },
})
defineEmits(['select-child'])

const isSelected = child => props.selectedChild?.childID === child.childID

// ── Scrolling the row of children ──
const rowRef = ref(null)
const canLeft = ref(false)   // more names hidden on the left
const canRight = ref(false)  // more names hidden on the right
function updateEdges() {
  const el = rowRef.value
  if (!el) return
  canLeft.value = el.scrollLeft > 4
  canRight.value = el.scrollLeft + el.clientWidth < el.scrollWidth - 4
}
const scrollRow = dir => rowRef.value?.scrollBy({ left: dir * rowRef.value.clientWidth * 0.8 })
// A normal (up/down) mouse wheel scrolls the row sideways
function onWheel(e) {
  const el = rowRef.value
  if (!el || el.scrollWidth <= el.clientWidth || Math.abs(e.deltaX) > Math.abs(e.deltaY)) return
  e.preventDefault()
  el.scrollLeft += e.deltaY
}
// The chosen child (also when picked with "Search children") is scrolled into view
watch(() => [props.selectedChild?.childID, props.children.length], async () => {
  await nextTick()
  rowRef.value?.querySelector(`[data-child="${props.selectedChild?.childID}"]`)
    ?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
  updateEdges()
}, { immediate: true })
onMounted(() => { window.addEventListener('resize', updateEdges); updateEdges() })
onUnmounted(() => window.removeEventListener('resize', updateEdges))

// "6 days", "3 months", "1 year 2 months"
const ageText = computed(() => {
  const a = ageParts(props.selectedChild?.birthDate)
  if (!a) return ''
  const s = (n, w) => `${n} ${w}${n === 1 ? '' : 's'}`
  if (a.years) return a.months ? `${s(a.years, 'year')} ${s(a.months, 'month')}` : s(a.years, 'year')
  if (a.months) return s(a.months, 'month')
  return s(a.days, 'day')
})

const fullName = computed(() => {
  const c = props.selectedChild
  return c ? [c.firstName, c.middleName, c.lastName].filter(Boolean).join(' ') : ''
})

// Allergies and conditions are not in the parent dashboard answer, so they are
// loaded when the window opens (GET /api/Children/{id}, own children only).
const showInfo = ref(false)
const details = ref(null)
const loadingDetails = ref(false)

async function openInfo() {
  showInfo.value = true
  if (details.value?.childID === props.selectedChild?.childID) return
  loadingDetails.value = true
  try {
    details.value = (await api.get(`/Children/${props.selectedChild.childID}`)).data
  } catch {
    details.value = null
  } finally {
    loadingDetails.value = false
  }
}
watch(() => props.selectedChild?.childID, () => { showInfo.value = false })

const hasAlerts = computed(() => !!(details.value?.allergies || details.value?.existingConditions))

const facts = computed(() => {
  const c = props.selectedChild
  if (!c) return []
  const birth = c.birthDate ? format(new Date(c.birthDate), 'MMMM d, yyyy') : '—'
  return [
    { label: 'Birth date', value: birth },
    { label: 'Age', value: ageText.value || '—' },
    { label: 'Sex', value: c.sex || '—' },
    { label: 'Barangay', value: barangayLabel(c.barangay) },
    { label: 'Birth weight', value: c.birthWeight ? `${Number(c.birthWeight)} kg` : 'Not recorded' },
    { label: 'Birth height', value: c.birthHeight ? `${Number(c.birthHeight)} cm` : 'Not recorded' },
    { label: 'Place of birth', value: c.placeOfBirth || '—', wide: true },
    { label: `You are ${c.firstName}'s`, value: c.relationshipType || '—' },
    { label: 'Health center', value: c.healthCenter || 'Leveriza Health Center' },
    { label: 'Parents / guardians', value: c.guardians ? c.guardians.split('; ').join(', ') : '—', wide: true },
  ]
})
</script>

<style scoped>
.no-scrollbar { scrollbar-width: none; }
.no-scrollbar::-webkit-scrollbar { display: none; }
.sheet-enter-active, .sheet-leave-active { transition: opacity 0.2s ease; }
.sheet-enter-from, .sheet-leave-to { opacity: 0; }
</style>
