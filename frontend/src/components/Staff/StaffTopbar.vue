<!--
  StaffTopbar.vue
  ================
  Shared top bar for every Staff*.vue page.

  Bell    -> families checked in today who are still waiting to be called in,
             from GET /api/Queue/today (same endpoint as the dashboard's queue
             table). The red dot shows when someone has waited 15+ minutes.
             Self-contained, so it works on every Staff*.vue page.
  Gear    -> routes to /staff/settings (StaffAccountSettings.vue).
  Avatar  -> identity-card dropdown only (name, role, signed-in status). No
             actions in here on purpose: logout already lives in
             StaffSidebar.vue, so a second logout in this dropdown would just
             be a duplicate (per earlier decision).

  Usage:
    <StaffTopbar title="Children Records" breadcrumb="Aruga / Children Record">
      <button @click="openAddModal" class="...">+ Register Child</button>
    </StaffTopbar>

  Anything passed in the default slot renders to the left of the bell/gear/
  avatar cluster, so each page can still have its own action button(s).
-->
<template>
  <header class="relative flex items-center justify-between gap-3 px-4 py-4 md:px-8 md:py-5 border-b border-stone-200 bg-white">
    <div class="flex items-center gap-3 min-w-0">
      <!-- Phones / small tablets: opens the sidebar menu -->
      <button @click="mobileNavOpen = true" class="md:hidden flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-stone-600 hover:bg-stone-100" aria-label="Open menu">
        <Menu :size="20" />
      </button>
      <div class="min-w-0">
        <h1 class="text-[18px] md:text-[22px] font-bold truncate">{{ title }}</h1>
        <p v-if="breadcrumb" class="text-[12.5px] mt-0.5 text-stone-500 truncate">{{ breadcrumb }}</p>
      </div>
    </div>
    <div class="flex items-center gap-2 md:gap-3 shrink-0">
      <slot />

      <!-- Bell: stock alerts + families still waiting to be called in -->
      <div class="relative">
        <button
          @click.stop="toggle('bell')"
          class="topbar-trigger relative flex h-9 w-9 items-center justify-center rounded-full bg-stone-50 hover:bg-stone-100"
          aria-label="Alerts and waiting families"
        >
          <Bell :size="17" class="text-stone-500" />
          <span v-if="longWaits > 0 || unreadAlerts > 0" class="absolute top-1.5 right-2 h-1.5 w-1.5 rounded-full bg-rose-600" />
        </button>

        <div
          v-if="open === 'bell'"
          class="topbar-panel absolute right-0 top-11 z-50 w-80 rounded-xl border border-stone-200 bg-white shadow-lg overflow-hidden"
        >
          <!-- Stock check / low stock alerts sent to this staff account -->
          <template v-if="alerts.length">
            <div class="flex items-center justify-between px-4 py-3 border-b border-stone-100">
              <p class="text-[13px] font-semibold">Alerts</p>
              <button v-if="unreadAlerts > 0" @click="markAlertsRead" class="text-[11px] font-semibold text-emerald-700 hover:underline">Mark all read</button>
            </div>
            <div class="max-h-60 overflow-y-auto border-b border-stone-100">
              <button
                v-for="a in alerts"
                :key="a.notificationID"
                @click="openAlert"
                class="block w-full px-4 py-3 text-left hover:bg-stone-50 border-b border-stone-50 last:border-0"
                :class="a.isRead ? '' : 'bg-rose-50/40'"
              >
                <p class="text-[12.5px] font-semibold">{{ a.title }}</p>
                <p class="text-[11px] text-stone-500 mt-0.5 whitespace-pre-line">{{ a.message }}</p>
              </button>
            </div>
          </template>

          <div class="flex items-center justify-between px-4 py-3 border-b border-stone-100">
            <p class="text-[13px] font-semibold">Waiting to be Called</p>
            <span class="text-[11px] text-stone-400">{{ waitingQueue.length }} waiting</span>
          </div>
          <div class="max-h-72 overflow-y-auto">
            <button
              v-for="q in waitingQueue"
              :key="q.queueID"
              @click="goToLate(q)"
              class="flex w-full items-start gap-3 px-4 py-3 text-left hover:bg-stone-50 border-b border-stone-50 last:border-0"
            >
              <span class="mt-1 h-1.5 w-1.5 rounded-full shrink-0" :class="q.minutes >= 15 ? 'bg-rose-600' : 'bg-amber-400'" />
              <div class="min-w-0">
                <p class="text-[12.5px] font-medium truncate">{{ q.child }}</p>
                <p class="text-[11px] text-stone-500 truncate">
                  {{ q.no }} · waiting {{ q.minutes }} min<span v-if="q.minutes >= 15" class="text-rose-600 font-semibold"> · long wait</span>
                </p>
              </div>
            </button>
            <p v-if="!loadingLate && waitingQueue.length === 0" class="px-4 py-6 text-center text-[12px] text-stone-400">
              Nobody is waiting right now.
            </p>
            <p v-if="loadingLate && waitingQueue.length === 0" class="px-4 py-6 text-center text-[12px] text-stone-400">
              Loading…
            </p>
          </div>
        </div>
      </div>

      <!-- Gear: account settings -->
      <button
        @click="router.push('/staff/settings')"
        class="flex h-9 w-9 items-center justify-center rounded-full bg-stone-50 hover:bg-stone-100"
        title="Account Settings"
      >
        <Settings :size="17" class="text-stone-500" />
      </button>

      <!-- Avatar: identity card, view-only -->
      <div class="relative">
        <button
          @click.stop="toggle('avatar')"
          class="topbar-trigger flex h-9 w-9 items-center justify-center rounded-full text-white text-xs font-semibold bg-emerald-700 hover:bg-emerald-800"
        >
          {{ staffInitials }}
        </button>

        <div
          v-if="open === 'avatar'"
          class="topbar-panel absolute right-0 top-11 z-50 w-64 rounded-xl border border-stone-200 bg-white shadow-lg p-4"
        >
          <div class="flex items-center gap-3">
            <div class="flex h-11 w-11 items-center justify-center rounded-full text-white text-sm font-semibold bg-emerald-700 shrink-0">
              {{ staffInitials }}
            </div>
            <div class="min-w-0">
              <p class="text-[13.5px] font-semibold truncate">{{ staffName }}</p>
              <p class="text-[11.5px] text-stone-500 truncate">{{ staffRole }}</p>
            </div>
          </div>
          <!-- The system keeps no shift schedule, so this only says the
               account is signed in (not a real on-duty status). -->
          <div class="mt-3 pt-3 border-t border-stone-100 flex items-center justify-between">
            <span class="text-[11.5px] text-stone-500">Status</span>
            <span class="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 text-[11px] font-medium text-emerald-700">
              <span class="h-1.5 w-1.5 rounded-full bg-emerald-600" />
              Signed in
            </span>
          </div>
        </div>
      </div>
    </div>
  </header>
</template>

<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, onMounted, onUnmounted } from "vue";
import { useRouter } from "vue-router";
import { Bell, Settings, Menu } from "lucide-vue-next";
import { mobileNavOpen } from "@/utils/mobileNav";
import { getAccount } from "@/utils/auth";
import { withRelationship } from "@/utils/format";

defineProps({
  title: { type: String, required: true },
  breadcrumb: { type: String, default: "" },
});

const router = useRouter();
const API_BASE = `${API_ORIGIN}/api`;

const account = getAccount() || {};
const user = account.user || {};
const staffName = computed(() =>
  user.name || user.Name || user.fullName || user.FullName ||
  [user.firstName || user.FirstName, user.lastName || user.LastName].filter(Boolean).join(" ") ||
  user.username || user.Username || "Staff"
);
const staffRole = computed(() => user.position || user.Position || user.jobTitle || account.role || "Staff");
const staffInitials = computed(() =>
  staffName.value
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map(w => w[0]?.toUpperCase())
    .join("") || "S"
);

// Which popover is open: 'bell' | 'avatar' | null
const open = ref(null);
const toggle = (key) => {
  open.value = open.value === key ? null : key;
};
const closeAll = (event) => {
  if (event.target.closest(".topbar-trigger") || event.target.closest(".topbar-panel")) return;
  open.value = null;
};

// Families checked in today who haven't been called into the room yet.
// Self-fetched from the same Queue/today endpoint the dashboard uses, so
// the bell is accurate on every Staff*.vue page, not just the dashboard.
const waitingQueue = ref([]);
const loadingLate = ref(false);
let lateRefreshInterval = null;

const longWaits = computed(() => waitingQueue.value.filter((q) => q.minutes >= 15).length);

const loadLateQueue = async () => {
  loadingLate.value = true;
  try {
    const response = await fetch(`${API_BASE}/Queue/today`);
    if (!response.ok) throw new Error(`Failed to load queue. Status: ${response.status}`);
    const data = await response.json();
    waitingQueue.value = data
      .filter((q) => (q.status || "Waiting") === "Waiting")
      .map((q) => ({
        queueID: q.queueID,
        no: `Q-${String(q.queueNumber).padStart(3, "0")}`,
        child: q.children?.map((c) => c.name).join(", ") || "—",
        parent: withRelationship(q.requestBy, q.requestByRelationship),
        minutes: q.checkedInAt ? Math.max(0, Math.floor((Date.now() - new Date(q.checkedInAt)) / 60000)) : 0,
      }))
      .sort((a, b) => b.minutes - a.minutes);
  } catch (error) {
    console.error("Waiting queue loading error:", error);
    waitingQueue.value = [];
  } finally {
    loadingLate.value = false;
  }
};

const goToLate = (q) => {
  open.value = null;
  router.push("/staff/dashboard");
};

// Alerts sent to this staff account: the weekly stock check and low-stock
// warnings (GET /api/Notifications/user/{userId}).
const alerts = ref([]);
const unreadAlerts = computed(() => alerts.value.filter((a) => !a.isRead).length);
const staffUserId = user.UserID || user.userId;

const loadAlerts = async () => {
  if (!staffUserId) return;
  try {
    const response = await fetch(`${API_BASE}/Notifications/user/${staffUserId}`);
    if (!response.ok) return;
    alerts.value = (await response.json()).slice(0, 8);
  } catch (error) {
    console.error("Alerts loading error:", error);
  }
};

const markAlertsRead = async () => {
  alerts.value.forEach((a) => { a.isRead = true; });
  try { await fetch(`${API_BASE}/Notifications/mark-all-read/user/${staffUserId}`, { method: "PATCH" }); } catch { /* shown as read anyway */ }
};

const openAlert = () => {
  open.value = null;
  markAlertsRead();
  router.push("/staff/vaccine-inventory");
};

onMounted(() => {
  loadLateQueue();
  loadAlerts();
  document.addEventListener("click", closeAll);
  lateRefreshInterval = setInterval(() => { loadLateQueue(); loadAlerts(); }, 15000);
});

onUnmounted(() => {
  if (lateRefreshInterval) clearInterval(lateRefreshInterval);
  document.removeEventListener("click", closeAll);
});
</script>