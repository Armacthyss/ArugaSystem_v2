<!--
  CaptchaBox.vue — "I'm not a robot" on Sign In and Forgot Password
  (asked for by the technical adviser: Aruga is a public government site, so
  bots must not be able to keep guessing passwords or requesting codes).

  The person ticks "I'm not a robot", then types the characters of a picture.
  The tick alone proves nothing (a bot can tick a box too); the picture is
  what the backend checks (GET /api/auth/captcha, Services/Captcha.cs).
  Each picture works once, so the page calls refresh() after every attempt.
  v-model is what the person typed; the page reads `captchaId` and `checked`
  through a ref.
-->
<template>
  <div v-if="enabled" class="mb-5">
    <div class="rounded-xl border-2 bg-[#fbfaf6] transition-colors" :class="checked ? 'border-[#546b41]/40' : 'border-[#dcccac]'">
      <!-- I'm not a robot -->
      <div class="flex items-center gap-3 px-4 py-3">
        <button
          type="button"
          role="checkbox"
          :aria-checked="checked"
          aria-label="I'm not a robot"
          :disabled="checked || checking"
          class="flex h-7 w-7 shrink-0 items-center justify-center rounded-md border-2 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-[#546b41]"
          :class="checked ? 'border-[#546b41] bg-[#546b41]' : 'border-[#b9a98a] bg-white hover:border-[#546b41]'"
          @click="tick"
        >
          <span v-if="checking" class="h-4 w-4 animate-spin rounded-full border-2 border-[#546b41] border-t-transparent"></span>
          <Check v-else-if="checked" class="h-5 w-5 text-white" :stroke-width="3" />
        </button>
        <span class="flex-1 select-none text-sm font-medium text-gray-700 cursor-pointer" @click="tick">I'm not a robot</span>
        <div class="flex shrink-0 flex-col items-center text-[#546b41]">
          <ShieldCheck class="h-6 w-6" />
          <span class="text-[9px] font-semibold uppercase tracking-wide text-gray-400">Aruga</span>
        </div>
      </div>

      <!-- After the tick: the picture to type -->
      <Transition name="captcha-open">
        <div v-if="checked" class="border-t border-[#dcccac]/70 px-4 pb-4 pt-3">
          <div class="mb-2 flex items-center justify-between gap-2">
            <label for="captcha" class="min-w-0 text-sm font-semibold text-gray-700">Type what you see</label>
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
          <div class="mb-2 flex h-[72px] w-full select-none items-center justify-center overflow-hidden rounded-xl border-2 border-[#dcccac] bg-gradient-to-br from-white to-[#eef4ea]">
            <img v-if="image" :src="image" alt="CAPTCHA picture: type the characters shown" class="h-full w-full object-contain" draggable="false" />
            <span v-else class="text-xs text-gray-400">{{ error || 'Loading…' }}</span>
          </div>
          <input
            id="captcha"
            ref="answerInput"
            :value="modelValue"
            @input="$emit('update:modelValue', $event.target.value.toUpperCase())"
            type="text"
            maxlength="8"
            autocomplete="off"
            autocapitalize="characters"
            spellcheck="false"
            placeholder="Characters in the picture"
            class="w-full border-2 border-[#dcccac] bg-white rounded-xl px-4 py-3 tracking-[0.3em] uppercase placeholder:tracking-normal placeholder:normal-case focus:outline-none focus:border-[#546b41]"
          />
        </div>
      </Transition>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from 'vue'
import axios from 'axios'
import { API_ORIGIN } from '@/utils/apiBase'
import { Check, RefreshCw, ShieldCheck } from 'lucide-vue-next'

defineProps({ modelValue: { type: String, default: '' } })
const emit = defineEmits(['update:modelValue'])

const enabled = ref(true)
const checked = ref(false)
const checking = ref(false)
const captchaId = ref('')
const image = ref('')
const error = ref('')
const loading = ref(false)
const answerInput = ref(null)
let loadedAt = 0

async function refresh() {
  error.value = ''
  loading.value = true
  emit('update:modelValue', '')
  try {
    const res = await axios.get(`${API_ORIGIN}/api/auth/captcha`)
    enabled.value = res.data.enabled !== false
    captchaId.value = res.data.captchaId || ''
    image.value = res.data.image || ''
    loadedAt = Date.now()
  } catch {
    image.value = ''
    error.value = 'Could not load the picture. Tap "New picture" to try again.'
  } finally {
    loading.value = false
  }
}

// Tick: a short "checking" moment, then the picture opens below. A picture
// loaded more than 4 minutes ago is replaced (each one lasts 5 minutes).
async function tick() {
  if (checked.value || checking.value) return
  checking.value = true
  const fresh = Date.now() - loadedAt < 4 * 60 * 1000 && image.value
  await Promise.all([fresh ? null : refresh(), new Promise(r => setTimeout(r, 600))])
  checking.value = false
  checked.value = true
  await nextTick()
  answerInput.value?.focus()
}

// Loaded early so the picture is ready when the box is ticked
onMounted(refresh)

defineExpose({ captchaId, enabled, checked, refresh })
</script>

<style scoped>
.captcha-open-enter-active { transition: opacity .25s ease, transform .25s ease; }
.captcha-open-enter-from { opacity: 0; transform: translateY(-6px); }
</style>
