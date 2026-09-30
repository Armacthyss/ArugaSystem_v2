<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import logoIcon from '@/assets/logo-icon.svg'
import { ref, computed, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import axios from 'axios'
import CaptchaBox from '@/components/Shared/CaptchaBox.vue'

const router = useRouter()

// Picture CAPTCHA before a code is sent (see CaptchaBox.vue)
const captcha = ref(null)
const captchaAnswer = ref('')
const API_BASE = `${API_ORIGIN}/api/auth/forgot-password`

// step: 'request' -> 'verify' -> 'reset' -> 'done'
const step = ref('request')
const identifier = ref('')
const deliveryMethod = ref('email') // 'email' | 'sms'
const otp = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const showPassword = ref(false)

const loading = ref(false)
const errorMessage = ref('')
const infoMessage = ref('')

// Same rules the backend enforces (AuthController.GetPasswordComplexityError)
const rules = computed(() => [
  { label: 'At least 8 characters', ok: newPassword.value.length >= 8 },
  { label: 'An uppercase letter', ok: /[A-Z]/.test(newPassword.value) },
  { label: 'A lowercase letter', ok: /[a-z]/.test(newPassword.value) },
  { label: 'A number', ok: /\d/.test(newPassword.value) },
  { label: 'A symbol (e.g. @ # !)', ok: /[^A-Za-z0-9]/.test(newPassword.value) },
])
const passwordOk = computed(() => rules.value.every(r => r.ok))
const passwordsMatch = computed(() => newPassword.value.length > 0 && newPassword.value === confirmPassword.value)

// "Send again" waits a minute, same as the backend's limit
const cooldown = ref(0)
let timer = null
function startCooldown() {
  cooldown.value = 60
  clearInterval(timer)
  timer = setInterval(() => {
    cooldown.value--
    if (cooldown.value <= 0) clearInterval(timer)
  }, 1000)
}
onBeforeUnmount(() => clearInterval(timer))

const requestCode = async () => {
  errorMessage.value = ''
  infoMessage.value = ''
  if (!identifier.value.trim()) {
    errorMessage.value = 'Enter your email or username first.'
    return
  }
  if (captcha.value?.enabled !== false && !captchaAnswer.value.trim()) {
    errorMessage.value = 'Type the characters in the picture first.'
    return
  }
  loading.value = true
  try {
    const res = await axios.post(`${API_BASE}/request`, {
      identifier: identifier.value.trim(),
      deliveryMethod: deliveryMethod.value,
      captchaId: captcha.value?.captchaId,
      captchaAnswer: captchaAnswer.value,
    })
    infoMessage.value = res.data.message
    otp.value = ''
    step.value = 'verify'
    startCooldown()
  } catch (err) {
    errorMessage.value = err?.response?.data?.message || 'Something went wrong. Please try again.'
    captcha.value?.refresh()   // each picture works once
  } finally {
    loading.value = false
  }
}

const verifyCode = async () => {
  errorMessage.value = ''
  if (!/^\d{6}$/.test(otp.value.trim())) {
    errorMessage.value = 'Enter the 6-digit code we sent you.'
    return
  }
  loading.value = true
  try {
    await axios.post(`${API_BASE}/verify`, {
      identifier: identifier.value.trim(),
      otp: otp.value.trim(),
    })
    step.value = 'reset'
  } catch (err) {
    errorMessage.value = err?.response?.data?.message || 'Invalid or expired code.'
  } finally {
    loading.value = false
  }
}

const resetPassword = async () => {
  errorMessage.value = ''
  if (!passwordOk.value) {
    errorMessage.value = 'The new password doesn’t meet all the rules yet.'
    return
  }
  if (!passwordsMatch.value) {
    errorMessage.value = 'Passwords do not match.'
    return
  }
  loading.value = true
  try {
    await axios.post(`${API_BASE}/reset`, {
      identifier: identifier.value.trim(),
      otp: otp.value.trim(),
      newPassword: newPassword.value,
    })
    step.value = 'done'
  } catch (err) {
    errorMessage.value = err?.response?.data?.message || 'Could not reset password. Please try again.'
  } finally {
    loading.value = false
  }
}

const sendAgain = () => {
  if (cooldown.value > 0) return
  step.value = 'request'
  otp.value = ''
  errorMessage.value = ''
  infoMessage.value = ''
}
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-[#fff8ec] px-4 py-10">
    <div class="w-full max-w-[420px] bg-white rounded-3xl shadow-xl p-8 sm:p-10">

      <div class="flex items-center gap-3 mb-6">
        <img :src="logoIcon" alt="Leveriza Health Center" class="h-10 w-10" />
        <div class="leading-tight">
          <p class="font-black text-[#2d3a26] tracking-wide">ARUGA</p>
          <p class="text-xs text-gray-500">Leveriza Health Center</p>
        </div>
      </div>

      <h2 class="text-3xl font-bold text-[#2d3a26] mb-2">Reset Password</h2>
      <p class="text-gray-500 mb-6 text-sm">
        <span v-if="step === 'request'">Enter the email or username you sign in with. We'll send you a 6-digit code.</span>
        <span v-else-if="step === 'verify'">Enter the code we sent you.</span>
        <span v-else-if="step === 'reset'">Choose a new password.</span>
        <span v-else>All set!</span>
      </p>

      <!-- STEP 1: request -->
      <form v-if="step === 'request'" @submit.prevent="requestCode">
        <div class="mb-5">
          <label class="block mb-2 text-sm font-semibold text-gray-700">Email or Username</label>
          <input
            v-model="identifier"
            type="text"
            autocomplete="username"
            placeholder="parent@example.com or your staff username"
            class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 focus:outline-none focus:border-[#546b41]"
          />
        </div>

        <div class="mb-6">
          <label class="block mb-2 text-sm font-semibold text-gray-700">Send the code by</label>
          <div class="grid grid-cols-2 gap-3 text-sm">
            <label
              class="flex items-center gap-2 rounded-xl border-2 px-3 py-2.5 cursor-pointer"
              :class="deliveryMethod === 'email' ? 'border-[#546b41] bg-[#546b41]/5' : 'border-[#dcccac]'"
            >
              <input type="radio" value="email" v-model="deliveryMethod" class="accent-[#546b41]" /> Email
            </label>
            <label
              class="flex items-center gap-2 rounded-xl border-2 px-3 py-2.5 cursor-pointer"
              :class="deliveryMethod === 'sms' ? 'border-[#546b41] bg-[#546b41]/5' : 'border-[#dcccac]'"
            >
              <input type="radio" value="sms" v-model="deliveryMethod" class="accent-[#546b41]" /> SMS (text)
            </label>
          </div>
        </div>

        <CaptchaBox ref="captcha" v-model="captchaAnswer" />

        <button
          type="submit"
          :disabled="loading"
          class="w-full bg-[#546b41] hover:bg-[#455936] text-white py-3 rounded-xl font-bold mb-2 disabled:opacity-60"
        >
          {{ loading ? 'Sending...' : 'Send Code' }}
        </button>
      </form>

      <!-- STEP 2: verify -->
      <form v-else-if="step === 'verify'" @submit.prevent="verifyCode">
        <p v-if="infoMessage" class="text-[#546b41] text-sm mb-4 rounded-xl bg-[#546b41]/5 px-4 py-3">{{ infoMessage }}</p>

        <div class="mb-6">
          <label class="block mb-2 text-sm font-semibold text-gray-700">Verification Code</label>
          <input
            v-model="otp" v-digits
            type="text"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="6"
            placeholder="6-digit code"
            class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 focus:outline-none focus:border-[#546b41] tracking-[0.5em] text-center text-lg font-bold"
          />
        </div>

        <button
          type="submit"
          :disabled="loading"
          class="w-full bg-[#546b41] hover:bg-[#455936] text-white py-3 rounded-xl font-bold mb-3 disabled:opacity-60"
        >
          {{ loading ? 'Verifying...' : 'Verify Code' }}
        </button>

        <button
          type="button"
          @click="sendAgain"
          :disabled="cooldown > 0"
          class="w-full text-sm text-[#546b41] py-2 disabled:text-gray-400"
        >
          {{ cooldown > 0 ? `Didn't get it? You can ask again in ${cooldown}s` : "Didn't get it? Send again" }}
        </button>
      </form>

      <!-- STEP 3: reset -->
      <form v-else-if="step === 'reset'" @submit.prevent="resetPassword">
        <div class="mb-3">
          <label class="block mb-2 text-sm font-semibold text-gray-700">New Password</label>
          <div class="relative">
            <input
              v-model="newPassword"
              :type="showPassword ? 'text' : 'password'"
              autocomplete="new-password"
              placeholder="New password"
              class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 pr-16 focus:outline-none focus:border-[#546b41]"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-xs font-semibold text-gray-400 hover:text-[#546b41]"
              @click="showPassword = !showPassword"
            >
              {{ showPassword ? 'Hide' : 'Show' }}
            </button>
          </div>
        </div>

        <ul class="mb-5 grid grid-cols-1 gap-1 text-xs">
          <li v-for="r in rules" :key="r.label" :class="r.ok ? 'text-[#546b41]' : 'text-gray-400'">
            {{ r.ok ? '✓' : '○' }} {{ r.label }}
          </li>
        </ul>

        <div class="mb-6">
          <label class="block mb-2 text-sm font-semibold text-gray-700">Confirm New Password</label>
          <input
            v-model="confirmPassword"
            :type="showPassword ? 'text' : 'password'"
            autocomplete="new-password"
            placeholder="Type it again"
            class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 focus:outline-none focus:border-[#546b41]"
          />
          <p v-if="confirmPassword && !passwordsMatch" class="text-xs text-red-500 mt-1">Passwords don't match yet.</p>
        </div>

        <button
          type="submit"
          :disabled="loading || !passwordOk || !passwordsMatch"
          class="w-full bg-[#546b41] hover:bg-[#455936] text-white py-3 rounded-xl font-bold mb-2 disabled:opacity-60"
        >
          {{ loading ? 'Saving...' : 'Reset Password' }}
        </button>
      </form>

      <!-- STEP 4: done -->
      <template v-else>
        <p class="text-[#546b41] text-sm mb-6 rounded-xl bg-[#546b41]/5 px-4 py-3">
          Your password has been reset. You can now sign in with your new password.
        </p>
        <button
          @click="router.push('/')"
          class="w-full bg-[#546b41] hover:bg-[#455936] text-white py-3 rounded-xl font-bold mb-4"
        >
          Back to Sign In
        </button>
      </template>

      <p v-if="errorMessage" class="mt-3 rounded-xl bg-red-50 border border-red-200 px-4 py-3 text-red-600 text-sm">{{ errorMessage }}</p>

      <router-link v-if="step !== 'done'" to="/" class="block text-center text-xs text-gray-400 mt-6 hover:text-[#546b41]">
        Cancel and go back to Sign In
      </router-link>
    </div>
  </div>
</template>
