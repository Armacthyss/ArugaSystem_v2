<script setup>
import { ref, computed, watch, onMounted } from "vue";
import { useRouter } from "vue-router";
import axios from "axios";
import {
  Syringe, ChevronLeft, ChevronRight, Search, ChevronDown,
  Download, Eye, Printer, MoreHorizontal, ListPlus, CalendarDays, CalendarClock,
  CheckCircle2, AlertTriangle, ChevronUp, CheckSquare, Square, RefreshCw,
} from "lucide-vue-next";
import StaffSidebar from "./StaffSidebar.vue";
import StaffTopbar from "./StaffTopbar.vue";
import { API_BASE, ageLabel, ageParts, formatDate, toISODate, downloadCSV } from "@/utils/format";
import { useFloatingMenu } from "@/utils/floatingMenu";
import { askConfirm } from "@/utils/dialog";

/* ---------------------------------------------------------
   Aruga Pediatric System — Vaccine Schedule (Staff / Nurse)
   Built from each child's vaccination timeline
   (GET /api/VaccinationTimeline/schedule). One row per child visit:
   every dose the child is due on the same day is grouped together,
   and all of a child's overdue doses are grouped into one catch-up row.
   Staff can put a due/overdue child straight into today's queue;
   recording the vaccination itself is done by the Doctor/Nurse.

   Simplified Oct 2026 (survey: "bombarded with information"): one list
   with four tabs (Due today / Overdue / Coming up / Done today) instead of
   six cards, a status dropdown, a calendar view (the Calendar page already
   shows the month) and three side boxes that repeated the same numbers.
--------------------------------------------------------- */

const router = useRouter();

const today = new Date();
today.setHours(0, 0, 0, 0);
const todayISO = toISODate(today);

/* ---------------- Data ---------------- */
const schedule = ref([]);
const todaysQueue = ref([]);
const inventory = ref([]);   // stock batches, to compare with what's needed
const loading = ref(true);
const loadError = ref("");
const toast = ref("");

function flash(msg) {
  toast.value = msg;
  setTimeout(() => { toast.value = ""; }, 4000);
}

async function loadData() {
  loading.value = true;
  loadError.value = "";
  try {
    const from = new Date(today); from.setDate(from.getDate() - 45);
    const to = new Date(today); to.setDate(to.getDate() + 90);
    const [s, q, inv] = await Promise.all([
      axios.get(`${API_BASE}/VaccinationTimeline/schedule`, { params: { from: toISODate(from), to: toISODate(to), includeOverdue: true } }),
      axios.get(`${API_BASE}/Queue/today`),
      axios.get(`${API_BASE}/VaccineInventory`).catch(() => ({ data: [] })),
    ]);
    schedule.value = s.data;
    todaysQueue.value = q.data;
    inventory.value = inv.data;
  } catch (e) {
    console.error("VaccineSchedule:", e);
    loadError.value = "Could not load the vaccination schedule. Check that the API is running.";
  } finally {
    loading.value = false;
  }
}

onMounted(loadData);

// childID -> today's queue entry (if the child is already queued)
const queueByChild = computed(() => {
  const map = {};
  for (const q of todaysQueue.value) for (const c of q.children || []) map[c.childID] = q;
  return map;
});

function queueStatusOf(q) {
  const s = (q?.status || "").toLowerCase().replace(/\s+/g, "");
  if (s === "inprogress") return "In Progress";
  if (s === "completed") return "Completed";
  return "Waiting";
}

/* ---------------- Appointments (one row per child visit) ---------------- */
const newGroup = (t, key, date, overdue) => ({
  key, overdue, childID: t.childID, parentID: t.parentID,
  child: t.childName, birthDate: t.birthDate, age: ageLabel(t.birthDate),
  parent: t.parentName || "—", contact: t.parentContact || "—",
  dueISO: date, vaccines: [],
});
const vaccineOf = (t) => ({
  name: t.vaccineName, short: t.abbreviation || t.vaccineName, dose: `Dose ${t.doseNumber}`,
  done: t.status === "Completed",
});

const appointments = computed(() => {
  const groups = new Map();
  for (const t of schedule.value) {
    const date = String(t.scheduledDate).slice(0, 10);
    const overdue = t.displayStatus === "Overdue";
    const key = overdue ? `${t.childID}|overdue` : `${t.childID}|${date}`;
    if (!groups.has(key)) groups.set(key, newGroup(t, key, date, overdue));
    const g = groups.get(key);
    if (date < g.dueISO) g.dueISO = date;
    g.vaccines.push(vaccineOf(t));
  }

  return [...groups.values()].map(g => {
    const q = queueByChild.value[g.childID];
    const allDone = g.vaccines.every(v => v.done);
    let status;
    if (allDone) status = "Completed";
    else if (g.overdue) status = q ? queueStatusOf(q) : "Delayed";
    else if (g.dueISO === todayISO) status = q ? queueStatusOf(q) : "Due Today";
    else status = "Scheduled";
    return {
      ...g,
      dueDate: new Date(g.dueISO + "T00:00:00"),
      due: formatDate(g.dueISO),
      queueNo: q ? `#${q.queueNumber}` : "—",
      inQueue: !!q,
      status,
    };
  }).sort((a, b) => a.dueDate - b.dueDate || a.child.localeCompare(b.child));
});

function progressOf(appt) {
  const done = appt.vaccines.filter((v) => v.done).length;
  return { done, total: appt.vaccines.length };
}

/* ---------------- Done today (one row per child) ---------------- */
// Doses recorded today, grouped by child. (A child's catch-up doses keep
// their original due dates, so grouping by date would split them up.)
const doneToday = computed(() => {
  const groups = new Map();
  for (const t of schedule.value) {
    if (t.status !== "Completed" || String(t.completedDate || "").slice(0, 10) !== todayISO) continue;
    if (!groups.has(t.childID)) groups.set(t.childID, newGroup(t, `${t.childID}|done`, todayISO, false));
    groups.get(t.childID).vaccines.push(vaccineOf(t));
  }
  return [...groups.values()].map(g => {
    const q = queueByChild.value[g.childID];
    return { ...g, dueDate: today, due: "Today", queueNo: q ? `#${q.queueNumber}` : "—", inQueue: !!q, status: "Completed" };
  }).sort((a, b) => a.child.localeCompare(b.child));
});

/* ---------------- Tabs (also the summary numbers) ---------------- */
const in30Days = new Date(today); in30Days.setDate(in30Days.getDate() + 30);
const isDueToday = (a) => a.dueISO === todayISO && !a.overdue;
const isOverdue = (a) => a.overdue && a.status !== "Completed";
const isComingUp = (a) => a.status === "Scheduled" && a.dueDate <= in30Days;

const tab = ref("due"); // "due" | "overdue" | "upcoming" | "done"
const tabs = computed(() => {
  const due = appointments.value.filter(isDueToday);
  const notHere = due.filter(a => a.status === "Due Today").length;
  const doses = doneToday.value.reduce((s, a) => s + a.vaccines.length, 0);
  return [
    { key: "due", label: "Due today", value: due.length, icon: Syringe, tint: "text-emerald-700", tintBg: "bg-emerald-50",
      note: !due.length ? "children" : notHere ? `${notHere} not checked in yet` : "all checked in", empty: "Nobody is due today. Look at Overdue or Coming up." },
    { key: "overdue", label: "Overdue", value: appointments.value.filter(isOverdue).length, icon: AlertTriangle, tint: "text-rose-700", tintBg: "bg-rose-50",
      note: "missed a dose, need catch-up", empty: "No child is overdue." },
    { key: "upcoming", label: "Coming up", value: appointments.value.filter(isComingUp).length, icon: CalendarClock, tint: "text-sky-700", tintBg: "bg-sky-50",
      note: "in the next 30 days", empty: "Nothing is due in the next 30 days." },
    { key: "done", label: "Done today", value: doneToday.value.length, icon: CheckCircle2, tint: "text-emerald-700", tintBg: "bg-emerald-50",
      note: `${doses} dose${doses === 1 ? "" : "s"} given`, empty: "No vaccines recorded yet today." },
  ];
});
const activeTab = computed(() => tabs.value.find(t => t.key === tab.value));

/* Toolbar */
const searchQuery = ref("");
const vaccineFilter = ref("All Vaccines");
const ageGroupFilter = ref("All Ages");

const vaccineOptions = computed(() => ["All Vaccines", ...new Set(schedule.value.map(t => t.vaccineName))]);

const statusStyle = {
  "Due Today": "bg-emerald-50 text-emerald-700",
  Waiting: "bg-amber-50 text-amber-700",
  "In Progress": "bg-sky-50 text-sky-700",
  Completed: "bg-emerald-50 text-emerald-700",
  Delayed: "bg-rose-50 text-rose-700",
  Scheduled: "bg-stone-100 text-stone-600",
};
const statusDot = {
  "Due Today": "bg-emerald-500",
  Waiting: "bg-amber-500",
  "In Progress": "bg-sky-500",
  Completed: "bg-emerald-600",
  Delayed: "bg-rose-600",
  Scheduled: "bg-stone-400",
};
// Plainer words for the status badge
const statusLabel = {
  "Due Today": "Not checked in",
  Delayed: "Overdue",
};

function ageMonths(birth) {
  return ageParts(birth, today)?.totalMonths ?? 0;
}
function inAgeGroup(appt) {
  const m = ageMonths(appt.birthDate);
  switch (ageGroupFilter.value) {
    case "Birth": return m < 1;
    case "0–6 Months": return m <= 6;
    case "7–12 Months": return m >= 7 && m <= 12;
    case "1–2 Years": return m >= 13 && m <= 24;
    default: return true;
  }
}

const filteredAppointments = computed(() => {
  const rows = tab.value === "done" ? doneToday.value
    : appointments.value.filter({ due: isDueToday, overdue: isOverdue, upcoming: isComingUp }[tab.value]);
  const q = searchQuery.value.trim().toLowerCase();
  return rows.filter((a) => {
    if (q && !a.child.toLowerCase().includes(q) && !a.parent.toLowerCase().includes(q)) return false;
    if (vaccineFilter.value !== "All Vaccines" && !a.vaccines.some((v) => v.name === vaccineFilter.value)) return false;
    return inAgeGroup(a);
  });
});

/* Export Schedule — whatever is currently visible in the list */
function exportScheduleCSV() {
  downloadCSV(`vaccine-schedule-${tab.value}-${todayISO}.csv`, [
    ["Queue No.", "Child Name", "Age", "Parent", "Contact", tab.value === "done" ? "Given" : "Due Date", "Vaccines", "Progress", "Status"],
    ...filteredAppointments.value.map((a) => {
      const { done, total } = progressOf(a);
      return [a.queueNo, a.child, a.age, a.parent, a.contact, a.due,
        a.vaccines.map((v) => `${v.name} (${v.dose})`).join("; "), `${done}/${total}`, statusLabel[a.status] || a.status];
    }),
  ]);
}

/* Expand / collapse rows */
const expandedRows = ref(new Set());
function toggleExpand(key) {
  const next = new Set(expandedRows.value);
  if (next.has(key)) next.delete(key);
  else next.add(key);
  expandedRows.value = next;
}

const {
  openMenuId: actionsMenuOpenFor,
  menuStyle: actionsMenuStyle,
  toggleMenu: toggleActionsMenu,
} = useFloatingMenu(110);

/* ---------------- Actions ---------------- */
const queueingKey = ref("");
const canQueue = (a) => !a.inQueue && a.status !== "Completed" && a.parentID && (a.overdue || a.dueISO === todayISO);

// POST /api/Queue — the whole visit for this child's parent today.
// Parents normally check in themselves with the clinic QR; this is the desk
// fallback for one who is here without a phone, so it asks first: a family
// that isn't here would be called in by Call Next and never come.
async function addToQueue(appt) {
  const ok = await askConfirm({
    title: `Is ${appt.parent} here at the clinic now?`,
    message: `${appt.child} will be added to today's queue. Only check in families who are here. Parents with a phone check in themselves by scanning the clinic QR.`,
    confirmText: "Yes, Check In",
    cancelText: "Not Yet",
  });
  if (!ok) return;
  queueingKey.value = appt.key;
  try {
    const res = await axios.post(`${API_BASE}/Queue`, { parentID: appt.parentID, childIDs: [appt.childID] });
    flash(`${appt.child} added to today's queue as #${res.data.queueNumber}.`);
    await loadData();
  } catch (e) {
    // 409: this child is already in today's queue (e.g. someone else just
    // checked them in); the server's message says under which number.
    flash(e.response?.data?.message || "Could not add to the queue.");
    if (e.response?.status === 409) await loadData();
  } finally {
    queueingKey.value = "";
  }
}

function viewPatient(appt) {
  actionsMenuOpenFor.value = null;
  router.push("/staff/patient-records?tab=children");
}
function printCard(appt) {
  actionsMenuOpenFor.value = null;
  window.open(`/print/vaccination-card/${appt.childID}`, "_blank");
}

/* Doses needed — doses due in the next 30 days (incl. overdue) next to the
   usable stock (active, unexpired batches), so staff can see a shortage
   coming. Folded into one line until opened. */
const distribution = computed(() => {
  const map = {};
  schedule.value
    .filter(t => t.status !== "Completed" && new Date(t.scheduledDate) <= in30Days)
    .forEach(t => {
      const k = t.abbreviation || t.vaccineName;
      map[k] = map[k] || { value: 0, vaccineID: t.vaccineID };
      map[k].value++;
    });
  return Object.entries(map).map(([name, d], i) => {
    const stock = inventory.value
      .filter(b => b.vaccineID === d.vaccineID && b.status && new Date(b.expirationDate) >= today)
      .reduce((sum, b) => sum + b.currentQuantity, 0);
    return { name, value: d.value, stock, short: Math.max(0, d.value - stock), color: stock < d.value ? "bg-rose-500" : i % 2 ? "bg-sky-600" : "bg-emerald-600" };
  });
});
const distMax = computed(() => Math.max(1, ...distribution.value.map((d) => d.value)));
const shortVaccines = computed(() => distribution.value.filter((d) => d.short > 0));
const showDoses = ref(false);

// ── PAGINATION ────────────────────────────────────────────────────
const schedPage     = ref(1)
const schedPageSize = ref(10)
watch([tab, searchQuery, vaccineFilter, ageGroupFilter], () => { schedPage.value = 1 })
const schedTotalPages = computed(() =>
  Math.max(1, Math.ceil(filteredAppointments.value.length / schedPageSize.value))
)
const paginatedAppointments = computed(() => {
  const start = (schedPage.value - 1) * schedPageSize.value
  return filteredAppointments.value.slice(start, start + schedPageSize.value)
})
const schedPaginationPages = computed(() => {
  const total = schedTotalPages.value
  const cur   = schedPage.value
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
  const pages = []
  pages.push(1)
  if (cur > 3) pages.push('...')
  for (let p = Math.max(2, cur - 1); p <= Math.min(total - 1, cur + 1); p++) pages.push(p)
  if (cur < total - 2) pages.push('...')
  pages.push(total)
  return pages
})
</script>

<template>
  <div class="flex min-h-screen w-full bg-stone-50 text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;" @click="actionsMenuOpenFor = null">
    <StaffSidebar />

    <main class="flex-1 min-w-0">
      <StaffTopbar title="Vaccine Schedule" breadcrumb="Aruga / Vaccine Schedule" />

      <div class="px-4 md:px-8 py-6 space-y-5">
        <div v-if="toast" class="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-[13px] text-emerald-800">{{ toast }}</div>
        <div v-if="loadError" class="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-[13px] text-rose-700">{{ loadError }}</div>

        <!-- Tabs: each card is a number AND the button that lists those children -->
        <div class="grid grid-cols-2 xl:grid-cols-4 gap-4" role="tablist" aria-label="Which children to list">
          <button
            v-for="t in tabs" :key="t.key"
            role="tab" :aria-selected="tab === t.key"
            @click="tab = t.key"
            class="rounded-2xl border bg-white p-4 text-left shadow-sm transition-colors"
            :class="tab === t.key ? 'border-emerald-600 ring-2 ring-emerald-600/15' : 'border-stone-200 hover:border-stone-300 hover:bg-stone-50/60'"
          >
            <div class="flex items-center justify-between gap-2">
              <p class="text-[13px] font-semibold" :class="tab === t.key ? 'text-emerald-800' : 'text-stone-600'">{{ t.label }}</p>
              <div class="flex h-8 w-8 items-center justify-center rounded-lg shrink-0" :class="t.tintBg">
                <component :is="t.icon" :size="16" :class="t.tint" />
              </div>
            </div>
            <p class="text-[26px] font-bold leading-tight mt-1">{{ loading ? '…' : t.value }}</p>
            <p class="text-[11.5px] text-stone-500 truncate">{{ t.note }}</p>
          </button>
        </div>

        <!-- The list -->
        <div class="rounded-2xl border border-stone-200 bg-white shadow-sm overflow-hidden">
          <div class="flex flex-wrap items-center gap-3 px-5 py-4 border-b border-stone-200">
            <div class="mr-auto min-w-0">
              <p class="text-[14px] font-semibold">{{ activeTab.label }}</p>
              <p class="text-[12px] text-stone-500">
                {{ filteredAppointments.length }} {{ filteredAppointments.length === 1 ? 'child' : 'children' }}
                · <RouterLink to="/staff/calendar" class="inline-flex items-center gap-1 font-medium text-emerald-700 hover:underline">
                    <CalendarDays :size="12" /> See the whole month on the Calendar
                  </RouterLink>
              </p>
            </div>

            <div class="flex items-center gap-2 w-full sm:w-64 rounded-xl border border-stone-200 px-3 py-2">
              <Search :size="15" class="text-stone-400 shrink-0" />
              <input v-model="searchQuery" type="text" placeholder="Search child or parent…" class="flex-1 min-w-0 text-[13px] outline-none placeholder:text-stone-400" />
            </div>
            <div class="relative">
              <select v-model="vaccineFilter" aria-label="Vaccine" class="appearance-none rounded-xl border border-stone-200 pl-3 pr-8 py-2 text-[13px] text-stone-600 outline-none cursor-pointer max-w-[200px]">
                <option v-for="v in vaccineOptions" :key="v">{{ v }}</option>
              </select>
              <ChevronDown :size="13" class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-stone-400" />
            </div>
            <div class="relative">
              <select v-model="ageGroupFilter" aria-label="Age" class="appearance-none rounded-xl border border-stone-200 pl-3 pr-8 py-2 text-[13px] text-stone-600 outline-none cursor-pointer">
                <option>All Ages</option>
                <option>Birth</option>
                <option>0–6 Months</option>
                <option>7–12 Months</option>
                <option>1–2 Years</option>
              </select>
              <ChevronDown :size="13" class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-stone-400" />
            </div>
            <button @click="loadData" class="flex items-center rounded-xl p-2.5 border border-stone-200 hover:bg-stone-50" title="Refresh" aria-label="Refresh">
              <RefreshCw :size="15" class="text-stone-500" :class="loading ? 'animate-spin' : ''" />
            </button>
            <button @click="exportScheduleCSV" class="flex items-center gap-2 rounded-xl px-3.5 py-2 text-[13px] font-medium border border-stone-200 hover:bg-stone-50" title="Download this list as a spreadsheet (CSV)">
              <Download :size="15" class="text-stone-500" /> Export
            </button>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full min-w-[760px] text-[13px] table-fixed">
              <colgroup>
                <col class="w-10" /><col class="w-[26%]" /><col /><col class="w-[120px]" /><col class="w-[150px]" /><col class="w-[190px]" />
              </colgroup>
              <thead>
                <tr class="bg-stone-50">
                  <th v-for="(h, i) in ['', 'Child', 'Vaccines', tab === 'done' ? 'Given' : 'Due', 'Status', '']" :key="i"
                    class="text-left font-semibold px-4 py-3 whitespace-nowrap text-[11px] uppercase tracking-wide text-stone-500"
                    :class="i === 5 ? 'sticky right-0 z-10 bg-stone-50 shadow-[-8px_0_8px_-8px_rgba(0,0,0,0.15)]' : ''">{{ h }}</th>
                </tr>
              </thead>
              <tbody>
                <template v-for="appt in paginatedAppointments" :key="appt.key">
                  <tr class="border-t border-stone-200 cursor-pointer hover:bg-stone-50/60" @click="toggleExpand(appt.key)"
                    :title="expandedRows.has(appt.key) ? 'Hide the vaccines' : 'Show every vaccine for this visit'">
                    <td class="px-3 py-3 text-center">
                      <ChevronUp v-if="expandedRows.has(appt.key)" :size="15" class="text-stone-400 mx-auto" />
                      <ChevronDown v-else :size="15" class="text-stone-400 mx-auto" />
                    </td>
                    <td class="px-4 py-3">
                      <p class="font-medium truncate">{{ appt.child }}</p>
                      <p class="text-[11.5px] text-stone-500 truncate">{{ appt.age }} · {{ appt.parent }}</p>
                    </td>
                    <td class="px-4 py-3">
                      <div class="flex flex-wrap items-center gap-1.5">
                        <span v-for="(v, i) in appt.vaccines.slice(0, 4)" :key="i" class="inline-flex rounded-full bg-sky-50 text-sky-700 text-[11px] font-medium px-2 py-0.5 whitespace-nowrap">{{ v.short }}</span>
                        <span v-if="appt.vaccines.length > 4" class="inline-flex rounded-full bg-stone-100 text-stone-600 text-[11px] font-medium px-2 py-0.5 whitespace-nowrap">+{{ appt.vaccines.length - 4 }} more</span>
                      </div>
                    </td>
                    <td class="px-4 py-3 whitespace-nowrap" :class="appt.overdue && appt.status !== 'Completed' ? 'text-rose-700 font-medium' : 'text-stone-600'">
                      {{ appt.due }}
                    </td>
                    <td class="px-4 py-3 whitespace-nowrap">
                      <span class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium" :class="statusStyle[appt.status]">
                        <span class="h-1.5 w-1.5 rounded-full shrink-0" :class="statusDot[appt.status]" />
                        {{ statusLabel[appt.status] || appt.status }}
                      </span>
                    </td>
                    <td class="sticky right-0 bg-white px-4 py-3 whitespace-nowrap shadow-[-8px_0_8px_-8px_rgba(0,0,0,0.15)]"
                      :class="actionsMenuOpenFor === appt.key ? 'z-30' : 'z-10'" @click.stop>
                      <div class="flex items-center gap-1 relative">
                        <button v-if="canQueue(appt)" @click="addToQueue(appt)" :disabled="queueingKey === appt.key"
                          class="flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-[12px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 disabled:opacity-50"
                          title="The parent is here but can't check in on their phone: check them in for them">
                          <ListPlus :size="14" /> Check In at Desk
                        </button>
                        <span v-else-if="appt.inQueue" class="text-[11.5px] text-stone-500 px-2">In queue {{ appt.queueNo }}</span>
                        <button @click="toggleActionsMenu(appt.key, $event)" class="p-1.5 rounded-lg hover:bg-stone-100 ml-auto" title="More actions">
                          <MoreHorizontal :size="15" class="text-stone-500" />
                        </button>
                        <Teleport to="body">
                        <div v-if="actionsMenuOpenFor === appt.key" @click.stop :style="actionsMenuStyle" class="fixed z-50 w-52 rounded-xl border border-stone-200 bg-white shadow-lg py-1.5">
                          <button @click="viewPatient(appt)" class="flex w-full items-center gap-2.5 px-3.5 py-2 text-[12.5px] text-stone-700 hover:bg-stone-50">
                            <Eye :size="15" class="text-stone-500" /> Open Patient Records
                          </button>
                          <button @click="printCard(appt)" class="flex w-full items-center gap-2.5 px-3.5 py-2 text-[12.5px] text-stone-700 hover:bg-stone-50">
                            <Printer :size="15" class="text-stone-500" /> Print Vaccination Card
                          </button>
                        </div>
                        </Teleport>
                      </div>
                    </td>
                  </tr>

                  <tr v-if="expandedRows.has(appt.key)" class="border-t border-stone-100 bg-stone-50/60">
                    <td colspan="6" class="px-6 py-4">
                      <div class="flex flex-wrap items-center justify-between gap-2 mb-2">
                        <p class="text-[12.5px] font-semibold text-stone-700">
                          {{ tab === 'done' ? 'Given today' : appt.overdue ? 'Missed doses (catch-up)' : 'Vaccines for this visit' }} · Parent contact: {{ appt.contact }}
                        </p>
                        <span v-if="tab !== 'done'" class="text-[12px] font-semibold text-emerald-700">{{ progressOf(appt).done }} of {{ progressOf(appt).total }} given</span>
                      </div>
                      <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
                        <div v-for="(v, i) in appt.vaccines" :key="i" class="flex items-center gap-2.5 rounded-lg bg-white border border-stone-200 px-3 py-2">
                          <CheckSquare v-if="v.done" :size="16" class="text-emerald-700 shrink-0" />
                          <Square v-else :size="16" class="text-stone-400 shrink-0" />
                          <span class="text-[12.5px]" :class="v.done ? 'text-stone-500' : 'text-stone-700'">{{ v.name }} · {{ v.dose }}</span>
                        </div>
                      </div>
                    </td>
                  </tr>
                </template>
                <tr v-if="!loading && filteredAppointments.length === 0">
                  <td colspan="6" class="px-5 py-12 text-center text-[13px] text-stone-400">
                    {{ searchQuery || vaccineFilter !== 'All Vaccines' || ageGroupFilter !== 'All Ages' ? 'No child matches your search or filters.' : activeTab.empty }}
                  </td>
                </tr>
                <tr v-if="loading">
                  <td colspan="6" class="px-5 py-12 text-center text-[13px] text-stone-400">Loading schedule...</td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- PAGINATION -->
          <div v-if="filteredAppointments.length > schedPageSize" class="flex items-center justify-between px-5 py-3.5 border-t border-stone-200 flex-wrap gap-3">
            <div class="flex items-center gap-3">
              <div class="flex items-center gap-2 text-[12px] text-stone-500">
                <span>Show</span>
                <select v-model="schedPageSize" @change="schedPage = 1" class="border border-stone-200 rounded-lg px-2 py-1 text-[12px] text-stone-600 outline-none bg-white">
                  <option :value="10">10</option><option :value="20">20</option><option :value="50">50</option><option :value="100">100</option>
                </select>
                <span>per page</span>
              </div>
              <p class="text-[12px] text-stone-500">
                {{ Math.min((schedPage - 1) * schedPageSize + 1, filteredAppointments.length) }}–{{ Math.min(schedPage * schedPageSize, filteredAppointments.length) }}
                of {{ filteredAppointments.length }}
              </p>
            </div>
            <div class="flex items-center gap-1">
              <button @click="schedPage--" :disabled="schedPage === 1" class="p-1.5 rounded-lg text-stone-400 hover:text-stone-600 hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed" aria-label="Previous page"><ChevronLeft :size="15" /></button>
              <template v-for="p in schedPaginationPages" :key="p">
                <span v-if="p === '...'" class="px-1 text-[12px] text-stone-400">…</span>
                <button v-else @click="schedPage = p" class="min-w-[28px] h-7 rounded-lg text-[12px] font-medium" :class="p === schedPage ? 'bg-emerald-700 text-white' : 'text-stone-600 hover:bg-stone-100'">{{ p }}</button>
              </template>
              <button @click="schedPage++" :disabled="schedPage >= schedTotalPages" class="p-1.5 rounded-lg text-stone-400 hover:text-stone-600 hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed" aria-label="Next page"><ChevronRight :size="15" /></button>
            </div>
          </div>
        </div>

        <!-- Doses needed: one line until opened -->
        <div class="rounded-2xl border bg-white shadow-sm" :class="shortVaccines.length ? 'border-rose-200' : 'border-stone-200'">
          <button @click="showDoses = !showDoses" :aria-expanded="showDoses" class="flex w-full items-center justify-between gap-3 px-5 py-4 text-left">
            <div class="min-w-0">
              <p class="text-[14px] font-semibold">Doses needed in the next 30 days</p>
              <p class="text-[12px]" :class="shortVaccines.length ? 'text-rose-700 font-medium' : 'text-stone-500'">
                <template v-if="loading">Checking stock…</template>
                <template v-else-if="shortVaccines.length">Not enough stock for {{ shortVaccines.map(d => d.name).join(', ') }}. Tell the Doctor or order more.</template>
                <template v-else-if="distribution.length">Enough stock on hand for every dose due.</template>
                <template v-else>No doses are due in the next 30 days.</template>
              </p>
            </div>
            <span v-if="distribution.length" class="flex items-center gap-1 text-[12px] font-medium text-emerald-700 shrink-0">
              {{ showDoses ? 'Hide' : 'Show per vaccine' }}
              <ChevronDown :size="14" class="transition-transform" :class="showDoses ? 'rotate-180' : ''" />
            </span>
          </button>
          <div v-if="showDoses && distribution.length" class="space-y-3 border-t border-stone-100 px-5 pt-4 pb-5">
            <div v-for="d in distribution" :key="d.name" class="flex items-center gap-3">
              <span class="w-16 text-[12px] text-stone-600 shrink-0">{{ d.name }}</span>
              <div class="flex-1 h-2.5 rounded-full bg-stone-100 overflow-hidden">
                <div class="h-full rounded-full" :class="d.color" :style="{ width: (d.value / distMax) * 100 + '%' }"></div>
              </div>
              <span class="w-16 text-right text-[12px] font-semibold">{{ d.value }} due</span>
              <span class="w-28 text-right text-[11.5px]" :class="d.short ? 'text-rose-600 font-semibold' : 'text-stone-400'">
                {{ d.short ? `short by ${d.short}` : `${d.stock} in stock` }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </main>
  </div>
</template>
