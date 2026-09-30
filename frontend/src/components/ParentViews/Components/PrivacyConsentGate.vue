<!--
  PrivacyConsentGate.vue — asks the parent to accept the Data Privacy Notice
  (RA 10173) before they can use the parent portal. Shown once: the answer
  is saved on the parent's record (Parents.PrivacyConsentAt) through
  POST /api/Parents/{id}/privacy-consent and logged in the Audit Logs.
  A parent who doesn't agree can sign out; the health center keeps their
  paper record and can talk it through with them.
-->
<template>
  <Teleport to="body">
    <div v-if="needed" class="fixed inset-0 z-[400] flex items-center justify-center bg-slate-900/60 p-4" role="dialog" aria-modal="true" aria-labelledby="privacy-title">
      <div class="flex max-h-[92vh] w-full max-w-lg flex-col rounded-2xl bg-white shadow-2xl">
        <div class="border-b border-slate-100 px-6 py-5">
          <p class="text-[10px] font-black uppercase tracking-widest text-emerald-600">Before you continue</p>
          <h2 id="privacy-title" class="mt-1 text-lg font-bold text-slate-800">Data Privacy Notice</h2>
          <p class="text-xs text-slate-500">Data Privacy Act of 2012 (Republic Act No. 10173)</p>
        </div>
        <div class="flex-1 overflow-y-auto px-6 py-5">
          <PrivacyNotice />
        </div>
        <div class="border-t border-slate-100 px-6 py-5 space-y-4">
          <label class="flex items-start gap-3 cursor-pointer">
            <input v-model="agree" type="checkbox" class="mt-0.5 h-5 w-5 shrink-0 rounded border-slate-300 accent-emerald-600" />
            <span class="text-sm text-slate-700">
              I have read the Data Privacy Notice and I agree that my and my child's information may be
              shared with and kept in Aruga for these purposes.
            </span>
          </label>
          <p v-if="error" class="text-xs font-semibold text-red-600">{{ error }}</p>
          <div class="flex flex-col-reverse gap-2 sm:flex-row">
            <button @click="$emit('decline')" class="rounded-xl border border-slate-200 px-4 py-3 text-sm font-semibold text-slate-600 hover:bg-slate-50 sm:flex-1">
              I don't agree — sign out
            </button>
            <button @click="accept" :disabled="!agree || saving" class="rounded-xl bg-emerald-600 px-4 py-3 text-sm font-bold text-white hover:bg-emerald-700 disabled:opacity-40 sm:flex-1">
              {{ saving ? 'Saving…' : 'I Agree' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import api from '../Composables/api'
import PrivacyNotice from '@/components/Shared/PrivacyNotice.vue'

const props = defineProps({ parentId: { type: String, default: null } })
defineEmits(['decline'])

const needed = ref(false)
const agree = ref(false)
const saving = ref(false)
const error = ref('')

function savedParent() {
  try { return JSON.parse(localStorage.getItem('parentUser') || '{}') } catch { return {} }
}

function remember(at) {
  const p = savedParent()
  p.privacyConsentAt = at
  localStorage.setItem('parentUser', JSON.stringify(p))
}

onMounted(async () => {
  if (!props.parentId || savedParent().privacyConsentAt) return
  // Not saved on this phone yet: ask the server (they may have agreed on another device)
  try {
    const res = await api.get(`/Parents/${props.parentId}`)
    if (res.data?.privacyConsentAt) remember(res.data.privacyConsentAt)
    else needed.value = true
  } catch {
    needed.value = true
  }
})

async function accept() {
  saving.value = true
  error.value = ''
  try {
    const res = await api.post(`/Parents/${props.parentId}/privacy-consent`)
    remember(res.data?.privacyConsentAt || new Date().toISOString())
    needed.value = false
  } catch (e) {
    error.value = e.response?.data?.message || 'Could not save your answer. Please try again.'
  } finally {
    saving.value = false
  }
}
</script>
