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
    <label for="captcha" class="block mb-2 text-sm font-semibold text-gray-700">Type the characters you see</label>
    <div class="flex items-center gap-2 mb-2">
      <div class="h-16 w-[200px] shrink-0 rounded-xl border-2 border-[#dcccac] overflow-hidden bg-[#f7f3ea] flex items-center justify-center">
        <img v-if="image" :src="image" alt="CAPTCHA picture: type the characters shown" class="h-full w-full" draggable="false" />
        <span v-else class="text-xs text-gray-400">{{ error || 'Loading…' }}</span>
      </div>
      <button
        type="button"
        class="h-11 w-11 shrink-0 rounded-xl border-2 border-[#dcccac] text-lg text-[#546b41] hover:border-[#546b41]"
        title="Show a new picture"
        aria-label="Show a new picture"
        @click="refresh"
      >
        ↻
      </button>
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

defineProps({ modelValue: { type: String, default: '' } })
const emit = defineEmits(['update:modelValue'])

const enabled = ref(true)
const captchaId = ref('')
const image = ref('')
const error = ref('')

async function refresh() {
  image.value = ''
  error.value = ''
  emit('update:modelValue', '')
  try {
    const res = await axios.get(`${API_ORIGIN}/api/auth/captcha`)
    enabled.value = res.data.enabled !== false
    captchaId.value = res.data.captchaId || ''
    image.value = res.data.image || ''
  } catch {
    error.value = 'Could not load the picture. Tap ↻ to try again.'
  }
}

onMounted(refresh)

defineExpose({ captchaId, enabled, refresh })
</script>
