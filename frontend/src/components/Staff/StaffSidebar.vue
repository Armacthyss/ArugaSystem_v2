<!--
  StaffSidebar.vue
  =================
  Single source of truth for the staff-side left nav. Every Staff*.vue page
  should import and render this instead of hand-rolling its own <aside>.
  That's the fix for the "sidebar changes every time I navigate" issue —
  before this, each page kept its own copy of navItems (different order,
  different items, some pages missing entirely), so switching pages made
  the whole sidebar visibly jump around.

  To add/remove/reorder a nav item in the future, edit NAV_ITEMS below —
  it will update on every page at once.
-->
<template>
  <!-- Phones / small tablets: slide-in menu opened from the top bar's ☰ -->
  <div v-if="mobileNavOpen" class="fixed inset-0 z-40 bg-stone-900/40 md:hidden" @click="mobileNavOpen = false"></div>
  <aside
    class="flex-col shrink-0 border-r border-stone-200 bg-white transition-all duration-200 h-screen top-0 md:flex md:sticky"
    :class="[collapsed ? 'md:w-19' : 'md:w-66', mobileNavOpen ? 'flex fixed left-0 z-50 w-66 shadow-xl' : 'hidden']"
  >
    <div class="flex items-center gap-3 px-5 py-5 border-b border-stone-200">
      <div class="flex h-9 w-9 items-center justify-center rounded-xl overflow-hidden shrink-0">
        <img :src="logoIcon" alt="Leveriza Health Center" class="w-full h-full object-cover" />
      </div>
      <div v-if="!collapsed" class="leading-tight">
        <p class="font-semibold text-[15px]">Aruga Pediatric System</p>
        <p class="text-[11px] text-stone-500">Staff / Nurse Portal</p>
      </div>
    </div>

    <nav class="flex-1 px-3 py-4 space-y-1 overflow-y-auto">
      <button
        v-for="item in navItems"
        :key="item.label"
        @click="goTo(item)"
        :disabled="item.disabled"
        :title="item.disabled ? 'Coming soon' : item.label"
        class="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-[13.5px] font-medium transition-colors"
        :class="[
          isActive(item) ? 'bg-emerald-50 text-emerald-800' : 'text-stone-500 hover:bg-stone-50',
          item.disabled ? 'opacity-40 cursor-not-allowed hover:bg-transparent' : ''
        ]"
      >
        <component :is="item.icon" :size="18" :stroke-width="2" />
        <span v-if="!collapsed">{{ item.label }}</span>
      </button>
    </nav>

    <div class="border-t border-stone-200 px-3 py-4">
      <div class="flex items-center gap-3 rounded-xl px-2 py-2">
        <div class="flex h-9 w-9 items-center justify-center rounded-full text-white text-xs font-semibold shrink-0 bg-sky-700">
          {{ staffInitials }}
        </div>
        <div v-if="!collapsed" class="leading-tight">
          <p class="text-[13px] font-semibold">{{ staffName }}</p>
          <p class="text-[11px] text-stone-500">{{ staffRole }}</p>
        </div>
      </div>
      <button @click="handleLogout" class="mt-2 flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-[13px] font-medium text-rose-700 hover:bg-rose-50">
        <LogOut :size="17" />
        <span v-if="!collapsed">Log out</span>
      </button>
    </div>

    <button
      @click="collapsed = !collapsed"
      class="mx-auto mb-3 hidden md:flex h-7 w-7 items-center justify-center rounded-full border border-stone-200 text-stone-500"
    >
      <component :is="collapsed ? ChevronRight : ChevronLeft" :size="14" />
    </button>
  </aside>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { useRouter, useRoute } from "vue-router";
import { mobileNavOpen } from "@/utils/mobileNav";
import { getAccount, logout } from "@/utils/auth";
import { userLevel } from "@/utils/format";
import logoIcon from "@/assets/logo-icon.svg";
import {
  Home, Syringe, Package, CalendarDays, FileCheck2,
  ClipboardList, BarChart2, Contact, LogOut, ChevronLeft, ChevronRight, QrCode,
} from "lucide-vue-next";

const router = useRouter();
const route = useRoute();

const collapsed = ref(false);
// The phone menu closes once a page is chosen
watch(() => route.fullPath, () => { mobileNavOpen.value = false });

// Canonical nav list — the ONE place item labels, order, icons, and routes
// are defined. "Notifications" and "Settings" were intentionally dropped:
// both are already reachable from the bell/gear icons in the top bar, so
// having them again in the sidebar (as dead, disabled entries) was just
// clutter. "Audit Log" was dropped too — that's a System Admin capability,
// not something Staff accounts should see at all (belongs in the Admin
// portal's own nav, if/when that exists separately).
// The Dashboard holds the Vaccination Room card (Call Next / Start
// Vaccinating); vaccination visit pages count as part of it.
const NAV_ITEMS = [
  { icon: Home,          label: "Dashboard",       path: "/staff/dashboard", also: "/staff/vaccination/" },
  { icon: Contact,       label: "Patient Records", path: "/staff/patient-records" },
  { icon: FileCheck2,    label: "My Vaccination Records", path: "/staff/vaccination-records" },
  { icon: Syringe,       label: "Vaccine Schedule",path: "/staff/vaccine-schedule" },
  { icon: Package,       label: "Inventory",       path: "/staff/vaccine-inventory" },
  { icon: CalendarDays,  label: "Calendar",        path: "/staff/calendar" },
  { icon: ClipboardList, label: "Queue Management",path: "/staff/queue-management" },
  { icon: QrCode,        label: "Check-in QR",     path: "/staff/checkin-qr" },
  { icon: BarChart2,     label: "Clinic Reports",  path: "/staff/reports" },
];

const navItems = NAV_ITEMS;

const isActive = (item) =>
  !!item.path && (route.path === item.path || (!!item.also && route.path.startsWith(item.also)));

const goTo = (item) => {
  if (item.disabled || !item.path) return;
  if (route.path === item.path) return;
  router.push(item.path);
};

// Same account-shape guessing as the old StaffDashboard.vue — kept here so
// every page shows the same name/role/initials instead of re-guessing it
// per file.
const account = getAccount() || {};
const user = account.user || {};
const staffName = computed(() =>
  user.name || user.Name || user.fullName || user.FullName ||
  [user.firstName || user.FirstName, user.lastName || user.LastName].filter(Boolean).join(" ") ||
  user.username || user.Username || "Staff"
);
const staffRole = computed(() => userLevel(user.UserType || user.position || account.role || "Staff"));
const staffInitials = computed(() =>
  staffName.value
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map(w => w[0]?.toUpperCase())
    .join("") || "S"
);

const handleLogout = () => {
  logout();
  router.push("/");
};
</script>