<!--
  StockAdjustModal.vue — "Adjust Stock" for one batch (Oct 2026), on the
  Admin / Doctor and Staff / Nurse Inventory pages. For doses that can't be
  given (broken vial, spoiled, expired and thrown away, lost) or a recount
  that found a different number. POST /api/VaccineInventory/{id}/adjust;
  each adjustment is kept with who, when and why.

    <StockAdjustModal v-if="adjusting" :batch="adjusting" :vaccine-name="..." @close="adjusting = null" @saved="reload" />
-->
<template>
  <Teleport to="body">
    <div class="fixed inset-0 z-[70] flex items-center justify-center bg-black/40 p-4" @click.self="$emit('close')"
      role="dialog" aria-modal="true" aria-labelledby="adjust-title">
      <div class="w-full max-w-md rounded-2xl bg-white shadow-xl overflow-hidden">
        <div class="flex items-start justify-between gap-3 px-5 py-4 border-b border-slate-100">
          <div>
            <p id="adjust-title" class="text-[16px] font-bold text-slate-900">Adjust stock</p>
            <p class="text-[12.5px] text-slate-500 mt-0.5">{{ vaccineName }} · Lot {{ batch.lotNumber }} · <span class="font-semibold text-slate-700">{{ batch.currentQuantity }} doses on file</span></p>
          </div>
          <button @click="$emit('close')" class="p-1.5 rounded-lg hover:bg-slate-100 text-slate-500" aria-label="Close"><X class="w-4 h-4" /></button>
        </div>

        <div class="px-5 py-4 space-y-4">
          <!-- What happened -->
          <div class="space-y-2">
            <p class="text-[13px] font-semibold text-slate-700">What happened?</p>
            <label class="flex items-start gap-3 rounded-xl border px-3.5 py-3 cursor-pointer" :class="mode === 'remove' ? 'border-emerald-600 bg-emerald-50/50' : 'border-slate-200 hover:bg-slate-50'">
              <input v-model="mode" type="radio" value="remove" class="mt-0.5 text-emerald-600 focus:ring-emerald-500" />
              <span>
                <span class="block text-[13.5px] font-medium text-slate-800">Some doses can't be used</span>
                <span class="block text-[12px] text-slate-500">Broken vial, spoiled, expired and thrown away, or lost</span>
              </span>
            </label>
            <label class="flex items-start gap-3 rounded-xl border px-3.5 py-3 cursor-pointer" :class="mode === 'count' ? 'border-emerald-600 bg-emerald-50/50' : 'border-slate-200 hover:bg-slate-50'">
              <input v-model="mode" type="radio" value="count" class="mt-0.5 text-emerald-600 focus:ring-emerald-500" />
              <span>
                <span class="block text-[13.5px] font-medium text-slate-800">Recount</span>
                <span class="block text-[12px] text-slate-500">The number on the shelf is different from the one on file</span>
              </span>
            </label>
          </div>

          <template v-if="mode === 'remove'">
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label for="adj-qty" class="block text-[12.5px] font-medium text-slate-600 mb-1">How many doses?</label>
                <input id="adj-qty" v-model.number="quantity" v-digits type="number" min="1" :max="batch.currentQuantity"
                  class="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-[14px] focus:outline-none focus:ring-2 focus:ring-emerald-500" />
              </div>
              <div>
                <label for="adj-reason" class="block text-[12.5px] font-medium text-slate-600 mb-1">Why?</label>
                <select id="adj-reason" v-model="reason" class="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-[14px] focus:outline-none focus:ring-2 focus:ring-emerald-500">
                  <option value="" disabled>Choose…</option>
                  <option v-for="r in REASONS" :key="r" :value="r">{{ r }}</option>
                </select>
              </div>
            </div>
          </template>
          <div v-else>
            <label for="adj-count" class="block text-[12.5px] font-medium text-slate-600 mb-1">How many doses are really on the shelf?</label>
            <input id="adj-count" v-model.number="quantity" v-digits type="number" min="0" :max="batch.initialQuantity"
              class="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-[14px] focus:outline-none focus:ring-2 focus:ring-emerald-500" />
          </div>

          <div>
            <label for="adj-note" class="block text-[12.5px] font-medium text-slate-600 mb-1">
              Note <span class="font-normal text-slate-400">{{ mode === 'remove' && reason === 'Other' ? '(required)' : '(optional)' }}</span>
            </label>
            <input id="adj-note" v-model="note" type="text" maxlength="255" :placeholder="mode === 'remove' ? 'e.g. Vial dropped during the morning session' : 'e.g. Wednesday stock check'"
              class="w-full rounded-xl border border-slate-200 px-3 py-2.5 text-[14px] focus:outline-none focus:ring-2 focus:ring-emerald-500" />
          </div>

          <!-- What will change -->
          <div v-if="after !== null" class="flex items-center justify-between rounded-xl bg-slate-50 px-3.5 py-2.5 text-[13px]">
            <span class="text-slate-500">Doses on file</span>
            <span class="font-semibold text-slate-800">{{ batch.currentQuantity }} <ArrowRight class="inline w-3.5 h-3.5 mx-1 text-slate-400" /> <span :class="after < batch.currentQuantity ? 'text-rose-700' : 'text-emerald-700'">{{ after }}</span></span>
          </div>

          <p v-if="error" class="rounded-xl bg-rose-50 px-3 py-2.5 text-[13px] text-rose-700" role="alert">{{ error }}</p>
          <p class="text-[11.5px] text-slate-400">Saved with your name and the time. To fix a mistake, make another adjustment.</p>
        </div>

        <div class="flex justify-end gap-2 px-5 py-4 border-t border-slate-100 bg-slate-50">
          <button @click="$emit('close')" class="rounded-xl px-4 py-2.5 text-[13px] font-medium text-slate-600 hover:bg-slate-100">Cancel</button>
          <button @click="save" :disabled="!canSave || saving"
            class="rounded-xl bg-emerald-700 px-5 py-2.5 text-[13px] font-semibold text-white hover:bg-emerald-800 disabled:opacity-50 disabled:cursor-not-allowed">
            {{ saving ? 'Saving…' : 'Save adjustment' }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import axios from 'axios'
import { X, ArrowRight } from 'lucide-vue-next'
import { API_BASE } from '@/utils/format'

const props = defineProps({
  batch: { type: Object, required: true },   // { inventoryID, lotNumber, currentQuantity, initialQuantity }
  vaccineName: { type: String, default: '' },
})
const emit = defineEmits(['close', 'saved'])

// Same list as the API (VaccineInventoryController.RemoveReasons)
const REASONS = ['Broken vial', 'Spoiled (cold chain)', 'Expired, thrown away', 'Lost', 'Other']

const mode = ref('remove')
const quantity = ref(null)
const reason = ref('')
const note = ref('')
const error = ref('')
const saving = ref(false)

watch(mode, () => { quantity.value = null; error.value = '' })

const after = computed(() => {
  const q = Number(quantity.value)
  if (quantity.value === null || quantity.value === '' || !Number.isFinite(q)) return null
  return mode.value === 'remove' ? props.batch.currentQuantity - q : q
})

const canSave = computed(() => {
  const q = Number(quantity.value)
  if (quantity.value === null || quantity.value === '' || !Number.isInteger(q)) return false
  if (mode.value === 'remove') {
    return q >= 1 && q <= props.batch.currentQuantity && !!reason.value && (reason.value !== 'Other' || !!note.value.trim())
  }
  return q >= 0 && q <= props.batch.initialQuantity && q !== props.batch.currentQuantity
})

async function save() {
  error.value = ''
  saving.value = true
  try {
    const res = await axios.post(`${API_BASE}/VaccineInventory/${props.batch.inventoryID}/adjust`, {
      mode: mode.value,
      quantity: Number(quantity.value),
      reason: mode.value === 'remove' ? reason.value : 'Recount',
      note: note.value.trim() || null,
    })
    emit('saved', res.data)
    emit('close')
  } catch (e) {
    error.value = e.response?.data?.message || 'Could not save the adjustment. Please try again.'
  } finally {
    saving.value = false
  }
}
</script>
