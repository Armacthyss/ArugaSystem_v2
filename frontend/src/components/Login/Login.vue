<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import logoIcon from "@/assets/logo-icon.svg"
import { ref, computed } from "vue"
import { useRouter, useRoute } from "vue-router"
import axios from "axios"

const API_BASE_URL = `${API_ORIGIN}/api/auth`

const router = useRouter()
const route = useRoute()

// Set when the router sent the person here from a page that needs a login
// (e.g. the clinic check-in QR), so we can take them back afterwards.
function redirectTarget() {
  const target = route.query.redirect
  return typeof target === "string" && target.startsWith("/") && !target.startsWith("//") ? target : null
}

const sessionNote = route.query.expired ? "Your session expired. Please sign in again." : ""

// =====================================================
// FORM STATE
// =====================================================

const identifier = ref("")
const password = ref("")
const showPassword = ref(false)
const rememberMe = ref(false)

const errorMessage = ref("")
const isLoading = ref(false)

const canSubmit = computed(() =>
  identifier.value.trim().length > 0 &&
  password.value.length > 0 &&
  !isLoading.value
)


// =====================================================
// ROLE REDIRECTION
// =====================================================

function redirectForRole(role) {
  const target = redirectTarget()
  if (target) {
    router.push(target)   // the router guard still checks the role
    return
  }

  switch (role) {
    case "Parent":
      router.push("/ParentOverview")
      break

    case "Healthcare":
      // Doctors and Nurses share the same Healthcare Worker portal.
      router.push("/healthcare/home")
      break

    case "Staff":
      router.push("/staff/dashboard")
      break

    case "SystemAdmin":
      router.push("/system-admin/home")
      break

    default:
      console.warn("Unknown role, no redirect configured:", role)
      errorMessage.value = "This account has no home page configured."
      break
  }
}

// =====================================================
// LOGIN
// =====================================================

const handleLogin = async () => {
  localStorage.clear()
  errorMessage.value = ""
  isLoading.value = true

  try {
    const response = await axios.post(`${API_BASE_URL}/login`, {
      identifier: identifier.value,
      password: password.value,
    })

    const data = response.data

    if (data.accountType === "Parent") {
      const p = data.user

      // Same account/authToken shape every other role writes, so the
      // router guard's isLoggedIn()/getRole() checks work for Parent
      // routes too.
      localStorage.setItem("authToken", data.token)
      localStorage.setItem("account", JSON.stringify({
        role: "Parent",
        user: {
          ParentID: p.parentID,
          FirstName: p.firstName,
          MiddleName: p.middleName,
          LastName: p.lastName,
          Email: p.email,
          ContactNo: p.contactNo,
          BarangayNo: p.barangayNo,
          Address: p.address,
          MustChangePassword: data.mustChangePassword,
        },
      }))

      // Legacy key some Parent components may still read directly.
      // The backend response field is `user`, not `parent` — this was
      // previously storing the literal string "undefined".
      localStorage.setItem("parentUser", JSON.stringify(p))

      if (data.mustChangePassword) router.push("/ChangePassword")
      else redirectForRole("Parent")
      return
    }

    if (data.accountType === "Personnel") {
      const u = data.user
      // Position is the real job title (Doctor / Nurse / Staff /
      // Administrator); UserType is a legacy column, used only as a fallback.
      const userType = u.position || u.userType

      // The backend already computes the coarse role (Staff vs Healthcare)
      // from the user's Position — trust that instead of re-deriving it
      // here, so the two can never disagree.
      const role = data.role

      localStorage.setItem("authToken", data.token)
      localStorage.setItem("account", JSON.stringify({
        role,
        user: {
          UserID: u.userID,
          FirstName: u.firstName,
          LastName: u.lastName,
          UserType: userType,
          PRCNo: u.prcNo,
          MustChangePassword: data.mustChangePassword,
        },
      }))

      if (data.mustChangePassword) {
        router.push("/ChangePassword")
        return
      }

      redirectForRole(role, userType)
      return
    }

    if (data.accountType === "SystemAdmin") {
      localStorage.setItem("authToken", data.token)
      localStorage.setItem("account", JSON.stringify({
        role: "SystemAdmin",
        user: {
          UserID: data.referenceID,
          FirstName: data.user.username,
          LastName: "",
          UserType: "SystemAdmin",
          MustChangePassword: data.mustChangePassword,
        },
      }))

      if (data.mustChangePassword) {
        router.push("/ChangePassword")
        return
      }

      redirectForRole("SystemAdmin", "SystemAdmin")
      return
    }

    // Unrecognized accountType — surface this instead of silently
    // sitting on the login page with no feedback.
    console.error("Login succeeded but accountType was not handled:", data.accountType)
    errorMessage.value = "This account type isn't supported yet. Please contact an administrator."
  } catch (error) {
    errorMessage.value = error?.response?.data?.message || "Invalid email/username or password."
    console.error("Login Error:", error)
  } finally {
    isLoading.value = false
  }
}

const currentYear = new Date().getFullYear()
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
        <div class="flex items-center gap-3">
          <img :src="logoIcon" alt="Leveriza Health Center" class="h-11 w-11" />
          <div>
            <h1 class="text-3xl font-black tracking-wide">ARUGA</h1>
            <p class="text-sm text-white/70 mt-1">Pediatric Health Record System</p>
          </div>
        </div>

        <div class="max-w-xl">
          <h2 class="text-5xl font-black leading-tight mb-6">Pediatric Immunization Tracker</h2>
          <p class="text-lg text-white/90 leading-relaxed">
            A centralized system for managing pediatric health records,
            vaccinations, appointments, and clinic operations.
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
      <form class="w-full max-w-105 bg-white rounded-3xl shadow-xl p-10" @submit.prevent="handleLogin">
        <div class="mb-8">
          <h2 class="text-3xl font-black text-[#2d3a26]">Welcome to Aruga</h2>
          <p class="text-gray-500 mt-2">Sign in to continue to your account.</p>
        </div>

        <!-- USERNAME / EMAIL -->
        <div class="mb-5">
          <label for="identifier" class="block mb-2 text-sm font-semibold text-gray-700">
            Email or Username
          </label>
          <input
            id="identifier"
            v-model="identifier"
            type="text"
            autocomplete="username"
            autocapitalize="none"
            placeholder="Enter your email or username"
            class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 focus:outline-none focus:border-[#546b41]"
          />
        </div>

        <!-- PASSWORD -->
        <div class="mb-3">
          <label for="password" class="block mb-2 text-sm font-semibold text-gray-700">
            Password
          </label>
          <div class="relative">
            <input
              id="password"
              v-model="password"
              :type="showPassword ? 'text' : 'password'"
              autocomplete="current-password"
              placeholder="Enter your password"
              class="w-full border-2 border-[#dcccac] rounded-xl px-4 py-3 pr-12 focus:outline-none focus:border-[#546b41]"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-gray-400 hover:text-[#546b41]"
              :aria-label="showPassword ? 'Hide password' : 'Show password'"
              @click="showPassword = !showPassword"
            >
              {{ showPassword ? "🙈" : "👁️" }}
            </button>
          </div>
        </div>

        <!-- REMEMBER ME / FORGOT PASSWORD -->
        <div class="flex items-center justify-between text-sm mb-6">
          <label class="flex items-center gap-2 text-gray-600 cursor-pointer select-none">
            <input
              v-model="rememberMe"
              type="checkbox"
              class="rounded border-[#dcccac] text-[#546b41] focus:ring-[#546b41]"
            />
            Remember me
          </label>

          <button
            type="button"
            class="text-[#546b41] hover:underline"
            @click="router.push('/forgot-password')"
          >
            Forgot password?
          </button>
        </div>

        <div v-if="sessionNote && !errorMessage" class="mb-4 rounded-xl bg-amber-50 border border-amber-200 px-4 py-3">
          <p class="text-amber-700 text-sm">{{ sessionNote }}</p>
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
          <span v-if="!isLoading">Sign In</span>
          <span v-else>Signing in…</span>
        </button>

        <p class="text-center text-xs text-gray-400 mt-8 leading-relaxed">
          © {{ currentYear }} Aruga · Leveriza Health Center<br />
          All rights reserved.
        </p>
      </form>
    </div>
  </div>
</template>