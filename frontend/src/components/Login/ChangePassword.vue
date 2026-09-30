<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, onMounted } from "vue"
import { useRouter } from "vue-router"
import axios from "axios"

const API_BASE_URL = `${API_ORIGIN}/api/auth`

const router = useRouter()

// =====================================================
// SESSION / ACCOUNT
// =====================================================

const account = ref(null)

onMounted(() => {
  const token = localStorage.getItem("authToken")

  if (!token) {
    router.push("/")
    return
  }

  const rawAccount = localStorage.getItem("account")

  if (rawAccount) {
    try {
      account.value = JSON.parse(rawAccount)
    } catch (error) {
      console.error("Invalid account data in storage:", error)
    }
  }
})


// =====================================================
// FORM STATE
// =====================================================

const currentPassword = ref("")
const newPassword = ref("")
const confirmPassword = ref("")

const showCurrent = ref(false)
const showNew = ref(false)
const showConfirm = ref(false)

const errorMessage = ref("")
const isLoading = ref(false)


// =====================================================
// PASSWORD REQUIREMENTS
// =====================================================

const requirements = computed(() => [
  { label: "At least 8 characters", met: newPassword.value.length >= 8 },
  { label: "One uppercase letter", met: /[A-Z]/.test(newPassword.value) },
  { label: "One lowercase letter", met: /[a-z]/.test(newPassword.value) },
  { label: "One number", met: /[0-9]/.test(newPassword.value) },
  { label: "One special character", met: /[^A-Za-z0-9]/.test(newPassword.value) }
])

const allRequirementsMet = computed(() =>
  requirements.value.every(req => req.met)
)

const passwordsMatch = computed(() =>
  newPassword.value.length > 0 &&
  newPassword.value === confirmPassword.value
)

const isDifferentFromCurrent = computed(() =>
  currentPassword.value.length > 0 &&
  newPassword.value.length > 0 &&
  newPassword.value !== currentPassword.value
)

const canSubmit = computed(() =>
  currentPassword.value.length > 0 &&
  allRequirementsMet.value &&
  passwordsMatch.value &&
  isDifferentFromCurrent.value &&
  !isLoading.value
)


// =====================================================
// ROLE REDIRECTION
// (kept in sync with Login.vue / router/index.js)
// =====================================================

function redirectForRole(role) {
  switch (role) {
    case "Parent":
      router.push("/ParentOverview")
      break

    // Staff / Nurse
    case "Staff":
    case "Nurse":
    case "Admission":
      router.push("/staff/dashboard")
      break

    // Admin / Doctor
    case "SystemAdmin":
    case "Doctor":
    case "Administrator":
      router.push("/system-admin/home")
      break

    case "SuperAdmin":
      router.push("/super-admin/home")
      break

    default:
      console.warn("Unknown role, no redirect configured:", role)
      router.push("/")
      break
  }
}


// =====================================================
// CHANGE PASSWORD
// =====================================================

async function handleChangePassword() {
  errorMessage.value = ""

  if (!allRequirementsMet.value) {
    errorMessage.value = "Please meet all password requirements before continuing."
    return
  }

  if (!passwordsMatch.value) {
    errorMessage.value = "New password and confirmation do not match."
    return
  }

  if (!isDifferentFromCurrent.value) {
    errorMessage.value = "Your new password must be different from your current password."
    return
  }

  const token = localStorage.getItem("authToken")

  if (!token) {
    errorMessage.value = "Your session has expired. Please log in again."
    router.push("/")
    return
  }

  isLoading.value = true

  try {
    // Body intentionally contains ONLY currentPassword and newPassword.
    // The backend identifies the account from the JWT (AccountID claim).
    const response = await axios.post(
      `${API_BASE_URL}/change-password`,
      {
        currentPassword: currentPassword.value,
        newPassword: newPassword.value
      },
      {
        headers: {
          Authorization: `Bearer ${token}`
        }
      }
    )

    const updatedAccount = {
      ...account.value,
      mustChangePassword: false
    }

    localStorage.setItem("account", JSON.stringify(updatedAccount))
    account.value = updatedAccount

    // The session's portal role (Parent / Healthcare / Staff / SystemAdmin)
    // is what the router guards on, so prefer it over the backend's
    // job-title style role.
    redirectForRole(updatedAccount.role ?? response.data?.role)

  } catch (error) {
    console.error("Change password error:", error)

    if (error.response?.status === 401) {
      localStorage.removeItem("authToken")
      localStorage.removeItem("account")

      errorMessage.value = "Your session has expired. Please log in again."
      router.push("/")
    } else if (error.response) {
      // e.g. 400 "Current password is incorrect." (checked before
      // error.request: a server reply also has error.request set)
      errorMessage.value =
        error.response.data?.message ||
        "Unable to change your password. Please check your current password and try again."
    } else if (error.request) {
      errorMessage.value = "Can't reach the server. Please check your connection and try again."
    } else {
      errorMessage.value = "Unable to change your password. Please try again."
    }
  } finally {
    isLoading.value = false
  }
}


// =====================================================
// LOGOUT
// =====================================================

function handleLogout() {
  localStorage.removeItem("authToken")
  localStorage.removeItem("account")
  router.push("/")
}
</script>

<template>
  <div class="min-h-screen flex">
    <!-- LEFT SIDE -->
    <div
      class="hidden lg:flex lg:w-1/2 relative bg-cover bg-center"
      style="background-image: url('https://images.unsplash.com/photo-1584515933487-779824d29309');"
    >
      <div class="absolute inset-0 bg-gradient-to-b from-green-900/80 via-green-800/75 to-green-700/85"></div>

      <div class="relative z-10 h-full w-full flex flex-col justify-between p-12 text-white">
        <div>
          <h1 class="text-3xl font-black tracking-wide">ARUGA</h1>
          <p class="text-sm text-white/70 mt-1">Pediatric Health Record System</p>
        </div>

        <div class="max-w-xl">
          <h2 class="text-5xl font-black leading-tight mb-6">One Last Step</h2>
          <p class="text-lg text-white/90 leading-relaxed">
            Your account was created with a temporary password. Set a new
            one to keep your pediatric health records secure.
          </p>
        </div>

        <div class="flex gap-6 text-sm text-white/70">
          <span>● Secure Access</span>
          <span>● Role-Based Access</span>
        </div>
      </div>
    </div>

    <!-- RIGHT SIDE -->
    <div class="w-full lg:w-1/2 bg-[#fff8ec] flex items-center justify-center px-6 py-10">
      <form class="w-full max-w-[420px] bg-white rounded-3xl shadow-xl p-10" @submit.prevent="handleChangePassword">
        <div class="mb-8">
          <h2 class="text-3xl font-black text-[#2d3a26]">Change Your Password</h2>
          <p class="text-gray-500 mt-2">
            For your security, you need to set a new password before continuing<span v-if="account"> {{ account.firstName || account.username || " to the dashboard" }}</span>.
          </p>
        </div>

        <!-- CURRENT / TEMPORARY PASSWORD -->
        <div class="mb-5">
          <label for="currentPassword" class="block mb-2 text-sm font-semibold text-gray-700">
            Current Password
          </label>
          <div class="relative">
            <input
              id="currentPassword"
              v-model="currentPassword"
              :type="showCurrent ? 'text' : 'password'"
              autocomplete="current-password"
              placeholder="Enter your current or temporary password"
              class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 pr-12 focus:outline-none focus:border-[#546b41]"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-gray-400 hover:text-[#546b41]"
              :aria-label="showCurrent ? 'Hide password' : 'Show password'"
              @click="showCurrent = !showCurrent"
            >
              {{ showCurrent ? "🙈" : "👁️" }}
            </button>
          </div>
        </div>

        <!-- NEW PASSWORD -->
        <div class="mb-5">
          <label for="newPassword" class="block mb-2 text-sm font-semibold text-gray-700">
            New Password
          </label>
          <div class="relative">
            <input
              id="newPassword"
              v-model="newPassword"
              :type="showNew ? 'text' : 'password'"
              autocomplete="new-password"
              placeholder="Create a new password"
              class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 pr-12 focus:outline-none focus:border-[#546b41]"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-gray-400 hover:text-[#546b41]"
              :aria-label="showNew ? 'Hide password' : 'Show password'"
              @click="showNew = !showNew"
            >
              {{ showNew ? "🙈" : "👁️" }}
            </button>
          </div>

          <!-- Live requirements checklist -->
          <ul class="mt-3 grid grid-cols-1 gap-1">
            <li
              v-for="req in requirements"
              :key="req.label"
              class="flex items-center gap-2 text-xs"
              :class="req.met ? 'text-[#546b41]' : 'text-gray-400'"
            >
              <span
                class="w-4 h-4 rounded-full flex items-center justify-center text-[10px] shrink-0"
                :class="req.met ? 'bg-[#546b41] text-white' : 'bg-gray-200 text-gray-400'"
              >
                {{ req.met ? "✓" : "" }}
              </span>
              {{ req.label }}
            </li>
          </ul>
        </div>

        <!-- CONFIRM NEW PASSWORD -->
        <div class="mb-5">
          <label for="confirmPassword" class="block mb-2 text-sm font-semibold text-gray-700">
            Confirm New Password
          </label>
          <div class="relative">
            <input
              id="confirmPassword"
              v-model="confirmPassword"
              :type="showConfirm ? 'text' : 'password'"
              autocomplete="new-password"
              placeholder="Re-enter your new password"
              class="w-full border-2 rounded-xl px-4 py-3 pr-12 focus:outline-none"
              :class="
                confirmPassword.length === 0
                  ? 'border-[#dcccac] focus:border-[#546b41]'
                  : passwordsMatch
                  ? 'border-emerald-400 focus:border-emerald-500'
                  : 'border-red-300 focus:border-red-400'
              "
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-gray-400 hover:text-[#546b41]"
              :aria-label="showConfirm ? 'Hide password' : 'Show password'"
              @click="showConfirm = !showConfirm"
            >
              {{ showConfirm ? "🙈" : "👁️" }}
            </button>
          </div>
          <p v-if="confirmPassword.length > 0 && !passwordsMatch" class="mt-1.5 text-xs text-red-500">
            Passwords do not match.
          </p>
        </div>

        <!-- ERROR -->
        <div v-if="errorMessage" class="mb-4 rounded-xl bg-red-50 border border-red-200 px-4 py-3">
          <p class="text-red-600 text-sm">{{ errorMessage }}</p>
        </div>

        <!-- SUBMIT -->
        <button
          type="submit"
          :disabled="!canSubmit"
          class="w-full bg-[#546b41] hover:bg-[#455936] disabled:bg-gray-300 text-white py-3 rounded-xl font-bold transition-all"
        >
          <span v-if="!isLoading">Change Password &amp; Continue</span>
          <span v-else>Updating…</span>
        </button>

        <!-- LOGOUT ESCAPE HATCH -->
        <p class="text-center text-sm mt-6">
          <button type="button" class="text-gray-400 hover:text-[#546b41] hover:underline" @click="handleLogout">
            Not you? Log out
          </button>
        </p>

        <p class="text-center text-xs text-gray-400 mt-6 leading-relaxed">
          You won't be able to access your account until your password is changed.
        </p>
      </form>
    </div>
  </div>
</template>