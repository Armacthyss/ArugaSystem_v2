<template>
  <aside class="col-span-12 lg:col-span-3 space-y-4">
    <div class="flex items-center justify-between px-2">
      <h3 class="text-[10px] font-semibold text-emerald-600 uppercase tracking-wide">Family Profiles</h3>
      <span class="text-[9px] font-black text-white bg-emerald-600 px-2 py-0.5 rounded-full">{{ children.length }}</span>
    </div>
    <div class="space-y-2 max-h-[calc(100vh-220px)] overflow-y-auto pr-1">
      <button v-for="child in children" :key="child.childID"
        @click="handleSelect(child)"
        :class="selectedChild?.childID === child.childID ? 'border-emerald-600 bg-white ring-4 ring-emerald-600/5' : 'border-transparent bg-white/50 hover:bg-white'"
        class="w-full p-4 rounded-xl border-2 transition-all text-left flex items-center gap-3">
        <span class="text-xl shrink-0">{{ child.sex === 'Female' ? '👧' : '👶' }}</span>
        <div class="min-w-0">
          <p class="text-xs font-bold text-slate-800 leading-tight truncate">{{ child.firstName }} {{ child.lastName }}</p>
          <!-- Leveriza knows families by Family No. and Barangay, not a system ID -->
          <p v-if="child.familyNo || child.barangay" class="text-[9px] text-slate-400 font-medium mt-0.5">
            <span v-if="child.familyNo">Family No. {{ child.familyNo }}</span><span v-if="child.familyNo && child.barangay"> · </span><span v-if="child.barangay">Brgy. {{ child.barangay }}</span>
          </p>
        </div>
      </button>
    </div>
  </aside>
</template>

<script setup>
// children and selectedChild are fetched/owned by whichever page renders
// this component (see HARD RULE 3) and passed down as props. This
// component only renders the list and emits the selection up — the page
// is responsible for persisting the choice to localStorage and re-fetching
// that child's records.
defineProps({
  children: { type: Array, default: () => [] },
  selectedChild: { type: Object, default: null },
})

const emit = defineEmits(['select-child'])

function handleSelect(child) {
  emit('select-child', child)
}
</script>