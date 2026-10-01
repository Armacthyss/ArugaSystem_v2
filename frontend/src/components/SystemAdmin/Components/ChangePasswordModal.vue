<script setup>
// "Change my password" window for the Admin / Doctor and Super Admin
// header. Stays on the current page (the full /ChangePassword page is only
// for a first sign-in or a reset). The backend checks the current password
// and the same rules again: POST /api/auth/change-password.
import { ref, computed } from 'vue'
import axios from 'axios'
import { KeyRound, Eye, EyeOff, X, Check, Circle } from 'lucide-vue-next'
import { API_BASE } from '@/utils/format'

const emit = defineEmits(['close'])

const form = ref({ current: '', newPw: '', confirm: '' })
const show = ref({ current: false, newPw: false, confirm: false })
const error = ref('')
const success = ref(false)
const saving = ref(false)

const requirements = computed(() => {
  const pw = form.value.newPw
  return [
    { label: 'At least 8 characters', met: pw.length >= 8 },
    { label: 'One uppercase letter',  met: /[A-Z]/.test(pw) },
    { label: 'One lowercase letter',  met: /[a-z]/.test(pw) },
    { label: 'One number',            met: /[0-9]/.test(pw) },
    { label: 'One special character', met: /[^A-Za-z0-9]/.test(pw) },
  ]
})
const meetsRules = computed(() => requirements.value.every(r => r.met))
const mismatch = computed(() => !!form.value.confirm && form.value.newPw !== form.value.confirm)
const canSubmit = computed(() =>
  !!form.value.current && meetsRules.value && form.value.newPw === form.value.confirm && !saving.value
)

const FIELDS = [
  { key: 'current', label: 'Current password',     placeholder: 'Enter your current password', autocomplete: 'current-password' },
  { key: 'newPw',   label: 'New password',         placeholder: 'Create a strong password',    autocomplete: 'new-password' },
  { key: 'confirm', label: 'Confirm new password', placeholder: 'Re-enter the new password',   autocomplete: 'new-password' },
]

async function submit() {
  error.value = ''
  if (!form.value.current) { error.value = 'Please enter your current password.'; return }
  if (!meetsRules.value) { error.value = 'The new password does not meet all the requirements.'; return }
  if (form.value.newPw !== form.value.confirm) { error.value = 'The new passwords do not match.'; return }
  if (form.value.newPw === form.value.current) { error.value = 'The new password must be different from the current one.'; return }

  saving.value = true
  try {
    await axios.post(`${API_BASE}/auth/change-password`, {
      currentPassword: form.value.current,
      newPassword: form.value.newPw,
    })
    success.value = true
    form.value = { current: '', newPw: '', confirm: '' }
    setTimeout(() => emit('close'), 1800)
  } catch (err) {
    error.value = err.response?.data?.message || 'Could not change the password. Please try again.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Teleport to="body">
    <div class="fixed inset-0 z-100 flex items-center justify-center p-4 bg-black/40" @click.self="emit('close')">
      <form
        class="w-full max-w-md bg-white rounded-xl shadow-2xl overflow-hidden"
        role="dialog"
        aria-modal="true"
        aria-labelledby="change-pw-title"
        @submit.prevent="submit"
      >
        <div class="flex items-center gap-3 px-6 py-4 border-b border-slate-200">
          <div class="w-9 h-9 rounded-lg bg-emerald-50 text-emerald-700 flex items-center justify-center shrink-0">
            <KeyRound class="w-5 h-5" :stroke-width="1.8" />
          </div>
          <div class="flex-1 min-w-0">
            <h2 id="change-pw-title" class="text-base font-semibold text-slate-900">Change my password</h2>
            <p class="text-xs text-slate-500">You'll use the new password the next time you sign in.</p>
          </div>
          <button type="button" @click="emit('close')" class="w-8 h-8 rounded-lg flex items-center justify-center text-slate-400 hover:bg-slate-50 hover:text-slate-600" aria-label="Close">
            <X class="w-4 h-4" />
          </button>
        </div>

        <div v-if="success" class="px-6 py-10 text-center">
          <div class="w-12 h-12 mx-auto rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center mb-3">
            <Check class="w-6 h-6" />
          </div>
          <p class="text-sm font-semibold text-slate-900">Password changed</p>
          <p class="text-xs text-slate-500 mt-1">Your new password is now active.</p>
        </div>

        <div v-else class="px-6 py-5 space-y-4">
          <div v-for="f in FIELDS" :key="f.key">
            <label :for="`pw-${f.key}`" class="block text-xs font-medium text-slate-600 mb-1.5">{{ f.label }}</label>
            <div class="relative">
              <input
                :id="`pw-${f.key}`"
                v-model="form[f.key]"
                :type="show[f.key] ? 'text' : 'password'"
                :placeholder="f.placeholder"
                :autocomplete="f.autocomplete"
                class="w-full pl-3 pr-10 py-2.5 text-sm border rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent"
                :class="f.key === 'confirm' && mismatch ? 'border-rose-300' : 'border-slate-200'"
              />
              <button
                type="button"
                @click="show[f.key] = !show[f.key]"
                class="absolute inset-y-0 right-0 w-10 flex items-center justify-center text-slate-400 hover:text-slate-600"
                :aria-label="show[f.key] ? 'Hide password' : 'Show password'"
              >
                <component :is="show[f.key] ? EyeOff : Eye" class="w-4 h-4" />
              </button>
            </div>

            <ul v-if="f.key === 'newPw'" class="mt-2 grid grid-cols-2 gap-x-3 gap-y-1">
              <li
                v-for="r in requirements"
                :key="r.label"
                class="flex items-center gap-1.5 text-[11px]"
                :class="r.met ? 'text-emerald-600' : 'text-slate-400'"
              >
                <component :is="r.met ? Check : Circle" class="w-3 h-3 shrink-0" />
                {{ r.label }}
              </li>
            </ul>
            <p v-if="f.key === 'confirm' && mismatch" class="mt-1.5 text-[11px] text-rose-600">The passwords do not match.</p>
          </div>

          <p v-if="error" class="text-xs text-rose-600 bg-rose-50 border border-rose-100 rounded-lg px-3 py-2">{{ error }}</p>
        </div>

        <div v-if="!success" class="flex justify-end gap-2 px-6 py-4 border-t border-slate-200 bg-slate-50">
          <button type="button" @click="emit('close')" class="px-4 py-2 text-sm font-medium text-slate-600 rounded-lg hover:bg-slate-100">Cancel</button>
          <button
            type="submit"
            :disabled="!canSubmit"
            class="px-4 py-2 text-sm font-medium text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {{ saving ? 'Saving…' : 'Update password' }}
          </button>
        </div>
      </form>
    </div>
  </Teleport>
</template>
