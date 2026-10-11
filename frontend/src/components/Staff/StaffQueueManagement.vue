<!--
  StaffQueueManagement.vue  —  /staff/queue-management
  The clinic's daily log of the queue: who checked in on a given day, who
  brought each child, and how each visit ended. Read-only: today's queue is
  run from the Dashboard (Check-In, Call Next, Remove from Queue), and past
  days stay exactly as they ended.
-->
<template>
  <div class="flex h-screen w-full bg-stone-50 text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;">
    <StaffSidebar />

    <div class="flex-1 flex flex-col overflow-hidden">
      <StaffTopbar title="Queue Management" breadcrumb="Aruga / Queue Management">
        <button
          @click="load(selectedDate)"
          :disabled="isLoading"
          class="flex items-center gap-2 rounded-lg border border-stone-200 px-3 py-2 text-[12px] font-medium text-stone-600 hover:bg-stone-50 disabled:opacity-50"
        >
          <RefreshCw :size="14" :class="{ 'animate-spin': isLoading }" />
          Refresh
        </button>
      </StaffTopbar>

      <main class="flex-1 overflow-y-auto p-4 sm:p-8">
        <div class="max-w-6xl mx-auto space-y-6">

          <!-- Day picker -->
          <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm flex flex-wrap items-center gap-3">
            <div class="flex items-center gap-1">
              <button @click="previousDate && load(previousDate)" :disabled="!previousDate || isLoading"
                class="p-2 rounded-lg border border-stone-200 hover:bg-stone-50 disabled:opacity-40 disabled:cursor-not-allowed"
                title="Previous day with check-ins">
                <ChevronLeft :size="16" class="text-stone-600" />
              </button>
              <div class="px-3 min-w-[230px] text-center">
                <p class="text-[15px] font-semibold">{{ dayLabel }}</p>
                <p class="text-[11.5px] text-stone-500">{{ isToday ? "Today" : isFuture ? "Upcoming" : "Past day · view only" }}</p>
              </div>
              <button @click="nextDate && load(nextDate)" :disabled="!nextDate || isLoading"
                class="p-2 rounded-lg border border-stone-200 hover:bg-stone-50 disabled:opacity-40 disabled:cursor-not-allowed"
                title="Next day with check-ins">
                <ChevronRight :size="16" class="text-stone-600" />
              </button>
            </div>

            <input
              type="date"
              :value="selectedDate"
              :max="today"
              @change="$event.target.value && load($event.target.value)"
              class="px-3 py-2 text-[13px] border border-stone-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
            <button v-if="!isToday" @click="load(today)"
              class="px-3 py-2 text-[12.5px] font-medium rounded-lg border border-stone-200 text-emerald-700 hover:bg-emerald-50">
              Today
            </button>

            <div class="relative flex-1 min-w-[200px]">
              <Search :size="15" class="absolute left-3 top-1/2 -translate-y-1/2 text-stone-400" />
              <input
                v-model="search"
                type="text"
                placeholder="Search child, parent or who brought them..."
                class="w-full pl-9 pr-3 py-2.5 text-[13px] border border-stone-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
          </div>

          <!-- The day in numbers -->
          <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">Families</p>
              <p class="text-[24px] font-bold mt-1.5">{{ entries.length }}</p>
              <p class="text-[11.5px] text-stone-500">{{ childCount }} {{ childCount === 1 ? "child" : "children" }}</p>
            </div>
            <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">Served</p>
              <p class="text-[24px] font-bold mt-1.5 text-emerald-700">{{ servedCount }}</p>
              <p class="text-[11.5px] text-stone-500">{{ servedCount === 1 ? "visit" : "visits" }} completed</p>
            </div>
            <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
              <template v-if="isToday">
                <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">Still in queue</p>
                <p class="text-[24px] font-bold mt-1.5 text-amber-700">{{ openCount }}</p>
                <p class="text-[11.5px] text-stone-500">waiting or in the room</p>
              </template>
              <template v-else>
                <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">Not served</p>
                <p class="text-[24px] font-bold mt-1.5 text-rose-700">{{ openCount }}</p>
                <p class="text-[11.5px] text-stone-500">checked in, visit not finished</p>
              </template>
            </div>
            <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">Brought by someone else</p>
              <p class="text-[24px] font-bold mt-1.5 text-sky-700">{{ broughtByOthersCount }}</p>
              <p class="text-[11.5px] text-stone-500">{{ broughtByOthersCount === 1 ? "child" : "children" }}, not the parent</p>
            </div>
          </div>

          <p v-if="isToday" class="text-[12.5px] text-stone-500">
            Today's queue is run from the
            <router-link to="/staff/dashboard" class="font-medium text-emerald-700 hover:underline">Dashboard</router-link>
            (Check-In Patient, Call Next, Remove from Queue). This page is the record.
          </p>

          <!-- The log -->
          <div class="bg-white border border-stone-200 rounded-2xl shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-[13px]">
              <thead>
                <tr class="bg-stone-50 border-b border-stone-200">
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Queue No.</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Child(ren)</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Brought by</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Checked in</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Status</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500 text-right">Details</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-stone-100">
                <template v-for="q in filteredEntries" :key="q.queueID">
                  <tr class="hover:bg-stone-50/60 transition-colors">
                    <td class="px-5 py-3 font-medium whitespace-nowrap">{{ q.no }}</td>
                    <td class="px-5 py-3">{{ q.childNames }}</td>
                    <td class="px-5 py-3 text-stone-500">{{ q.broughtBy }}</td>
                    <td class="px-5 py-3 text-stone-500 whitespace-nowrap">{{ formatTime(q.checkedInAt) }}</td>
                    <td class="px-5 py-3">
                      <span class="inline-block text-[12px] font-medium rounded-full px-2.5 py-1" :class="q.outcome.style">
                        {{ q.outcome.label }}
                      </span>
                    </td>
                    <td class="px-5 py-3 text-right">
                      <button @click="expandedId = expandedId === q.queueID ? null : q.queueID"
                        class="p-1.5 rounded-lg hover:bg-stone-100" title="View details">
                        <Eye :size="15" class="text-stone-500" />
                      </button>
                    </td>
                  </tr>
                  <tr v-if="expandedId === q.queueID" class="bg-stone-50 border-t border-stone-100">
                    <td colspan="6" class="px-5 py-3 text-[12.5px] text-stone-600 space-y-1">
                      <p v-for="c in q.children" :key="c.childID">
                        <span class="font-medium text-stone-800">{{ c.name }}</span>
                        — brought by {{ withRelationship(c.broughtBy, c.broughtByRelationship) }}
                      </p>
                      <p class="text-stone-500">
                        Parent on file: {{ q.parentName || "—" }} · {{ barangayLabel(q.barangayNo, "No barangay on file") }}
                      </p>
                    </td>
                  </tr>
                </template>

                <tr v-if="!isLoading && filteredEntries.length === 0">
                  <td colspan="6" class="px-6 py-16 text-center">
                    <div class="flex flex-col items-center">
                      <ClipboardList class="w-12 h-12 text-stone-200 mb-2" />
                      <p class="text-stone-400 font-medium">
                        {{ entries.length === 0 ? "No one checked in on this day." : "No one on this day matches your search." }}
                      </p>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
            </div>
          </div>

          <p v-if="loadError" class="text-[12.5px] text-rose-600">{{ loadError }}</p>
        </div>
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from "vue";
import axios from "axios";
import { RefreshCw, Search, Eye, ClipboardList, ChevronLeft, ChevronRight } from "lucide-vue-next";
import StaffSidebar from "./StaffSidebar.vue";
import StaffTopbar from "./StaffTopbar.vue";
import { API_BASE, toISODate, formatTime, withRelationship } from "@/utils/format";
import { barangayLabel } from "@/utils/barangays";

const today = toISODate();
const selectedDate = ref(today);
const entries = ref([]);
const previousDate = ref(null);
const nextDate = ref(null);
const isLoading = ref(false);
const loadError = ref(null);
const search = ref("");
const expandedId = ref(null);

const isToday = computed(() => selectedDate.value === today);
const isFuture = computed(() => selectedDate.value > today);
const dayLabel = computed(() =>
  new Date(`${selectedDate.value}T00:00:00`).toLocaleDateString("en-PH", {
    weekday: "long", month: "long", day: "numeric", year: "numeric",
  }));

// How a visit ended. On a past day, a family still "Waiting" or "In
// Progress" was never finished (left, or not called before closing).
function outcomeOf(status) {
  if (status === "Completed") return { label: "Completed", style: "bg-emerald-50 text-emerald-700" };
  if (!isToday.value && !isFuture.value) return { label: "Not served", style: "bg-rose-50 text-rose-700" };
  if (status === "InProgress") return { label: "In Progress", style: "bg-violet-50 text-violet-700" };
  return { label: "Waiting", style: "bg-amber-50 text-amber-700" };
}

async function load(date) {
  isLoading.value = true;
  loadError.value = null;
  expandedId.value = null;
  try {
    const { data } = await axios.get(`${API_BASE}/Queue/day`, { params: { date } });
    selectedDate.value = date;
    previousDate.value = data.previousDate ? toISODate(data.previousDate) : null;
    nextDate.value = data.nextDate ? toISODate(data.nextDate) : null;
    entries.value = (data.entries || []).map((q) => ({
      queueID: q.queueID,
      no: `Q-${String(q.queueNumber).padStart(3, "0")}`,
      children: q.children || [],
      childNames: q.children?.map((c) => c.name).join(", ") || "—",
      // "Ana Castillo (Sibling)", or the parent when they came themselves
      broughtBy: withRelationship(q.requestBy, q.requestByRelationship),
      parentName: q.parentName,
      barangayNo: q.barangayNo,
      checkedInAt: q.checkedInAt,
      status: q.status || "Waiting",
    }));
  } catch (error) {
    console.error("Queue Management load error:", error);
    loadError.value = "Could not load this day's queue. Please try refreshing.";
  } finally {
    isLoading.value = false;
  }
}

const withOutcome = computed(() => entries.value.map((q) => ({ ...q, outcome: outcomeOf(q.status) })));

const filteredEntries = computed(() => {
  const term = search.value.trim().toLowerCase();
  if (!term) return withOutcome.value;
  return withOutcome.value.filter((q) =>
    [q.childNames, q.broughtBy, q.parentName].some((s) => (s || "").toLowerCase().includes(term)));
});

const childCount = computed(() => entries.value.reduce((n, q) => n + q.children.length, 0));
const servedCount = computed(() => entries.value.filter((q) => q.status === "Completed").length);
const openCount = computed(() => entries.value.filter((q) => q.status !== "Completed").length);
const broughtByOthersCount = computed(() => entries.value.reduce((n, q) =>
  n + q.children.filter((c) => c.broughtBy && c.broughtBy !== q.parentName).length, 0));

onMounted(() => load(today));
</script>
