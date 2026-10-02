<template>
  <div class="flex h-screen w-full bg-stone-50 text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;">
    <StaffSidebar />

    <div class="flex-1 flex flex-col overflow-hidden">
      <StaffTopbar title="Queue Management" breadcrumb="Aruga / Queue Management">
        <button
          @click="loadQueues"
          :disabled="isLoading"
          class="flex items-center gap-2 rounded-lg border border-stone-200 px-3 py-2 text-[12px] font-medium text-stone-600 hover:bg-stone-50 disabled:opacity-50"
        >
          <RefreshCw :size="14" :class="{ 'animate-spin': isLoading }" />
          Refresh
        </button>
      </StaffTopbar>

      <main class="flex-1 overflow-y-auto p-8">
        <div class="max-w-6xl mx-auto space-y-6">

          <!-- Summary -->
          <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">Total Entries</p>
              <p class="text-[24px] font-bold mt-1.5">{{ filteredQueues.length }}</p>
            </div>
            <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">Waiting</p>
              <p class="text-[24px] font-bold mt-1.5 text-amber-700">{{ countByStatus('Waiting') }}</p>
            </div>
            <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">In Progress</p>
              <p class="text-[24px] font-bold mt-1.5 text-violet-700">{{ countByStatus('InProgress') }}</p>
            </div>
            <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">Completed</p>
              <p class="text-[24px] font-bold mt-1.5 text-emerald-700">{{ countByStatus('Completed') }}</p>
            </div>
          </div>

          <!-- Filters -->
          <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm flex flex-wrap items-center gap-3">
            <div class="relative flex-1 min-w-[220px]">
              <Search :size="15" class="absolute left-3 top-1/2 -translate-y-1/2 text-stone-400" />
              <input
                v-model="search"
                type="text"
                placeholder="Search by child or parent name..."
                class="w-full pl-9 pr-3 py-2.5 text-[13px] border border-stone-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>
            <select
              v-model="statusFilter"
              class="px-3 py-2.5 text-[13px] border border-stone-200 rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            >
              <option value="">All statuses</option>
              <option v-for="s in availableStatuses" :key="s" :value="s">{{ statusLabel(s) }}</option>
            </select>
            <input
              v-model="dateFilter"
              type="date"
              class="px-3 py-2.5 text-[13px] border border-stone-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
            <button
              v-if="search || statusFilter || dateFilter"
              @click="search = ''; statusFilter = ''; dateFilter = ''"
              class="text-[12px] font-medium text-stone-500 hover:text-stone-700 px-2"
            >
              Clear filters
            </button>
          </div>

          <!-- Table -->
          <div class="bg-white border border-stone-200 rounded-2xl shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-[13px]">
              <thead>
                <tr class="bg-stone-50 border-b border-stone-200">
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Queue No.</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Child(ren)</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Parent / Guardian</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Date</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500">Status</th>
                  <th class="px-5 py-3 text-[11px] font-semibold uppercase tracking-wide text-stone-500 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-stone-100">
                <template v-for="q in paginatedQueues" :key="q.queueID">
                  <tr class="hover:bg-stone-50/60 transition-colors">
                    <td class="px-5 py-3 font-medium whitespace-nowrap">{{ q.no }}</td>
                    <td class="px-5 py-3">{{ q.child }}</td>
                    <td class="px-5 py-3 text-stone-500">{{ q.parent }}</td>
                    <td class="px-5 py-3 text-stone-500 whitespace-nowrap">{{ formatDate(q.queueDate) }}</td>
                    <td class="px-5 py-3">
                      <select
                        :value="q.status"
                        @change="changeStatus(q, $event.target.value)"
                        :disabled="updatingId === q.queueID"
                        class="text-[12px] font-medium rounded-full pl-2.5 pr-6 py-1 border-none outline-none cursor-pointer disabled:opacity-50"
                        :class="statusStyle[q.status] || 'bg-stone-100 text-stone-600'"
                      >
                        <!-- "In Progress" = called into the vaccination room; that is set with Call Next on the Dashboard -->
                        <option v-for="s in availableStatuses" :key="s" :value="s" :disabled="s === 'InProgress' && q.status !== 'InProgress'">{{ statusLabel(s) }}</option>
                      </select>
                    </td>
                    <td class="px-5 py-3 text-right">
                      <div class="flex items-center justify-end gap-1">
                        <button @click="toggleExpand(q)" class="p-1.5 rounded-lg hover:bg-stone-100" title="View details">
                          <Eye :size="15" class="text-stone-500" />
                        </button>
                        <button @click="deleteQueue(q)" class="p-1.5 rounded-lg hover:bg-rose-50" title="Delete entry">
                          <Trash2 :size="15" class="text-rose-500" />
                        </button>
                      </div>
                    </td>
                  </tr>
                  <tr v-if="expandedId === q.queueID" class="bg-stone-50 border-t border-stone-100">
                    <td colspan="6" class="px-5 py-3 text-[12.5px] text-stone-600">
                      <span class="font-medium text-stone-800">Queue {{ q.no }}</span> ·
                      Barangay: {{ q.barangayNo || '—' }} ·
                      Children: {{ q.child }} ·
                      Requested by: {{ q.parent }}
                    </td>
                  </tr>
                </template>

                <tr v-if="!isLoading && filteredQueues.length === 0">
                  <td colspan="6" class="px-6 py-20 text-center">
                    <div class="flex flex-col items-center">
                      <ClipboardList class="w-12 h-12 text-stone-200 mb-2" />
                      <p class="text-stone-400 font-medium">
                        {{ queues.length === 0 ? 'No queue entries found.' : 'No entries match your filters.' }}
                      </p>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
            </div>

            <!-- Pagination -->
            <div v-if="filteredQueues.length > 0" class="flex items-center justify-between px-6 py-3.5 border-t border-stone-100 flex-wrap gap-3">
              <div class="flex items-center gap-3">
                <div class="flex items-center gap-2 text-xs text-stone-500">
                  <span>Show</span>
                  <select v-model="pageSize" @change="currentPage = 1"
                    class="border border-stone-200 rounded-lg px-2 py-1 text-xs text-stone-600 outline-none bg-white">
                    <option :value="10">10</option>
                    <option :value="20">20</option>
                    <option :value="50">50</option>
                  </select>
                  <span>entries</span>
                </div>
                <p class="text-xs text-stone-400">
                  Showing {{ Math.min((currentPage - 1) * pageSize + 1, filteredQueues.length) }}–{{ Math.min(currentPage * pageSize, filteredQueues.length) }}
                  of {{ filteredQueues.length }}
                </p>
              </div>
              <div class="flex items-center gap-1">
                <button @click="currentPage--" :disabled="currentPage === 1"
                  class="p-1.5 rounded-lg text-stone-400 hover:text-stone-600 hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
                  <ChevronLeft class="w-4 h-4" />
                </button>
                <span class="text-xs text-stone-500 px-2">{{ currentPage }} / {{ totalPages }}</span>
                <button @click="currentPage++" :disabled="currentPage >= totalPages"
                  class="p-1.5 rounded-lg text-stone-400 hover:text-stone-600 hover:bg-stone-100 disabled:opacity-40 disabled:cursor-not-allowed transition-colors">
                  <ChevronRight class="w-4 h-4" />
                </button>
              </div>
            </div>
          </div>

          <p v-if="loadError" class="text-[12.5px] text-rose-600">{{ loadError }}</p>
        </div>
      </main>
    </div>
  </div>
</template>

<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, onMounted } from "vue";
import axios from "axios";
import {
  RefreshCw, Search, Eye, Trash2, ClipboardList, ChevronLeft, ChevronRight,
} from "lucide-vue-next";
import StaffSidebar from "./StaffSidebar.vue";
import StaffTopbar from "./StaffTopbar.vue";
import { withRelationship } from "@/utils/format";
import { askConfirm, showAlert } from "@/utils/dialog";

const API_BASE = `${API_ORIGIN}/api`;

// Unlike the Dashboard (which calls GET /api/Queue/today, scoped
// server-side to today), this page is the intentional full-history view —
// it calls GetAll and lets staff filter/search across every date.
const queues = ref([]);
const isLoading = ref(false);
const loadError = ref(null);
const updatingId = ref(null);

const search = ref("");
const statusFilter = ref("");
const dateFilter = ref("");
const expandedId = ref(null);
const currentPage = ref(1);
const pageSize = ref(20);

const statusStyle = {
  Waiting: "bg-amber-50 text-amber-700",
  InProgress: "bg-violet-50 text-violet-700",
  Completed: "bg-emerald-50 text-emerald-700",
};

const statusLabels = {
  Waiting: "Waiting",
  InProgress: "In Progress",
  Completed: "Completed",
};
const statusLabel = (s) => statusLabels[s] || s;

// Filter dropdown options come from whatever statuses actually exist in
// the fetched data (plus the ones the backend is known to produce), rather
// than a hardcoded list that could drift from what CallNext/Complete
// actually write to Queues.Status.
const availableStatuses = computed(() => {
  const known = new Set(["Waiting", "InProgress", "Completed"]);
  queues.value.forEach((q) => known.add(q.status));
  return Array.from(known);
});

const loadQueues = async () => {
  isLoading.value = true;
  loadError.value = null;
  try {
    const response = await axios.get(`${API_BASE}/Queue`);
    queues.value = response.data.map((q) => ({
      queueID: q.queueID,
      queueNumber: q.queueNumber,
      no: `Q-${String(q.queueNumber).padStart(3, "0")}`,
      child: q.children?.map((c) => c.name).join(", ") || "—",
      // "Rosario Santos (Grandmother)": who brought the child
      parent: withRelationship(q.requestBy, q.requestByRelationship),
      barangayNo: q.barangayNo,
      queueDate: q.queueDate,
      status: q.status || "Waiting",
    }));
  } catch (error) {
    console.error("Queue Management load error:", error);
    loadError.value = "Could not load queue entries. Please try refreshing.";
  } finally {
    isLoading.value = false;
  }
};

const formatDate = (dateStr) => {
  if (!dateStr) return "—";
  return new Date(dateStr).toLocaleDateString("en-PH", {
    year: "numeric", month: "short", day: "numeric",
  });
};

const isSameDate = (dateStr, isoDateInput) => {
  if (!dateStr || !isoDateInput) return true;
  const d = new Date(dateStr);
  const [y, m, day] = isoDateInput.split("-").map(Number);
  return d.getFullYear() === y && d.getMonth() + 1 === m && d.getDate() === day;
};

const filteredQueues = computed(() => {
  const term = search.value.trim().toLowerCase();
  return queues.value.filter((q) => {
    if (statusFilter.value && q.status !== statusFilter.value) return false;
    if (dateFilter.value && !isSameDate(q.queueDate, dateFilter.value)) return false;
    if (term && !q.child.toLowerCase().includes(term) && !q.parent.toLowerCase().includes(term)) return false;
    return true;
  });
});

const countByStatus = (status) =>
  filteredQueues.value.filter((q) => q.status === status).length;

const totalPages = computed(() =>
  Math.max(1, Math.ceil(filteredQueues.value.length / pageSize.value))
);

const paginatedQueues = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredQueues.value.slice(start, start + pageSize.value);
});

const toggleExpand = (q) => {
  expandedId.value = expandedId.value === q.queueID ? null : q.queueID;
};

const changeStatus = async (q, newStatus) => {
  if (newStatus === q.status) return;
  updatingId.value = q.queueID;
  q.status = newStatus; // optimistic
  try {
    await axios.put(`${API_BASE}/Queue/${q.queueID}/status`, { status: newStatus });
  } catch (error) {
    console.error("Status update error:", error);
    // Re-fetch from the server to show what's actually saved, rather than
    // blindly reverting to the pre-edit value.
    await loadQueues();
    showAlert({
      title: "The status wasn't changed",
      message: error.response?.data?.message || "The list has been refreshed to show the current state.",
      tone: "error",
    });
  } finally {
    updatingId.value = null;
  }
};

const deleteQueue = async (q) => {
  const ok = await askConfirm({
    title: `Remove queue entry ${q.no}?`,
    message: `${q.child}, brought by ${q.parent}, on ${formatDate(q.queueDate)}. This can't be undone.`,
    confirmText: "Remove",
    tone: "danger",
  });
  if (!ok) return;
  try {
    await axios.delete(`${API_BASE}/Queue/${q.queueID}`);
    queues.value = queues.value.filter((r) => r.queueID !== q.queueID);
  } catch (error) {
    console.error("Delete queue error:", error);
    showAlert({ title: "Could not delete this entry", message: "Please try again.", tone: "error" });
  }
};

onMounted(loadQueues);
</script>