<!--
  CaptchaBox.vue — the picture CAPTCHA on Sign In and Forgot Password
  (asked for by the technical adviser: Aruga is a public government site, so
  bots must not be able to keep guessing passwords or requesting codes).

  The picture comes from GET /api/auth/captcha (Services/Captcha.cs). Each
  picture works once, so the page calls refresh() after every attempt.
  v-model is what the person typed; the page reads `captchaId` through a ref.
-->
<template>
  <div v-if="enabled" class="mb-5">
    <div class="mb-2 flex items-center justify-between gap-2">
      <label for="captcha" class="min-w-0 text-sm font-semibold text-gray-700">Type the characters below</label>
      <button
        type="button"
        class="inline-flex shrink-0 items-center gap-1 whitespace-nowrap text-xs font-semibold text-[#546b41] hover:underline"
        title="Show a new picture"
        @click="refresh"
      >
        <RefreshCw class="h-3.5 w-3.5" :class="{ 'animate-spin': loading }" /> New picture
      </button>
    </div>
    <!-- The picture (320 x 72, see-through) sits on this soft box -->
    <div class="mb-2 flex h-[72px] w-full select-none items-center justify-center overflow-hidden rounded-xl border-2 border-[#dcccac] bg-gradient-to-br from-[#fbfaf6] to-[#eef4ea]">
      <img v-if="image" :src="image" alt="CAPTCHA picture: type the characters shown" class="h-full w-full object-contain" draggable="false" />
      <span v-else class="text-xs text-gray-400">{{ error || 'Loading…' }}</span>
    </div>
    <input
      id="captcha"
      :value="modelValue"
      @input="$emit('update:modelValue', $event.target.value.toUpperCase())"
      type="text"
      maxlength="8"
      autocomplete="off"
      autocapitalize="characters"
      spellcheck="false"
      placeholder="Characters in the picture"
      class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 tracking-[0.3em] uppercase placeholder:tracking-normal placeholder:normal-case focus:outline-none focus:border-[#546b41]"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import axios from 'axios'
import { API_ORIGIN } from '@/utils/apiBase'
import { RefreshCw } from 'lucide-vue-next'

defineProps({ modelValue: { type: String, default: '' } })
const emit = defineEmits(['update:modelValue'])

const enabled = ref(true)
const captchaId = ref('')
const image = ref('')
const error = ref('')
const loading = ref(false)

async function refresh() {
  error.value = ''
  loading.value = true
  emit('update:modelValue', '')
  try {
    const res = await axios.get(`${API_ORIGIN}/api/auth/captcha`)
    enabled.value = res.data.enabled !== false
    captchaId.value = res.data.captchaId || ''
    image.value = res.data.image || ''
  } catch {
    image.value = ''
    error.value = 'Could not load the picture. Tap "New picture" to try again.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

defineExpose({ captchaId, enabled, refresh })
</script>
