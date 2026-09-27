<script setup>
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import axios from "axios";
import {
  Users, Syringe, ChevronLeft, ChevronRight, Search, Filter, ChevronDown,
  Download, Eye, Printer, MoreHorizontal, ListPlus,
  List, CalendarDays, X, Clock, CheckCircle2, AlertTriangle, ChevronUp, CheckSquare, Square, RefreshCw,
} from "lucide-vue-next";
import StaffSidebar from "./StaffSidebar.vue";
import StaffTopbar from "./StaffTopbar.vue";
import { API_BASE, ageLabel, formatDate, toISODate, downloadCSV } from "@/utils/format";
import { useFloatingMenu } from "@/utils/floatingMenu";

/* ---------------------------------------------------------
   Aruga Pediatric System — Vaccine Schedule (Admission Staff)
   Built from each child's vaccination timeline
   (GET /api/VaccinationTimeline/schedule). One row per child visit:
   every dose the child is due on the same day is grouped together,
   and all of a child's overdue doses are grouped into one catch-up row.
   Staff can put a due/overdue child straight into today's queue;
   recording the vaccination itself is done by the Doctor/Nurse.
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
const appointments = computed(() => {
  const groups = new Map();
  for (const t of schedule.value) {
    const date = String(t.scheduledDate).slice(0, 10);
    const overdue = t.displayStatus === "Overdue";
    const key = overdue ? `${t.childID}|overdue` : `${t.childID}|${date}`;
    if (!groups.has(key)) {
      groups.set(key, {
        key, overdue, childID: t.childID, parentID: t.parentID,
        id: `PT-${String(t.childID).slice(-8).toUpperCase()}`,
        child: t.childName, birthDate: t.birthDate, age: ageLabel(t.birthDate),
        parent: t.parentName || "—", contact: t.parentContact || "—",
        dueISO: date, vaccines: [],
      });
    }
    const g = groups.get(key);
    if (date < g.dueISO) g.dueISO = date;
    g.vaccines.push({ name: t.vaccineName, short: t.abbreviation || t.vaccineName, dose: `Dose ${t.doseNumber}`, done: t.status === "Completed" });
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

/* ---------------- Summary cards ---------------- */
const summaryCards = computed(() => {
  const inWeek = new Date(today); inWeek.setDate(inWeek.getDate() + 7);
  const tomorrow = new Date(today); tomorrow.setDate(tomorrow.getDate() + 1);
  const dueToday = appointments.value.filter(a => a.dueISO === todayISO && !a.overdue);
  return [
    { label: "Vaccinations Due Today", value: dueToday.reduce((s, a) => s + a.vaccines.length, 0), tint: "text-emerald-700", tintBg: "bg-emerald-50", icon: Syringe },
    { label: "Upcoming This Week", value: appointments.value.filter(a => a.status === "Scheduled" && a.dueDate <= inWeek).length, tint: "text-sky-700", tintBg: "bg-sky-50", icon: CalendarDays },
    { label: "Children Overdue", value: appointments.value.filter(a => a.overdue && a.status !== "Completed").length, tint: "text-rose-700", tintBg: "bg-rose-50", icon: AlertTriangle },
    { label: "Completed Today", value: schedule.value.filter(t => t.status === "Completed" && String(t.completedDate || "").slice(0, 10) === todayISO).length, tint: "text-emerald-700", tintBg: "bg-emerald-50", icon: CheckCircle2 },
    { label: "Tomorrow's Schedule", value: appointments.value.filter(a => a.dueISO === toISODate(tomorrow) && !a.overdue).length, tint: "text-sky-700", tintBg: "bg-sky-50", icon: Clock },
    { label: "Children Scheduled This Month", value: new Set(appointments.value.filter(a => a.dueDate.getMonth() === today.getMonth() && a.dueDate.getFullYear() === today.getFullYear()).map(a => a.childID)).size, tint: "text-emerald-700", tintBg: "bg-emerald-50", icon: Users },
  ];
});

/* View toggle */
const view = ref("list"); // "list" | "calendar"

/* Toolbar state */
const searchByOptions = ["Child Name", "Parent Name", "Patient ID"];
const searchBy = ref("Child Name");
const searchQuery = ref("");
const scheduleStatusFilter = ref("Due Today & Overdue");
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

function ageMonths(birth) {
  const b = new Date(birth);
  return (today.getFullYear() - b.getFullYear()) * 12 + (today.getMonth() - b.getMonth());
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
  return appointments.value.filter((a) => {
    const q = searchQuery.value.trim().toLowerCase();
    if (q) {
      if (searchBy.value === "Patient ID" && !a.id.toLowerCase().includes(q)) return false;
      if (searchBy.value === "Parent Name" && !a.parent.toLowerCase().includes(q)) return false;
      if (searchBy.value === "Child Name" && !a.child.toLowerCase().includes(q)) return false;
    }
    const f = scheduleStatusFilter.value;
    if (f === "Due Today & Overdue" && !((a.dueISO === todayISO && !a.overdue) || (a.overdue && a.status !== "Completed"))) return false;
    if (f === "Due Today" && !(a.dueISO === todayISO && !a.overdue)) return false;
    if (f === "Upcoming" && a.status !== "Scheduled") return false;
    if (f === "Overdue" && !(a.overdue && a.status !== "Completed")) return false;
    if (f === "Completed" && a.status !== "Completed") return false;
    if (vaccineFilter.value !== "All Vaccines" && !a.vaccines.some((v) => v.name === vaccineFilter.value)) return false;
    if (!inAgeGroup(a)) return false;
    return true;
  });
});

/* Export Schedule — whatever is currently visible in the list */
function exportScheduleCSV() {
  downloadCSV(`vaccine-schedule-${todayISO}.csv`, [
    ["Queue No.", "Patient ID", "Child Name", "Age", "Parent", "Contact", "Due Date", "Vaccines", "Progress", "Status"],
    ...filteredAppointments.value.map((a) => {
      const { done, total } = progressOf(a);
      return [a.queueNo, a.id, a.child, a.age, a.parent, a.contact, a.due,
        a.vaccines.map((v) => `${v.name} (${v.dose})`).join("; "), `${done}/${total}`, a.status];
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
async function addToQueue(appt) {
  queueingKey.value = appt.key;
  try {
    const res = await axios.post(`${API_BASE}/Queue`, { parentID: appt.parentID, childIDs: [appt.childID] });
    flash(`${appt.child} added to today's queue as #${res.data.queueNumber}.`);
    await loadData();
  } catch (e) {
    if (e.response?.status === 409) {
      flash(`${appt.parent} already has queue #${e.response.data.queueNumber} today — add ${appt.child} to that visit from Queue Management.`);
    } else {
      flash(e.response?.data?.message || "Could not add to the queue.");
    }
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

/* Right sidebar */
const todaysSummary = computed(() => {
  const due = appointments.value.filter(a => a.dueISO === todayISO && !a.overdue);
  return [
    { label: "Children Due Today", value: due.length, icon: CalendarDays, tint: "text-sky-700" },
    { label: "Completed", value: due.filter(a => a.status === "Completed").length, icon: CheckCircle2, tint: "text-emerald-700" },
    { label: "In Queue", value: appointments.value.filter(a => a.inQueue && a.status !== "Completed").length, icon: Clock, tint: "text-amber-700" },
    { label: "Not Yet Arrived", value: due.filter(a => a.status === "Due Today").length, icon: AlertTriangle, tint: "text-rose-700" },
  ];
});

const upcomingVaccines = computed(() => {
  const inWeek = new Date(today); inWeek.setDate(inWeek.getDate() + 7);
  const map = {};
  schedule.value
    .filter(t => t.displayStatus === "Upcoming" && new Date(t.scheduledDate) <= inWeek)
    .forEach(t => { map[t.vaccineName] = (map[t.vaccineName] || 0) + 1; });
  return Object.entries(map).map(([vaccine, count]) => ({ vaccine, count, window: "Next 7 days" })).sort((a, b) => b.count - a.count);
});

const overdueList = computed(() => appointments.value.filter((a) => a.overdue && a.status !== "Completed").slice(0, 6));

/* Vaccine distribution — doses scheduled in the next 30 days */
// Doses due in the next 30 days (incl. overdue) next to the usable stock
// (active, unexpired batches), so staff can see a shortage coming.
const distribution = computed(() => {
  const inMonth = new Date(today); inMonth.setDate(inMonth.getDate() + 30);
  const map = {};
  schedule.value
    .filter(t => t.status !== "Completed" && new Date(t.scheduledDate) <= inMonth)
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

// ── PAGINATION ────────────────────────────────────────────────────
const schedPage     = ref(1)
const schedPageSize = ref(10)
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

/* ---------------- Calendar view ---------------- */
const calendarViewDate = ref(new Date(today.getFullYear(), today.getMonth(), 1));

const calendarMonthLabel = computed(() =>
  calendarViewDate.value.toLocaleDateString("en-US", { month: "long", year: "numeric" })
);

function goToPrevMonth() {
  const d = new Date(calendarViewDate.value);
  d.setMonth(d.getMonth() - 1);
  calendarViewDate.value = d;
  selectedDay.value = null;
}
function goToNextMonth() {
  const d = new Date(calendarViewDate.value);
  d.setMonth(d.getMonth() + 1);
  calendarViewDate.value = d;
  selectedDay.value = null;
}

function isToday(day) {
  return (
    day === today.getDate() &&
    calendarViewDate.value.getFullYear() === today.getFullYear() &&
    calendarViewDate.value.getMonth() === today.getMonth()
  );
}

// Calendar shows every scheduled visit on its actual date (overdue rows
// are shown on the date they were originally due).
const calendarAppointments = computed(() => appointments.value);

const dueCountByDay = computed(() => {
  const map = {};
  const viewYear = calendarViewDate.value.getFullYear();
  const viewMonth = calendarViewDate.value.getMonth();
  calendarAppointments.value.forEach((a) => {
    if (a.dueDate.getFullYear() === viewYear && a.dueDate.getMonth() === viewMonth) {
      const day = a.dueDate.getDate();
      map[day] = (map[day] || 0) + 1;
    }
  });
  return map;
});

const calendarCells = computed(() => {
  const year = calendarViewDate.value.getFullYear();
  const month = calendarViewDate.value.getMonth();
  const firstWeekday = new Date(year, month, 1).getDay();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const cells = [];
  for (let i = 0; i < firstWeekday; i++) cells.push(null);
  for (let d = 1; d <= daysInMonth; d++) cells.push(d);
  return cells;
});

const selectedDay = ref(null);
function selectDay(day) {
  if (!day) return;
  selectedDay.value = day;
}
const selectedDayLabel = computed(() => {
  if (!selectedDay.value) return "";
  const d = new Date(calendarViewDate.value.getFullYear(), calendarViewDate.value.getMonth(), selectedDay.value);
  return formatDate(d);
});
const selectedDaySchedule = computed(() => {
  if (!selectedDay.value) return [];
  const year = calendarViewDate.value.getFullYear();
  const month = calendarViewDate.value.getMonth();
  return calendarAppointments.value.filter(
    (a) =>
      a.dueDate.getFullYear() === year &&
      a.dueDate.getMonth() === month &&
      a.dueDate.getDate() === selectedDay.value
  );
});
</script>

<template>
  <div class="flex min-h-screen w-full bg-stone-50 text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;" @click="actionsMenuOpenFor = null">
    <StaffSidebar />

    <main class="flex-1 min-w-0">
      <StaffTopbar title="Vaccine Schedule" breadcrumb="Aruga / Vaccine Schedule" />

      <div class="px-8 py-6 space-y-6">
        <div v-if="toast" class="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-[13px] text-emerald-800">{{ toast }}</div>
        <div v-if="loadError" class="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-[13px] text-rose-700">{{ loadError }}</div>

        <!-- Summary cards -->
        <div class="grid grid-cols-6 gap-4">
          <div v-for="c in summaryCards" :key="c.label" class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
            <div class="flex items-center justify-between">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500 leading-tight">{{ c.label }}</p>
              <div class="flex h-7 w-7 items-center justify-center rounded-lg shrink-0" :class="c.tintBg">
                <component :is="c.icon" :size="14" :class="c.tint" />
              </div>
            </div>
            <p class="text-[24px] font-bold mt-2">{{ loading ? '…' : c.value }}</p>
          </div>
        </div>

        <!-- Toolbar -->
        <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
          <div class="flex flex-wrap items-center gap-3">
            <div class="flex items-center gap-2 flex-1 min-w-[300px] rounded-xl border border-stone-200 px-3 py-2.5">
              <Search :size="16" class="text-stone-400 shrink-0" />
              <input v-model="searchQuery" type="text" :placeholder="`Search by ${searchBy.toLowerCase()}...`" class="flex-1 min-w-0 text-[13px] outline-none placeholder:text-stone-400" />
              <div class="relative shrink-0">
                <select v-model="searchBy" class="appearance-none bg-stone-50 rounded-lg pl-3 pr-7 py-1.5 text-[12px] font-medium text-stone-600 outline-none cursor-pointer">
                  <option v-for="opt in searchByOptions" :key="opt" :value="opt">{{ opt }}</option>
                </select>
                <ChevronDown :size="13" class="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 text-stone-400" />
              </div>
            </div>

            <div class="relative">
              <select v-model="scheduleStatusFilter" @change="schedPage = 1" class="appearance-none rounded-xl border border-stone-200 pl-9 pr-8 py-2.5 text-[13px] font-medium text-stone-600 outline-none cursor-pointer">
                <option>Due Today &amp; Overdue</option>
                <option>Due Today</option>
                <option>Upcoming</option>
                <option>Overdue</option>
                <option>Completed</option>
                <option>All</option>
              </select>
              <Filter :size="14" class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-stone-400" />
              <ChevronDown :size="13" class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-stone-400" />
            </div>

            <div class="relative">
              <select v-model="vaccineFilter" class="appearance-none rounded-xl border border-stone-200 pl-9 pr-8 py-2.5 text-[13px] font-medium text-stone-600 outline-none cursor-pointer">
                <option v-for="v in vaccineOptions" :key="v">{{ v }}</option>
              </select>
              <Syringe :size="14" class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-stone-400" />
              <ChevronDown :size="13" class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-stone-400" />
            </div>

            <div class="relative">
              <select v-model="ageGroupFilter" class="appearance-none rounded-xl border border-stone-200 pl-3 pr-8 py-2.5 text-[13px] font-medium text-stone-600 outline-none cursor-pointer">
                <option>All Ages</option>
                <option>Birth</option>
                <option>0–6 Months</option>
                <option>7–12 Months</option>
                <option>1–2 Years</option>
              </select>
              <ChevronDown :size="13" class="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-stone-400" />
            </div>

            <div class="flex-1"></div>

            <div class="flex items-center rounded-xl border border-stone-200 p-1">
              <button @click="view = 'list'" class="flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[12.5px] font-medium" :class="view === 'list' ? 'bg-emerald-700 text-white' : 'text-stone-500'">
                <List :size="14" /> List
              </button>
              <button @click="view = 'calendar'" class="flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[12.5px] font-medium" :class="view === 'calendar' ? 'bg-emerald-700 text-white' : 'text-stone-500'">
                <CalendarDays :size="14" /> Calendar
              </button>
            </div>

            <button @click="loadData" class="flex items-center gap-2 rounded-xl px-3 py-2.5 text-[13px] font-medium border border-stone-200 hover:bg-stone-50" title="Refresh">
              <RefreshCw :size="15" class="text-stone-500" :class="loading ? 'animate-spin' : ''" />
            </button>
            <button @click="exportScheduleCSV" class="flex items-center gap-2 rounded-xl px-4 py-2.5 text-[13px] font-medium border border-stone-200 hover:bg-stone-50">
              <Download :size="16" class="text-stone-500" /> Export Schedule
            </button>
          </div>
        </div>

        <!-- Content grid: main + right sidebar -->
        <div class="grid grid-cols-12 gap-6">
          <div class="col-span-8 space-y-6">
            <!-- LIST VIEW -->
            <div v-if="view === 'list'" class="rounded-2xl border border-stone-200 bg-white shadow-sm overflow-hidden">
              <div class="flex items-center justify-between px-5 py-4 border-b border-stone-200">
                <p class="text-[14px] font-semibold">{{ scheduleStatusFilter === 'All' ? 'All Scheduled Visits' : scheduleStatusFilter }}</p>
                <span class="text-[12px] text-stone-500">{{ filteredAppointments.length }} visit{{ filteredAppointments.length === 1 ? '' : 's' }}</span>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-[13px] table-fixed">
                  <colgroup>
                    <col class="w-9" /><col class="w-[64px]" /><col class="w-[160px]" /><col class="w-[170px]" />
                    <col class="w-[70px]" /><col class="w-[100px]" /><col class="w-[110px]" /><col class="w-[150px]" />
                  </colgroup>
                  <thead>
                    <tr class="bg-stone-50">
                      <th v-for="(h, i) in ['', 'Queue', 'Child', 'Vaccines Due', 'Progress', 'Due Date', 'Status', '']" :key="i"
                        class="text-left font-semibold px-4 py-3 whitespace-nowrap text-[11px] uppercase tracking-wide text-stone-500"
                        :class="i === 7 ? 'sticky right-0 z-10 bg-stone-50 shadow-[-8px_0_8px_-8px_rgba(0,0,0,0.15)]' : ''">{{ h }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <template v-for="appt in paginatedAppointments" :key="appt.key">
                      <tr class="border-t border-stone-200 cursor-pointer hover:bg-stone-50/60" @click="toggleExpand(appt.key)">
                        <td class="px-3 py-3 text-center">
                          <ChevronUp v-if="expandedRows.has(appt.key)" :size="15" class="text-stone-400 mx-auto" />
                          <ChevronDown v-else :size="15" class="text-stone-400 mx-auto" />
                        </td>
                        <td class="px-4 py-3 font-medium whitespace-nowrap">{{ appt.queueNo }}</td>
                        <td class="px-4 py-3">
                          <p class="font-medium truncate">{{ appt.child }}</p>
                          <p class="text-[11px] text-stone-500 truncate">{{ appt.age }} · {{ appt.parent }}</p>
                        </td>
                        <td class="px-4 py-3">
                          <div class="flex flex-wrap items-center gap-1.5">
                            <span v-for="(v, i) in appt.vaccines.slice(0, 3)" :key="i" class="inline-flex rounded-full bg-sky-50 text-sky-700 text-[11px] font-medium px-2 py-1 whitespace-nowrap">{{ v.short }}</span>
                            <span v-if="appt.vaccines.length > 3" class="inline-flex rounded-full bg-stone-100 text-stone-600 text-[11px] font-medium px-2 py-1 whitespace-nowrap">+{{ appt.vaccines.length - 3 }}</span>
                          </div>
                        </td>
                        <td class="px-4 py-3 whitespace-nowrap">
                          <span class="text-[11px] font-semibold text-stone-600">{{ progressOf(appt).done }}/{{ progressOf(appt).total }}</span>
                        </td>
                        <td class="px-4 py-3 whitespace-nowrap" :class="appt.overdue && appt.status !== 'Completed' ? 'text-rose-700 font-medium' : 'text-stone-600'">{{ appt.due }}</td>
                        <td class="px-4 py-3 whitespace-nowrap">
                          <span class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium" :class="statusStyle[appt.status]">
                            <span class="h-1.5 w-1.5 rounded-full shrink-0" :class="statusDot[appt.status]" />
                            {{ appt.status }}
                          </span>
                        </td>
                        <td class="sticky right-0 bg-white px-4 py-3 whitespace-nowrap shadow-[-8px_0_8px_-8px_rgba(0,0,0,0.15)]"
                          :class="actionsMenuOpenFor === appt.key ? 'z-30' : 'z-10'" @click.stop>
                          <div class="flex items-center gap-1 relative">
                            <button v-if="canQueue(appt)" @click="addToQueue(appt)" :disabled="queueingKey === appt.key"
                              class="flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-[12px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 disabled:opacity-50"
                              title="Add this child to today's queue">
                              <ListPlus :size="14" /> Add to Queue
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
                        <td colspan="8" class="px-6 py-4">
                          <div class="flex items-center justify-between mb-2">
                            <p class="text-[12.5px] font-semibold text-stone-700">
                              {{ appt.overdue ? 'Overdue doses (catch-up)' : 'Vaccines scheduled for this visit' }} · Parent contact: {{ appt.contact }}
                            </p>
                            <span class="text-[12px] font-semibold text-emerald-700">{{ progressOf(appt).done }} / {{ progressOf(appt).total }} Completed</span>
                          </div>
                          <div class="grid grid-cols-2 gap-2">
                            <div v-for="(v, i) in appt.vaccines" :key="i" class="flex items-center gap-2.5 rounded-lg bg-white border border-stone-200 px-3 py-2">
                              <CheckSquare v-if="v.done" :size="16" class="text-emerald-700 shrink-0" />
                              <Square v-else :size="16" class="text-stone-400 shrink-0" />
                              <span class="text-[12.5px]" :class="v.done ? 'text-stone-500 line-through decoration-stone-300' : 'text-stone-700'">{{ v.name }} {{ v.dose }}</span>
                            </div>
                          </div>
                        </td>
                      </tr>
                    </template>
                    <tr v-if="!loading && filteredAppointments.length === 0">
                      <td colspan="8" class="px-5 py-12 text-center text-[13px] text-stone-400">No visits match the current filters.</td>
                    </tr>
                    <tr v-if="loading">
                      <td colspan="8" class="px-5 py-12 text-center text-[13px] text-stone-400">Loading schedule...</td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <!-- PAGINATION -->
              <div v-if="filteredAppointments.length > 0" class="flex items-center justify-between px-5 py-3.5 border-t border-stone-200 flex-wrap gap-3">
                <div class="flex items-center gap-3">
                  <div class="flex items-center gap-2 text-[12px] text-stone-500">
                    <span>Show</span>
                    <select v-model="schedPageSize" @change="schedPage = 1" class="border border-stone-200 rounded-lg px-2 py-1 text-[12px] text-stone-600 outline-none bg-white">
                      <option :value="10">10</option><option :value="20">20</option><option :value="50">50</option><option :value="100">100</option>
                    </select>
                    <span>entries</span>
                  </div>
                  <p class="text-[12px] text-stone-500">
                    Showing {{ Math.min((schedPage - 1) * schedPageSize + 1, filteredAppointments.length) }}–{{ Math.min(schedPage * schedPageSize, filteredAppointments.length) }}
                    of {{ filteredAppointments.length }} visits
                  </p>
                </div>
                <div class="flex items-center gap-1">
                  <button @click="schedPage--" :disabled="schedPage === 1" class="p-1.5 rounded-lg text-stone-400 hover:text-stone-600 hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed"><ChevronLeft :size="15" /></button>
                  <template v-for="p in schedPaginationPages" :key="p">
                    <span v-if="p === '...'" class="px-1 text-[12px] text-stone-400">…</span>
                    <button v-else @click="schedPage = p" class="min-w-[28px] h-7 rounded-lg text-[12px] font-medium" :class="p === schedPage ? 'bg-emerald-700 text-white' : 'text-stone-600 hover:bg-stone-100'">{{ p }}</button>
                  </template>
                  <button @click="schedPage++" :disabled="schedPage >= schedTotalPages" class="p-1.5 rounded-lg text-stone-400 hover:text-stone-600 hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed"><ChevronRight :size="15" /></button>
                </div>
              </div>
            </div>

            <!-- CALENDAR VIEW -->
            <div v-else class="rounded-2xl border border-stone-200 bg-white shadow-sm p-5">
              <div class="flex items-center justify-between mb-4">
                <p class="text-[14px] font-semibold">{{ calendarMonthLabel }}</p>
                <div class="flex items-center gap-1">
                  <button @click="goToPrevMonth" class="flex h-8 w-8 items-center justify-center rounded-lg border border-stone-200 text-stone-500 hover:bg-stone-50"><ChevronLeft :size="15" /></button>
                  <button @click="goToNextMonth" class="flex h-8 w-8 items-center justify-center rounded-lg border border-stone-200 text-stone-500 hover:bg-stone-50"><ChevronRight :size="15" /></button>
                </div>
              </div>
              <div class="grid grid-cols-7 gap-2 mb-2">
                <div v-for="d in ['Sun','Mon','Tue','Wed','Thu','Fri','Sat']" :key="d" class="text-center text-[11px] font-semibold text-stone-500 uppercase">{{ d }}</div>
              </div>
              <div class="grid grid-cols-7 gap-2">
                <button v-for="(day, i) in calendarCells" :key="i" @click="selectDay(day)" :disabled="!day"
                  class="aspect-square rounded-xl border p-1.5 flex flex-col items-start justify-between text-left transition-colors"
                  :class="[!day ? 'border-transparent' : 'border-stone-200 hover:border-emerald-400', day === selectedDay ? 'ring-2 ring-emerald-500 border-emerald-400' : '', isToday(day) ? 'bg-emerald-50' : 'bg-white']">
                  <span v-if="day" class="text-[12px] font-medium" :class="isToday(day) ? 'text-emerald-800' : 'text-stone-700'">{{ day }}</span>
                  <span v-if="day && dueCountByDay[day]" class="text-[10px] font-semibold px-1.5 py-0.5 rounded-full bg-emerald-600 text-white self-end">{{ dueCountByDay[day] }}</span>
                </button>
              </div>

              <div v-if="selectedDay" class="mt-5 rounded-xl bg-stone-50 p-4">
                <div class="flex items-center justify-between mb-2">
                  <p class="text-[13px] font-semibold">{{ selectedDayLabel }} — Scheduled Visits</p>
                  <button @click="selectedDay = null" class="p-1 rounded-lg hover:bg-stone-200"><X :size="14" class="text-stone-500" /></button>
                </div>
                <div v-if="selectedDaySchedule.length === 0" class="text-[12.5px] text-stone-500 py-2">No visits scheduled on this date.</div>
                <div v-else class="space-y-2">
                  <div v-for="appt in selectedDaySchedule" :key="appt.key" class="flex items-center justify-between rounded-lg bg-white px-3 py-2 border border-stone-200">
                    <div>
                      <p class="text-[12.5px] font-medium">{{ appt.child }}</p>
                      <p class="text-[11px] text-stone-500">{{ appt.vaccines.map(v => v.short).join(', ') }}</p>
                    </div>
                    <span class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-medium" :class="statusStyle[appt.status]">{{ appt.status }}</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Vaccine distribution -->
            <div class="rounded-2xl border border-stone-200 bg-white shadow-sm p-5">
              <p class="text-[14px] font-semibold mb-1">Doses Needed — Next 30 Days</p>
              <p class="text-[11.5px] text-stone-500 mb-4">Doses due (including overdue) compared with the usable stock on hand.</p>
              <div class="space-y-3">
                <div v-for="d in distribution" :key="d.name" class="flex items-center gap-3">
                  <span class="w-28 text-[12px] text-stone-500 shrink-0">{{ d.name }}</span>
                  <div class="flex-1 h-2.5 rounded-full bg-stone-100 overflow-hidden">
                    <div class="h-full rounded-full" :class="d.color" :style="{ width: (d.value / distMax) * 100 + '%' }"></div>
                  </div>
                  <span class="w-8 text-right text-[12px] font-semibold">{{ d.value }}</span>
                  <span class="w-32 text-right text-[11px]" :class="d.short ? 'text-rose-600 font-semibold' : 'text-stone-400'">
                    {{ d.short ? `short by ${d.short}` : `${d.stock} in stock` }}
                  </span>
                </div>
                <p v-if="!distribution.length" class="text-[12.5px] text-stone-400">No doses scheduled in the next 30 days.</p>
              </div>
            </div>
          </div>

          <!-- Right sidebar -->
          <div class="col-span-4 space-y-6">
            <div class="rounded-2xl border border-stone-200 bg-white shadow-sm p-5">
              <div class="flex items-center justify-between mb-3">
                <p class="text-[14px] font-semibold">Today's Summary</p>
                <span class="text-[11.5px] text-stone-500">{{ formatDate(today) }}</span>
              </div>
              <div class="space-y-2">
                <div v-for="t in todaysSummary" :key="t.label" class="flex items-center justify-between rounded-xl bg-stone-50 px-3 py-2.5">
                  <div class="flex items-center gap-2">
                    <component :is="t.icon" :size="15" :class="t.tint" />
                    <span class="text-[12.5px] text-stone-600">{{ t.label }}</span>
                  </div>
                  <span class="text-[13px] font-semibold">{{ t.value }}</span>
                </div>
              </div>
            </div>

            <div class="rounded-2xl border border-stone-200 bg-white shadow-sm p-5">
              <p class="text-[14px] font-semibold mb-3">Upcoming Vaccines</p>
              <div class="space-y-2">
                <div v-for="u in upcomingVaccines" :key="u.vaccine" class="flex items-center justify-between rounded-xl bg-stone-50 px-3 py-2.5">
                  <div>
                    <p class="text-[12.5px] font-medium">{{ u.vaccine }}</p>
                    <p class="text-[11px] text-stone-500">{{ u.window }}</p>
                  </div>
                  <span class="text-[13px] font-semibold text-sky-700">{{ u.count }}</span>
                </div>
                <p v-if="!upcomingVaccines.length" class="text-[12.5px] text-stone-500 py-2">Nothing due in the next 7 days.</p>
              </div>
            </div>

            <div class="rounded-2xl border border-stone-200 bg-white shadow-sm p-5">
              <div class="flex items-center justify-between mb-3">
                <p class="text-[14px] font-semibold">Overdue List</p>
                <button @click="scheduleStatusFilter = 'Overdue'; view = 'list'" class="text-[11.5px] text-rose-700 font-medium hover:underline">View all</button>
              </div>
              <div class="space-y-2">
                <div v-for="appt in overdueList" :key="appt.key" class="flex items-center justify-between rounded-xl bg-rose-50 px-3 py-2.5 gap-2">
                  <div class="min-w-0">
                    <p class="text-[12.5px] font-medium truncate">{{ appt.child }}</p>
                    <p class="text-[11px] text-stone-500 truncate">{{ appt.vaccines.length }} dose(s) · since {{ appt.due }} · {{ appt.contact }}</p>
                  </div>
                  <span class="text-[10.5px] font-semibold text-rose-700 shrink-0">{{ appt.status === 'Delayed' ? 'Delayed' : appt.status }}</span>
                </div>
                <p v-if="overdueList.length === 0" class="text-[12.5px] text-stone-500 py-2">No overdue children.</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  </div>
</template>
