<script setup>

import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { mobileNavOpen } from '@/utils/mobileNav'
import logoIcon from '@/assets/logo-icon.svg'

import {
  House,
  Users,
  Baby,
  Syringe,
  Package,
  Bell,
  Clock,
  BarChart3,
  ClipboardList,
  LogOut,
  ChevronLeft,
  Activity,
  UserCog,
} from 'lucide-vue-next'

import { logout, getRole } from '@/utils/auth'

const router = useRouter()
// The phone menu closes once a page is chosen
const route = useRoute()
watch(() => route.fullPath, () => { mobileNavOpen.value = false })

// Admin / Doctor: the clinic. Super Admin (the development team): the
// system only, with the audit logs (technical adviser, Oct 2026).
const ADMIN_ITEMS = [
  { label: 'Dashboard',          icon: House,          to: '/admin/home' },
  { label: 'User Management',    icon: Users,         to: '/admin/user-management' },
  { label: 'Patient Management', icon: Baby,          to: '/admin/patients' },
  { label: 'Vaccine Management', icon: Syringe,       to: '/admin/vaccines' },
  { label: 'Inventory',          icon: Package,       to: '/admin/inventory' },
  { label: 'Notifications',      icon: Bell,          to: '/admin/notifications' },
  { label: 'Operating Hours',    icon: Clock,         to: '/admin/operating-hours' },
  { label: 'Reports',            icon: BarChart3,     to: '/admin/reports' },
]
const SUPER_ADMIN_ITEMS = [
  { label: 'System Status',      icon: Activity,      to: '/super-admin/home' },
  { label: 'Admin Accounts',     icon: UserCog,       to: '/super-admin/accounts' },
  { label: 'Audit Logs',         icon: ClipboardList, to: '/super-admin/audit-logs' },
]

const props = defineProps({
  // Leave empty to show the signed-in level's menu
  navItems: { type: Array, default: null },

  // Optional overrides; by default the signed-in account is shown
  userName: { type: String, default: '' },
  userRole: { type: String, default: '' },
  userInitials: { type: String, default: '' },
})

const account = computed(() => {
  try {
    return JSON.parse(localStorage.getItem('account') || 'null')
  } catch {
    return null
  }
})

const loggedInUser = computed(() => account.value?.user || {})

const isSuperAdmin = getRole() === 'SuperAdmin'
const items = computed(() => props.navItems || (isSuperAdmin ? SUPER_ADMIN_ITEMS : ADMIN_ITEMS))

// Login.vue stores FirstName / LastName (PascalCase); accept both spellings
const firstName = computed(() => loggedInUser.value.FirstName || loggedInUser.value.firstName || '')
const lastName = computed(() => loggedInUser.value.LastName || loggedInUser.value.lastName || '')

const displayName = computed(() => {
  if (props.userName) return props.userName
  const fullName = [firstName.value, lastName.value].filter(Boolean).join(' ')
  return fullName || loggedInUser.value.username || 'Administrator'
})

const displayRole = computed(() => {
  if (props.userRole) return props.userRole
  if (isSuperAdmin) return 'Super Admin'
  const position = loggedInUser.value.UserType || loggedInUser.value.position
  // Admin level: the Doctor (older accounts may be an "Administrator")
  if (account.value?.role === 'SystemAdmin') return position === 'Doctor' ? 'Admin / Doctor' : 'Administrator'
  return position || account.value?.role || 'User'
})

// Same title block as the Staff / Nurse sidebar
const portalLabel = computed(() => isSuperAdmin ? 'Super Admin Portal' : 'Admin / Doctor Portal')

const displayInitials = computed(() => {
  if (props.userInitials) return props.userInitials
  const initials = `${firstName.value[0] || ''}${lastName.value[0] || ''}`.toUpperCase()
  return initials || 'SA'
})

function handleLogout() {
  logout()
  router.push('/')
}

const isCollapsed = ref(false)

const toggleSidebar = () => {
  isCollapsed.value = !isCollapsed.value
}

</script>

<template>
  <!-- Phones / small tablets: slide-in menu opened from the header's ☰ -->
  <div v-if="mobileNavOpen" class="fixed inset-0 z-40 bg-slate-900/40 md:hidden" @click="mobileNavOpen = false"></div>
  <aside
    :class="[isCollapsed ? 'md:w-20' : 'md:w-65', mobileNavOpen ? 'flex fixed left-0 z-50 w-65 shadow-xl' : 'hidden']"
    class="md:flex md:sticky flex-col shrink-0 top-0 h-screen bg-white border-r border-slate-200 transition-all duration-300 ease-in-out"
  >
    <div class="h-17.5 flex items-center gap-3 px-5 border-b border-slate-200 shrink-0">
      <div class="w-9 h-9 rounded-lg overflow-hidden shrink-0">
        <img :src="logoIcon" alt="Leveriza Health Center" class="w-full h-full object-cover" />
      </div>
      <div v-if="!isCollapsed" class="leading-tight whitespace-nowrap overflow-hidden">
        <p class="font-semibold text-[15px] text-slate-900">Aruga Pediatric System</p>
        <p class="text-[11px] text-slate-500">{{ portalLabel }}</p>
      </div>
    </div>

    <nav class="flex-1 overflow-y-auto py-4 px-3 space-y-1">
     <RouterLink
  v-for="item in items"
  :key="item.label"
  :to="item.to"
  class="w-full flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
  :class="[
    $route.path === item.to
      ? 'bg-emerald-50 text-emerald-700'
      : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900'
  ]"
>
       <component
  :is="item.icon"
  class="w-5 h-5 shrink-0"
  :stroke-width="1.8"
/>
        <span v-if="!isCollapsed" class="truncate">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <div class="border-t border-slate-200 p-3 shrink-0 space-y-2">
      <div class="flex items-center gap-3 px-2 py-2">
        <div class="w-9 h-9 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center text-xs font-bold shrink-0">{{ displayInitials }}</div>
        <div v-if="!isCollapsed" class="min-w-0">
          <p class="text-sm font-semibold text-slate-900 truncate">{{ displayName }}</p>
          <p class="text-xs text-slate-500 truncate">{{ displayRole }}</p>
        </div>
      </div>
      <button
        @click="handleLogout"
        class="w-full flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-rose-600 hover:bg-rose-50 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-400"
      >
        <LogOut
  class="w-5 h-5 shrink-0"
  :stroke-width="1.8"
/>
        <span v-if="!isCollapsed">Log out</span>
      </button>
      <button @click="toggleSidebar" class="w-full hidden md:flex items-center justify-center rounded-lg px-3 py-2 text-xs font-medium text-slate-400 hover:bg-slate-50 hover:text-slate-600 transition-colors">
       <ChevronLeft
  :class="isCollapsed ? 'rotate-180' : ''"
  class="w-4 h-4 transition-transform"
/>
      </button>
    </div>
  </aside>
</template>