<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed, watch, onMounted, onUnmounted } from "vue";
import { useRouter } from "vue-router";
import {
  Syringe, FileText, Search, QrCode, UserPlus, CalendarDays,
  Clock, CheckCircle2, AlertTriangle, Activity, MoreHorizontal,
  Eye, UserCog, DoorOpen, TrendingUp, TrendingDown, RefreshCw,
  ClipboardCheck, X, Megaphone, Undo2, Check, Loader2,
} from "lucide-vue-next";
import StaffSidebar from "./StaffSidebar.vue";
import StaffTopbar from "./StaffTopbar.vue";
import { getToken } from "@/utils/auth";
import { withRelationship } from "@/utils/format";
import { visitProgress, unfinishedChildren } from "@/utils/visitProgress";
import { askConfirm, showAlert } from "@/utils/dialog";

const router = useRouter();

const API_BASE = `${API_ORIGIN}/api`;

const waitingCount = computed(() =>
  queue.value.filter(q => q.status === "Waiting").length
);

const readyCount = computed(() =>
  queue.value.filter(q => q.status === "Ready").length
);

const inProgressCount = computed(() =>
  queue.value.filter(q => q.status === "In Progress").length
);

const completedCount = computed(() =>
  queue.value.filter(q => q.status === "Completed").length
);

const lateCount = computed(() =>
  queue.value.filter(q => q.status === "Late").length
);

const summaryCards = computed(() => [
  {
    label: "Due for Vaccination",
    value: expected.value.length,
    trend: `${expected.value.filter(e => e.status === "Not Arrived").length} not yet arrived`,
    up: null,
    icon: CalendarDays,
    tint: "text-emerald-700",
    tintBg: "bg-emerald-50",
  },
  {
    label: "Patients Waiting",
    value: queue.value.filter(q => q.status === "Waiting").length,
    trend: "today",
    up: null,
    icon: Clock,
    tint: "text-amber-700",
    tintBg: "bg-amber-50",
  },
  {
    label: "In Progress",
    value: queue.value.filter(q => q.status === "In Progress").length,
    trend: "today",
    up: null,
    icon: Activity,
    tint: "text-sky-700",
    tintBg: "bg-sky-50",
  },
  {
    label: "Completed Today",
    value: queue.value.filter(q => q.status === "Completed").length,
    trend: "today",
    up: null,
    icon: CheckCircle2,
    tint: "text-emerald-700",
    tintBg: "bg-emerald-50",
  },
  {
    label: "Late Patients",
    value: queue.value.filter(q => q.status === "Late").length,
    trend: "today",
    up: null,
    icon: AlertTriangle,
    tint: "text-rose-700",
    tintBg: "bg-rose-50",
  },
  {
    label: "Vaccination Room",
    value: nowServing.value ? `#${nowServing.value.queueNumber}` : "Free",
    trend: nowServing.value ? "family inside now" : waitingList.value.length ? "press Call Next" : "nobody waiting",
    up: null,
    icon: DoorOpen,
    tint: "text-sky-700",
    tintBg: "bg-sky-50",
  },
]);

// Each action either routes to the page that actually owns that workflow
// (Register Child / Search Patient) or acts locally on this page (View
// Today's Queue scrolls to the table already on this dashboard). "Scan Queue
// QR" has no scanning feature built anywhere yet, so it's disabled rather
// than pretending to work.
const quickActions = [
  { icon: UserPlus, label: "Register Child", action: () => router.push("/staff/patient-records?tab=children&openRegister=1") },
  { icon: QrCode, label: "Check-in QR", action: () => router.push("/staff/checkin-qr") },
  // For an already-registered patient who walks in without their parent's
  // login (no QR, no parent account access) — this opens a modal to look
  // up the parent/child and add them straight to today's queue, instead of
  // routing to Register Child (which is for brand-new patients).
  { icon: ClipboardCheck, label: "Check-In Patient", action: () => openCheckInModal() },
  { icon: UserPlus, label: "Add Walk-in Patient", action: () => router.push("/staff/patient-records?tab=children&openRegister=1&walkin=1") },
  { icon: Search, label: "Search Patient", action: () => router.push("/staff/patient-records?tab=parents") },
  { icon: CalendarDays, label: "View Today's Queue", action: () => scrollToQueue() },
  { icon: Megaphone, label: "Vaccination Room", action: () => roomSectionRef.value?.scrollIntoView({ behavior: "smooth", block: "start" }) },
];

const queueSectionRef = ref(null);
const roomSectionRef = ref(null);
const scrollToQueue = () => {
  queueSectionRef.value?.scrollIntoView({ behavior: "smooth", block: "start" });
};

const statusStyle = {
  Waiting: "bg-amber-50 text-amber-700",
  Ready: "bg-sky-50 text-sky-700",
  "In Progress": "bg-violet-50 text-violet-700",
  Completed: "bg-emerald-50 text-emerald-700",
  Late: "bg-rose-50 text-rose-700",
};
const statusDot = {
  Waiting: "bg-amber-500",
  Ready: "bg-sky-500",
  "In Progress": "bg-violet-500",
  Completed: "bg-emerald-600",
  Late: "bg-rose-600",
};

const queue = ref([]);
const loadingQueue = ref(false);
const queueError = ref(null);

// The stat cards / donut still want today's Completed count, so `queue`
// keeps completed visits in it — but the active table below shouldn't
// keep showing a patient whose session is already finished.
const visibleQueue = computed(() =>
  queue.value.filter((q) => q.status !== "Completed")
);

// Confirmed from the real ParentsController.cs ([Route("api/[controller]")],
// GetAllParents action mapped to "all") — GetAllParents lives at
// GET /api/Parents/all. Children: GET /api/Children/all.
const PARENTS_BASE = `${API_BASE}/Parents`;
const CHILDREN_BASE = `${API_BASE}/Children`;

// Sends the signed-in staff member's token so the audit log knows who
// did what.
const authJson = () => ({ "Content-Type": "application/json", Authorization: `Bearer ${getToken()}` });

/* -------------------- Vaccination room (Call Next) --------------------
   Leveriza has one vaccination room and one person vaccinating. The Nurse
   presses Call Next: the next family in line is called in, and their phone
   shows "It's your turn" (with a sound) plus one SMS. Then Start
   Vaccinating for each child, and Complete Visit when done. */
const nowServing = computed(() => queue.value.find(q => q.status === "In Progress") || null);
const waitingList = computed(() =>
  queue.value.filter(q => q.status === "Waiting").sort((a, b) => a.queueNumber - b.queueNumber)
);
const roomBusy = ref(false);
const roomMessage = ref("");
const roomError = ref(false);

const flashRoom = (msg, isError = false) => {
  roomMessage.value = msg;
  roomError.value = isError;
  setTimeout(() => { if (roomMessage.value === msg) roomMessage.value = ""; }, 5000);
};

const roomAction = async (url, fallbackError) => {
  roomBusy.value = true;
  try {
    const res = await fetch(url, { method: "PATCH", headers: authJson() });
    const body = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(body.message || fallbackError);
    if (body.message) flashRoom(body.message);
  } catch (error) {
    flashRoom(error.message || fallbackError, true);
  } finally {
    roomBusy.value = false;
    await loadQueue();
  }
};

const callNext = () => roomAction(`${API_BASE}/Queue/call-next`, "Could not call the next family.");
// Out of turn (the next in line stepped out), or "Call Again" for the family already called
const callVisit = (q) => {
  assignMenuOpen.value = null;
  return roomAction(`${API_BASE}/Queue/${q.queueID}/call`, "Could not call this family.");
};
const backToWaiting = async (q) => {
  const ok = await askConfirm({
    title: `${q.no} did not come in?`,
    message: `${q.child}, brought by ${q.parent || "their parent"}, will go back to the waiting list and keep queue number ${q.no}. You can call them again later.`,
    confirmText: "Back to Waiting",
    cancelText: "They're here",
  });
  if (!ok) return;
  return roomAction(`${API_BASE}/Queue/${q.queueID}/back-to-waiting`, "Could not update this family.");
};
// Each child's progress in the family inside (done / not yet), so nobody
// is forgotten before Complete Visit. Reloaded when a new family is called
// in, and when the Nurse comes back to the Dashboard from a visit page.
const roomProgress = ref({});
const loadRoomProgress = async () => {
  roomProgress.value = nowServing.value ? await visitProgress(nowServing.value.children) : {};
};
watch(() => nowServing.value?.queueID, loadRoomProgress);

const completeVisit = async (q) => {
  await loadRoomProgress();
  const left = unfinishedChildren(q.children, roomProgress.value);
  const ok = await askConfirm(left.length
    ? {
        title: `Complete ${q.no} anyway?`,
        message: "These are still due today and not recorded:",
        details: left,
        confirmText: "Complete Anyway",
        cancelText: "Go Back",
        tone: "danger",
      }
    : {
        title: `Complete the visit for ${q.no}?`,
        message: `${q.child}. The parents get a text of the vaccines given today.`,
        confirmText: "Complete Visit",
      });
  if (!ok) return;
  return roomAction(`${API_BASE}/Queue/${q.queueID}/complete`, "Could not complete the visit.");
};
const startVaccinating = (q, c) => router.push(`/staff/vaccination/${q.queueID}?child=${c.childID}`);

/* -------------------- Check-In Patient modal -------------------- */
// Staff-initiated check-in for an existing patient, bypassing the need for
// the parent to log in themselves. Reuses the same POST /api/Queue
// endpoint (ParentID + ChildIDs body) that the parent-facing Checkin.vue
// already uses for its own self-check-in.
const checkInModalOpen = ref(false);
const parentsList = ref([]);
const childrenList = ref([]);
const loadingCheckInData = ref(false);
const parentSearch = ref("");
const selectedParent = ref(null);
const selectedChildIds = ref([]);
const submittingCheckIn = ref(false);
const checkInError = ref(null);

// Confirmed real shapes from ParentsController.cs and ChildrenController.cs:
// Parent: { parentID, firstName, middleName, lastName, email, contactNo,
//   barangayNo, address } — no separate name/fullName field.
// Child (GetAllChildren): { childID, firstName, middleName, lastName,
//   birthDate, ..., parents: [{ parentID, parentName, relationshipType,
//   isPrimaryContact, canReceiveNotifications }] } — parent linkage is a
// many-to-many array (a child can have more than one guardian), NOT a
// single top-level parentID field like the earlier guess assumed.
const getParentId = (p) => p.parentID;
const getParentName = (p) => [p.firstName, p.lastName].filter(Boolean).join(" ") || "Unnamed Parent";
const getChildId = (c) => c.childID;
const childBelongsToParent = (c, parentId) =>
  Array.isArray(c.parents) && c.parents.some((r) => r.parentID === parentId);
const getChildName = (c) =>
  [c.firstName, c.lastName].filter(Boolean).join(" ") || "Unnamed Child";

const openCheckInModal = async () => {
  checkInModalOpen.value = true;
  checkInError.value = null;
  selectedParent.value = null;
  selectedChildIds.value = [];
  parentSearch.value = "";

  if (parentsList.value.length && childrenList.value.length) return; // already loaded this session

  loadingCheckInData.value = true;
  try {
    const [parentsRes, childrenRes] = await Promise.all([
      fetch(`${PARENTS_BASE}/all`),
      fetch(`${CHILDREN_BASE}/all`),
    ]);

    // Report exactly which call failed and why, instead of a generic
    // message — a 404 (wrong route), a 401/403 (auth), and a 500 (server
    // error) all need a different fix, so surfacing the real status here
    // saves a trip to DevTools.
    if (!parentsRes.ok) {
      const body = await parentsRes.text().catch(() => "");
      throw new Error(`GET /api/Parents/all failed — status ${parentsRes.status}. ${body.slice(0, 200)}`);
    }
    if (!childrenRes.ok) {
      const body = await childrenRes.text().catch(() => "");
      throw new Error(`GET /api/Children/all failed — status ${childrenRes.status}. ${body.slice(0, 200)}`);
    }

    parentsList.value = await parentsRes.json();
    childrenList.value = await childrenRes.json();

    if (!Array.isArray(parentsList.value) || !Array.isArray(childrenList.value)) {
      throw new Error("Server responded, but not with the expected array shape — check the API response in DevTools.");
    }
  } catch (error) {
    console.error("Check-in lookup error:", error);
    checkInError.value = error.message || "Couldn't load parent/child records. Please try again.";
  } finally {
    loadingCheckInData.value = false;
  }
};

const closeCheckInModal = () => {
  checkInModalOpen.value = false;
};

const filteredParents = computed(() => {
  const term = parentSearch.value.trim().toLowerCase();
  if (!term) return [];
  return parentsList.value
    .filter((p) => getParentName(p).toLowerCase().includes(term))
    .slice(0, 8);
});

const pickParent = (p) => {
  selectedParent.value = p;
  selectedChildIds.value = [];
  parentSearch.value = "";
};

const childrenForSelectedParent = computed(() => {
  if (!selectedParent.value) return [];
  const parentId = getParentId(selectedParent.value);
  return childrenList.value.filter((c) => childBelongsToParent(c, parentId));
});

const toggleChildSelection = (childId) => {
  const idx = selectedChildIds.value.indexOf(childId);
  if (idx === -1) selectedChildIds.value.push(childId);
  else selectedChildIds.value.splice(idx, 1);
};

const submitCheckIn = async () => {
  if (!selectedParent.value || selectedChildIds.value.length === 0) return;
  submittingCheckIn.value = true;
  checkInError.value = null;
  try {
    const response = await fetch(`${API_BASE}/Queue`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        ParentID: getParentId(selectedParent.value),
        ChildIDs: selectedChildIds.value,
      }),
    });

    if (response.status === 409) {
      checkInError.value = "This patient is already checked in today.";
      return;
    }
    if (!response.ok) {
      throw new Error(`Failed to check in. Status: ${response.status}`);
    }

    checkInModalOpen.value = false;
    await loadQueue();
  } catch (error) {
    console.error("Check-in submit error:", error);
    checkInError.value = "Couldn't complete check-in. Please try again.";
  } finally {
    submittingCheckIn.value = false;
  }
};

const expandedRow = ref(null);
const assignMenuOpen = ref(null); // `${queueID}-more` | null
const menuPosition = ref({ top: "0px", left: "0px" });

const toggleExpand = (q) => {
  expandedRow.value = expandedRow.value === q.queueID ? null : q.queueID;
};

// Popovers are teleported to <body> (see template) so the table's
// overflow-x-auto and the card's overflow-hidden don't clip them —
// position is computed from the trigger button's own screen position.
const toggleMenu = (key, event) => {
  if (assignMenuOpen.value === key) {
    assignMenuOpen.value = null;
    return;
  }
  const rect = event.currentTarget.getBoundingClientRect();
  menuPosition.value = {
    top: `${rect.bottom + window.scrollY + 6}px`,
    left: `${rect.right + window.scrollX - 256}px`, // 256px = popover width (w-64)
  };
  assignMenuOpen.value = key;
};

const activeMoreQueue = computed(() => {
  const key = assignMenuOpen.value;
  if (!key || !key.toString().endsWith("-more")) return null;
  return queue.value.find(q => q.queueID === key.toString().replace("-more", ""));
});

const closeMenus = (event) => {
  if (!assignMenuOpen.value) return;
  if (event.target.closest(".menu-trigger") || event.target.closest(".menu-popover")) return;
  assignMenuOpen.value = null;
};

// DELETE /api/Queue/{id}
const removeFromQueue = async (q) => {
  assignMenuOpen.value = null;
  const ok = await askConfirm({
    title: `Remove ${q.no} from today's queue?`,
    message: `${q.child}, brought by ${q.parent}, will be taken off today's list. This can't be undone.`,
    confirmText: "Remove",
    tone: "danger",
  });
  if (!ok) return;
  try {
    const res = await fetch(`${API_BASE}/Queue/${q.queueID}`, { method: "DELETE", headers: authJson() });
    if (!res.ok) throw new Error(`Status ${res.status}`);
  } catch (error) {
    console.error("Remove from queue error:", error);
    showAlert({ title: "Could not remove this entry", message: "Please try again.", tone: "error" });
  } finally {
    await loadQueue();
  }
};

/* ---------------- Expected Patients (children due today) ----------------
   From each child's vaccination timeline: everyone with a dose due today,
   plus whether they've already checked in (i.e. are in today's queue). */
const dueToday = ref([]);
const stockSummary = ref([]);
const activities = ref([]);

const loadExpected = async () => {
  try {
    const today = new Date();
    const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
    const res = await fetch(`${API_BASE}/VaccinationTimeline/schedule?from=${iso}&to=${iso}&includeOverdue=false`);
    if (!res.ok) throw new Error(`Status ${res.status}`);
    dueToday.value = await res.json();
  } catch (e) {
    console.error("loadExpected:", e);
  }
};

const expected = computed(() => {
  const byChild = new Map();
  for (const t of dueToday.value) {
    if (!byChild.has(t.childID)) {
      byChild.set(t.childID, { childID: t.childID, child: t.childName, parent: t.parentName || "—", vaccines: [], allDone: true });
    }
    const e = byChild.get(t.childID);
    e.vaccines.push(t.abbreviation || t.vaccineName);
    if (t.status !== "Completed") e.allDone = false;
  }
  const queued = new Set(queue.value.flatMap(q => q.childIDs || []));
  return [...byChild.values()].map(e => ({
    ...e,
    time: e.vaccines.join(", "),
    status: e.allDone ? "Vaccinated" : queued.has(e.childID) ? "Checked In" : "Not Arrived",
  }));
});
const arrivalStyle = {
  "Checked In": "bg-emerald-50 text-emerald-700",
  Vaccinated: "bg-sky-50 text-sky-700",
  "Not Arrived": "bg-stone-100 text-stone-600",
};

/* ---------------- Vaccine stock alerts ----------------
   Per vaccine: total usable doses vs. the batches' minimum stock, plus
   any batch expiring within 30 days. */
const loadStock = async () => {
  try {
    const [invRes, vacRes] = await Promise.all([fetch(`${API_BASE}/VaccineInventory`), fetch(`${API_BASE}/Vaccines`)]);
    if (!invRes.ok || !vacRes.ok) throw new Error("inventory request failed");
    const inventory = await invRes.json();
    const vaccines = await vacRes.json();
    const now = new Date();
    stockSummary.value = vaccines.map(v => {
      const usable = inventory.filter(i => i.vaccineID === v.vaccineID && i.status && new Date(i.expirationDate) >= now);
      const stock = usable.reduce((s, i) => s + i.currentQuantity, 0);
      const minimum = usable.reduce((s, i) => s + i.minimumStock, 0) || 20;
      const expiring = usable
        .filter(i => i.currentQuantity > 0)
        .map(i => Math.ceil((new Date(i.expirationDate) - now) / 86400000))
        .filter(d => d <= 30)
        .sort((a, b) => a - b)[0];
      const level = stock === 0 ? "Critical" : stock < minimum ? "Low" : expiring !== undefined ? "Watch" : null;
      return {
        vaccineID: v.vaccineID,
        name: v.abbreviation || v.vaccineName,
        detail: `${stock} doses remaining`,
        note: stock === 0 ? "Out of stock" : stock < minimum ? `Below minimum of ${minimum}` : expiring !== undefined ? `A batch expires in ${expiring} day${expiring === 1 ? "" : "s"}` : "",
        level,
      };
    }).filter(s => s.level);
  } catch (e) {
    console.error("loadStock:", e);
  }
};
const stockAlerts = computed(() => stockSummary.value);

// "Notify parents" on an out-of-stock vaccine: parents of children due for it
// this week are told not to come for that dose yet (and told again when it's
// back). The same check also runs by itself every morning.
const stockNotice = ref({});
const notifyStockParents = async (s) => {
  stockNotice.value = { ...stockNotice.value, [s.vaccineID]: "Sending…" };
  try {
    const res = await fetch(`${API_BASE}/Notifications/stock-notices?vaccineId=${s.vaccineID}`, { method: "POST", headers: authJson() });
    const data = await res.json().catch(() => ({}));
    stockNotice.value = { ...stockNotice.value, [s.vaccineID]: data.message || (res.ok ? "Done." : "Could not send the notices.") };
  } catch {
    stockNotice.value = { ...stockNotice.value, [s.vaccineID]: "Could not send the notices." };
  }
};
const stockStyle = {
  Critical: "bg-rose-50 text-rose-700",
  Low: "bg-amber-50 text-amber-700",
  Watch: "bg-sky-50 text-sky-700",
};
const stockNoteColor = {
  Critical: "text-rose-700",
  Low: "text-amber-700",
  Watch: "text-sky-700",
};

/* ---------------- Recent clinic activity (audit log) ---------------- */
const ACTIVITY_ICON = {
  "Patient Management": UserPlus,
  Vaccination: Syringe,
  Inventory: ClipboardCheck,
  Authentication: DoorOpen,
  "User Management": UserCog,
};
function timeAgo(value) {
  const mins = Math.floor((Date.now() - new Date(value).getTime()) / 60000);
  if (mins < 1) return "Just now";
  if (mins < 60) return `${mins} min ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours} hr ago`;
  return `${Math.floor(hours / 24)} d ago`;
}
const loadActivities = async () => {
  try {
    // Clinic work only; the full audit trail is the Super Admin's
    const res = await fetch(`${API_BASE}/AuditLogs/activity`);
    if (!res.ok) throw new Error(`Status ${res.status}`);
    const logs = await res.json();
    activities.value = logs
      .filter(l => l.module !== "Authentication")
      .slice(0, 10)
      .map(l => ({
        icon: ACTIVITY_ICON[l.module] || FileText,
        text: `${l.user}: ${l.action.toLowerCase()} — ${l.affectedRecord !== "—" ? l.affectedRecord : l.description}`,
        time: timeAgo(l.timestamp),
      }));
  } catch (e) {
    console.error("loadActivities:", e);
  }
};

/* Queue overview donut chart */

const donutData = computed(() => [
  {
    name: "Waiting",
    value: queue.value.filter(q => q.status === "Waiting").length,
    color: "#d97706",
    dot: "bg-amber-500",
  },
  {
    name: "Ready",
    value: queue.value.filter(q => q.status === "Ready").length,
    color: "#0284c7",
    dot: "bg-sky-500",
  },
  {
    name: "In Progress",
    value: queue.value.filter(q => q.status === "In Progress").length,
    color: "#7c3aed",
    dot: "bg-violet-500",
  },
  {
    name: "Completed",
    value: queue.value.filter(q => q.status === "Completed").length,
    color: "#059669",
    dot: "bg-emerald-600",
  },
  {
    name: "Late",
    value: queue.value.filter(q => q.status === "Late").length,
    color: "#e11d48",
    dot: "bg-rose-600",
  },
]);

const donutTotal = computed(() =>
  donutData.value.reduce((sum, item) => sum + item.value, 0)
);

const donutGradient = computed(() => {
  if (donutTotal.value === 0) {
    return "conic-gradient(#e7e5e4 0deg 360deg)";
  }

  let start = 0;

  const stops = donutData.value.map((d) => {
    const end =
      start + (d.value / donutTotal.value) * 360;

    const stop =
      `${d.color} ${start}deg ${end}deg`;

    start = end;

    return stop;
  });

  return `conic-gradient(${stops.join(", ")})`;
});


const loadQueue = async () => {
  loadingQueue.value = true;
  queueError.value = null;

  try {
    const response = await fetch(`${API_BASE}/Queue/today`);

    if (!response.ok) {
      throw new Error(`Failed to load queue. Status: ${response.status}`);
    }

    const data = await response.json();

queue.value = data.map((q) => ({
  queueID: q.queueID,
  queueNumber: q.queueNumber,

  no: `Q-${String(q.queueNumber).padStart(3, "0")}`,

  child:
    q.children?.map(c => c.name).join(", ") || "—",

  // "Rosario Santos (Grandmother)": who brought the child
  parent:
    withRelationship(q.requestBy, q.requestByRelationship),

  // Check-in time
  time: q.checkedInAt
    ? new Date(q.checkedInAt).toLocaleTimeString("en-PH", { hour: "numeric", minute: "2-digit" })
    : "—",
  children: q.children || [],

  // Backend stores "InProgress"; this page labels it "In Progress".
  status:
    q.status === "InProgress" ? "In Progress" : (q.status || "Waiting"),
  childIDs: (q.children || []).map(c => c.childID),
}));
  } catch (error) {
    console.error("Queue loading error:", error);
    queueError.value = error.message;
  } finally {
    loadingQueue.value = false;
  }
};
let queueRefreshInterval = null;

onMounted(() => {
  loadQueue();
  loadExpected();
  loadStock();
  loadActivities();
  document.addEventListener("click", closeMenus);

  queueRefreshInterval = setInterval(loadQueue, 5000);
  // Slower-moving panels
  slowRefreshInterval = setInterval(() => {
    loadExpected();
    loadStock();
    loadActivities();
  }, 30000);
});

let slowRefreshInterval = null;

onUnmounted(() => {
  if (queueRefreshInterval) {
    clearInterval(queueRefreshInterval);
  }
  if (slowRefreshInterval) {
    clearInterval(slowRefreshInterval);
  }
  document.removeEventListener("click", closeMenus);
});
</script>

<template>
  <div class="flex min-h-screen w-full bg-stone-50 text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;">
    <!-- ---------------- Sidebar ---------------- -->
    <StaffSidebar />

    <!-- ---------------- Main ---------------- -->
    <main class="flex-1 min-w-0">
      <StaffTopbar title="Staff / Nurse Dashboard" breadcrumb="Aruga / Dashboard" />

      <div class="px-4 md:px-8 py-6 space-y-6">
        <!-- Summary cards -->
        <div class="grid grid-cols-2 sm:grid-cols-3 xl:grid-cols-6 gap-4">
          <div
            v-for="c in summaryCards"
            :key="c.label"
            class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm"
          >
            <div class="flex items-center justify-between">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500">{{ c.label }}</p>
              <div class="flex h-7 w-7 items-center justify-center rounded-lg shrink-0" :class="c.tintBg">
                <component :is="c.icon" :size="14" :class="c.tint" />
              </div>
            </div>
            <p class="text-[26px] font-bold mt-2">{{ c.value }}</p>
            <div class="flex items-center gap-1 mt-1">
              <TrendingUp v-if="c.up === true" :size="12" class="text-emerald-700" />
              <TrendingDown v-if="c.up === false" :size="12" class="text-rose-700" />
              <span
                class="text-[11px] font-medium"
                :class="c.up === true ? 'text-emerald-700' : c.up === false ? 'text-rose-700' : 'text-stone-500'"
              >
                {{ c.trend }}
              </span>
            </div>
          </div>
        </div>

        <!-- Quick actions -->
        <div class="rounded-2xl border border-stone-200 bg-white p-5 shadow-sm">
          <p class="text-[14px] font-semibold mb-3">Quick Actions</p>
          <div class="flex flex-wrap gap-3">
            <button
              v-for="a in quickActions"
              :key="a.label"
              @click="a.action && a.action()"
              :disabled="a.disabled"
              :title="a.disabled ? 'Coming soon' : a.label"
              class="flex items-center gap-2 rounded-xl px-4 py-2.5 text-[13px] font-medium border border-stone-200 transition-colors"
              :class="a.disabled ? 'opacity-40 cursor-not-allowed' : 'hover:bg-stone-50'"
            >
              <component :is="a.icon" :size="16" class="text-emerald-700" />
              {{ a.label }}
            </button>
          </div>
        </div>

        <!-- Queue + side column -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Today's Queue -->
          <div ref="queueSectionRef" class="lg:col-span-8 rounded-2xl border border-stone-200 bg-white shadow-sm overflow-hidden">
            
            <div class="flex items-center justify-between px-5 py-4 border-b border-stone-200">
  <div>
    <p class="text-[14px] font-semibold">Today's Queue</p>

    <span class="text-[12px] text-stone-500">
      {{ visibleQueue.length }} {{ visibleQueue.length === 1 ? 'family' : 'families' }} in queue
    </span>
  </div>

  <button
    @click="loadQueue"
    :disabled="loadingQueue"
    class="flex items-center gap-2 rounded-lg border border-stone-200 px-3 py-2 text-[12px] font-medium text-stone-600 hover:bg-stone-50 disabled:opacity-50"
  >
    <RefreshCw
      :size="14"
      :class="{ 'animate-spin': loadingQueue }"
    />

    Refresh
  </button>
</div>
            <div class="overflow-x-auto">
              <table class="w-full text-[13px]">
                <thead>
                  <tr class="bg-stone-50">
                    <th
                      v-for="h in ['Queue No.', 'Child', 'Parent', 'Checked In', 'Status', '']"
                      :key="h"
                      class="text-left font-semibold px-5 py-3 whitespace-nowrap text-[11px] uppercase tracking-wide text-stone-500"
                    >
                      {{ h }}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <template v-for="q in visibleQueue" :key="q.queueID">
                   <tr class="border-t border-stone-200">
                    <td class="px-5 py-3 font-medium whitespace-nowrap">{{ q.no }}</td>
                    <!-- names wrap, so the action buttons stay on screen -->
                    <td class="px-5 py-3 min-w-[140px]">{{ q.child }}</td>
                    <td class="px-5 py-3 min-w-[140px] text-stone-500">{{ q.parent }}</td>
                    <td class="px-5 py-3 whitespace-nowrap text-stone-500">{{ q.time }}</td>
                    <td class="px-5 py-3 whitespace-nowrap">
                      <span
                        class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium"
                        :class="statusStyle[q.status]"
                      >
                        <span class="h-1.5 w-1.5 rounded-full" :class="statusDot[q.status]" />
                        {{ q.status }}
                      </span>
                    </td>
                    <td class="px-5 py-3 whitespace-nowrap relative">
                      <div class="flex items-center gap-1">
                        <button @click="toggleExpand(q)" class="p-1.5 rounded-lg hover:bg-stone-100" title="View Patient">
                          <Eye :size="15" class="text-stone-500" />
                        </button>
                        <button
                          v-if="q.status === 'Waiting'"
                          @click="callVisit(q)"
                          :disabled="roomBusy || !!nowServing"
                          class="p-1.5 rounded-lg hover:bg-emerald-50 disabled:opacity-30 disabled:cursor-not-allowed"
                          :title="nowServing ? 'Complete the visit in the room first' : 'Call this family into the vaccination room (out of turn)'"
                        >
                          <Megaphone :size="15" class="text-emerald-700" />
                        </button>
                        <button @click="toggleMenu(`${q.queueID}-more`, $event)" class="menu-trigger p-1.5 rounded-lg hover:bg-stone-100" title="More">
                          <MoreHorizontal :size="15" class="text-stone-500" />
                        </button>
                      </div>
                    </td>
                  </tr>
                  <tr v-if="expandedRow === q.queueID" class="bg-stone-50 border-t border-stone-100">
                    <td colspan="6" class="px-5 py-3 text-[12.5px] text-stone-600">
                      <span class="font-medium text-stone-800">Queue {{ q.no }}</span> —
                      Child: {{ q.child }} · Parent: {{ q.parent }} · Checked in {{ q.time }} · Status: {{ q.status }}
                      <span v-if="q.status === 'In Progress'" class="text-violet-700"> · In the vaccination room</span>
                      <span v-else-if="q.status === 'Waiting'" class="text-amber-700"> · Waiting to be called</span>
                      <button @click="router.push('/staff/patient-records?tab=children')" class="ml-2 text-emerald-700 font-medium hover:underline">Open Patient Records</button>
                    </td>
                  </tr>
                  </template>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Right column: donut + expected patients -->
          <div class="lg:col-span-4 flex flex-col gap-6">
            <div class="rounded-2xl border border-stone-200 bg-white p-5 shadow-sm">
              <p class="text-[14px] font-semibold mb-2">Queue Overview</p>
              <div class="h-40 relative flex items-center justify-center">
                <div class="h-32 w-32 rounded-full" :style="{ background: donutGradient }">
                  <div class="h-full w-full flex items-center justify-center">
                    <div class="h-19 w-19 rounded-full bg-white flex flex-col items-center justify-center">
                      <p class="text-[20px] font-bold">{{ donutTotal }}</p>
                      <p class="text-[10px] text-stone-500">Total today</p>
                    </div>
                  </div>
                </div>
              </div>
              <div class="grid grid-cols-2 gap-x-3 gap-y-1.5 mt-3">
                <div v-for="d in donutData" :key="d.name" class="flex items-center gap-1.5 text-[11.5px]">
                  <span class="h-2 w-2 rounded-full" :class="d.dot" />
                  <span class="text-stone-500">{{ d.name }}</span>
                  <span class="font-semibold ml-auto">{{ d.value }}</span>
                </div>
              </div>
            </div>

            <div class="rounded-2xl border border-stone-200 bg-white p-5 shadow-sm flex-1">
              <div class="flex items-center justify-between mb-3">
                <p class="text-[14px] font-semibold">Expected Patients</p>
                <span class="text-[11px] text-stone-500">{{ expected.length }} today</span>
              </div>
              <div class="space-y-1 max-h-70 overflow-y-auto pr-1">
                <div
                  v-for="p in expected"
                  :key="p.childID"
                  class="flex items-center justify-between rounded-xl px-3 py-2.5 bg-stone-50"
                >
                  <div class="min-w-0">
                    <p class="text-[12.5px] font-medium truncate">{{ p.child }}</p>
                    <p class="text-[11px] truncate text-stone-500">{{ p.time }} · {{ p.parent }}</p>
                  </div>
                  <span
                    class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium shrink-0"
                    :class="arrivalStyle[p.status]"
                  >
                    {{ p.status }}
                  </span>
                </div>
                <p v-if="expected.length === 0" class="text-[12.5px] text-stone-400 px-3 py-2">No vaccinations are due today.</p>
              </div>
            </div>
          </div>
        </div>

        <!-- Vaccination Room: one room, one vaccinator. Call Next alerts the
             parent's phone (banner + sound + SMS). -->
        <div ref="roomSectionRef" class="rounded-2xl border shadow-sm overflow-hidden"
          :class="nowServing ? 'border-violet-200 bg-violet-50/30' : 'border-emerald-200 bg-white'">
          <div class="flex items-center justify-between gap-4 px-5 py-4 border-b" :class="nowServing ? 'border-violet-100' : 'border-stone-200'">
            <div class="flex items-center gap-3">
              <div class="flex h-10 w-10 items-center justify-center rounded-xl" :class="nowServing ? 'bg-violet-100' : 'bg-emerald-50'">
                <DoorOpen :size="18" :class="nowServing ? 'text-violet-700' : 'text-emerald-700'" />
              </div>
              <div>
                <p class="text-[14px] font-semibold">Vaccination Room</p>
                <p class="text-[11.5px] text-stone-500">
                  <template v-if="nowServing">A family is inside. Record their vaccines, then Complete Visit.</template>
                  <template v-else-if="waitingList.length">{{ waitingList.length }} waiting. Call Next alerts the next parent's phone.</template>
                  <template v-else>Nobody is waiting right now.</template>
                </p>
              </div>
            </div>
            <button
              @click="callNext"
              :disabled="roomBusy || !!nowServing || waitingList.length === 0"
              class="flex items-center gap-2 rounded-xl bg-emerald-700 px-5 py-3 text-[14px] font-semibold text-white shadow-sm hover:bg-emerald-800 disabled:cursor-not-allowed disabled:opacity-40"
              :title="nowServing ? 'Complete the visit in the room first' : waitingList.length === 0 ? 'Nobody is waiting' : ''"
            >
              <Loader2 v-if="roomBusy" :size="17" class="animate-spin" />
              <Megaphone v-else :size="17" />
              Call Next<span v-if="!nowServing && waitingList.length"> · #{{ waitingList[0].queueNumber }}</span>
            </button>
          </div>

          <div v-if="roomMessage" class="px-5 py-2.5 text-[12.5px] border-b"
            :class="roomError ? 'bg-rose-50 text-rose-800 border-rose-100' : 'bg-sky-50 text-sky-800 border-sky-100'">
            {{ roomMessage }}
          </div>

          <div class="grid grid-cols-1 lg:grid-cols-12 gap-0">
            <!-- Inside the room now -->
            <div class="lg:col-span-8 p-5">
              <p class="text-[11px] font-semibold uppercase tracking-wide text-stone-500 mb-2">Inside the room</p>
              <div v-if="!nowServing" class="rounded-xl border border-dashed border-stone-200 px-4 py-8 text-center text-[12.5px] text-stone-400">
                The room is free.
              </div>
              <div v-else class="rounded-xl border border-violet-200 bg-white p-4">
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <p class="text-[22px] font-bold leading-none">{{ nowServing.no }}</p>
                    <p class="text-[12px] text-stone-500 mt-1">Brought by {{ nowServing.parent }} · checked in {{ nowServing.time }}</p>
                  </div>
                  <div class="flex items-center gap-2">
                    <button @click="callVisit(nowServing)" :disabled="roomBusy"
                      class="flex items-center gap-1.5 rounded-lg border border-stone-200 px-3 py-1.5 text-[12px] font-medium text-stone-600 hover:bg-stone-50 disabled:opacity-40"
                      title="Alert the parent's phone again (no second SMS)">
                      <Megaphone :size="13" /> Call Again
                    </button>
                    <button @click="backToWaiting(nowServing)" :disabled="roomBusy"
                      class="flex items-center gap-1.5 rounded-lg border border-stone-200 px-3 py-1.5 text-[12px] font-medium text-stone-600 hover:bg-stone-50 disabled:opacity-40"
                      title="They didn't come in: back to waiting, same number">
                      <Undo2 :size="13" /> Not Here
                    </button>
                  </div>
                </div>
                <div class="mt-3 divide-y divide-stone-100">
                  <div v-for="c in nowServing.children" :key="c.childID" class="flex items-center justify-between py-2.5">
                    <div class="flex items-center gap-2">
                      <span class="text-[13.5px] font-medium">{{ c.name }}</span>
                      <span v-if="roomProgress[c.childID]?.state === 'done'" class="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                        <Check :size="11" /> Done
                      </span>
                      <span v-else-if="roomProgress[c.childID]" class="rounded-full bg-amber-50 px-2 py-0.5 text-[11px] font-semibold text-amber-700">
                        {{ roomProgress[c.childID].due }} vaccine{{ roomProgress[c.childID].due === 1 ? '' : 's' }} due
                      </span>
                    </div>
                    <button @click="startVaccinating(nowServing, c)"
                      class="flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-[12px] font-semibold"
                      :class="roomProgress[c.childID]?.state === 'done' ? 'border border-stone-200 text-stone-600 hover:bg-stone-50' : 'bg-emerald-700 text-white hover:bg-emerald-800'">
                      <Syringe :size="13" /> {{ roomProgress[c.childID]?.state === 'done' ? 'Open' : 'Start Vaccinating' }}
                    </button>
                  </div>
                </div>
                <div class="mt-3 flex justify-end">
                  <button @click="completeVisit(nowServing)" :disabled="roomBusy"
                    class="flex items-center gap-1.5 rounded-lg bg-stone-800 px-4 py-2 text-[12.5px] font-semibold text-white hover:bg-stone-900 disabled:opacity-40">
                    <Check :size="14" /> Complete Visit
                  </button>
                </div>
              </div>
            </div>

            <!-- Next in line -->
            <div class="lg:col-span-4 border-t lg:border-t-0 lg:border-l border-stone-200 p-5">
              <p class="text-[11px] font-semibold uppercase tracking-wide text-stone-500 mb-2">Next in line</p>
              <p v-if="waitingList.length === 0" class="text-[12.5px] text-stone-400">Nobody is waiting.</p>
              <div class="space-y-1.5 max-h-56 overflow-y-auto pr-1">
                <div v-for="(q, i) in waitingList" :key="q.queueID" class="flex items-center justify-between rounded-xl bg-stone-50 px-3 py-2">
                  <div class="min-w-0">
                    <p class="text-[12.5px] font-semibold">{{ q.no }} <span v-if="i === 0" class="ml-1 text-[10.5px] font-medium text-emerald-700">next</span></p>
                    <p class="text-[11px] text-stone-500 truncate">{{ q.child }}</p>
                  </div>
                  <span class="text-[11px] text-stone-400 shrink-0">{{ q.time }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Stock alerts + activities -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <div class="lg:col-span-4 rounded-2xl border border-stone-200 bg-white p-5 shadow-sm">
            <p class="text-[14px] font-semibold mb-3">Vaccine Stock Alerts</p>
            <div class="space-y-2.5">
              <p v-if="stockAlerts.length === 0" class="text-[12.5px] text-emerald-700 px-3 py-2">All vaccines are sufficiently stocked.</p>
              <div
                v-for="s in stockAlerts"
                :key="s.name"
                class="flex items-center justify-between rounded-xl px-3 py-2.5 bg-stone-50"
              >
                <div class="min-w-0">
                  <p class="text-[12.5px] font-semibold">{{ s.name }}</p>
                  <p class="text-[11px] text-stone-500">{{ s.detail }}</p>
                  <p class="text-[11px]" :class="stockNoteColor[s.level]">{{ s.note }}</p>
                  <button
                    v-if="s.level === 'Critical'"
                    @click="notifyStockParents(s)"
                    class="mt-1 text-[11px] font-semibold text-emerald-700 hover:underline"
                  >
                    Notify parents with a child due for it
                  </button>
                  <p v-if="stockNotice[s.vaccineID]" class="text-[10.5px] text-stone-500 mt-0.5">{{ stockNotice[s.vaccineID] }}</p>
                </div>
                <span
                  class="text-[10.5px] font-semibold px-2 py-1 rounded-full shrink-0"
                  :class="stockStyle[s.level]"
                >
                  {{ s.level }}
                </span>
              </div>
            </div>
          </div>

          <div class="lg:col-span-8 rounded-2xl border border-stone-200 bg-white p-5 shadow-sm">
            <p class="text-[14px] font-semibold mb-3">Recent Clinic Activities</p>
            <div class="space-y-3 max-h-[260px] overflow-y-auto pr-1">
              <p v-if="activities.length === 0" class="text-[12.5px] text-stone-400">No recent activity yet.</p>
              <div v-for="(a, i) in activities" :key="i" class="flex items-start gap-3">
                <div class="flex h-8 w-8 items-center justify-center rounded-full shrink-0 bg-emerald-50">
                  <component :is="a.icon" :size="14" class="text-emerald-800" />
                </div>
                <div class="flex-1 min-w-0 flex items-start justify-between gap-3">
                  <p class="text-[12.5px]">{{ a.text }}</p>
                  <span class="text-[11px] whitespace-nowrap text-stone-500">{{ a.time }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
        <Teleport to="body">
    <div
      v-if="activeMoreQueue"
      class="menu-popover fixed z-50 w-48 rounded-xl border border-stone-200 bg-white shadow-lg p-2"
      :style="menuPosition"
    >
      <button @click="removeFromQueue(activeMoreQueue)" class="flex w-full items-center rounded-lg px-2 py-1.5 text-[12.5px] text-rose-700 hover:bg-rose-50">
        Remove from Queue
      </button>
    </div>

    <!-- Check-In Patient modal -->
    <div v-if="checkInModalOpen" class="fixed inset-0 z-50 flex items-center justify-center bg-black/30 px-4">
      <div class="w-full max-w-md rounded-2xl bg-white shadow-xl">
        <div class="flex items-center justify-between px-5 py-4 border-b border-stone-200">
          <p class="text-[15px] font-semibold">Check-In Patient</p>
          <button @click="closeCheckInModal" class="p-1.5 rounded-lg hover:bg-stone-100">
            <X :size="16" class="text-stone-500" />
          </button>
        </div>

        <div class="px-5 py-4 space-y-4 max-h-[70vh] overflow-y-auto">
          <p v-if="loadingCheckInData" class="text-[12.5px] text-stone-400">Loading parent/child records…</p>

          <template v-if="!loadingCheckInData">
            <!-- Step 1: find the parent -->
            <div v-if="!selectedParent">
              <label class="block text-[12px] font-medium text-stone-600 mb-1">Search parent by name</label>
              <input
                v-model="parentSearch"
                type="text"
                placeholder="e.g. Mark Reyes"
                class="w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] focus:outline-none focus:ring-2 focus:ring-emerald-600/30 focus:border-emerald-600"
              />
              <div v-if="parentSearch.trim()" class="mt-2 rounded-xl border border-stone-200 divide-y divide-stone-100 overflow-hidden">
                <button
                  v-for="p in filteredParents"
                  :key="getParentId(p)"
                  @click="pickParent(p)"
                  class="flex w-full items-center justify-between px-3 py-2.5 text-left text-[13px] hover:bg-stone-50"
                >
                  {{ getParentName(p) }}
                </button>
                <p v-if="filteredParents.length === 0" class="px-3 py-2.5 text-[12.5px] text-stone-400">
                  No matching parent found.
                </p>
              </div>
            </div>

            <!-- Step 2: pick which of the parent's children to check in -->
            <div v-else>
              <div class="flex items-center justify-between rounded-xl bg-stone-50 px-3 py-2.5 mb-3">
                <p class="text-[13px] font-medium">{{ getParentName(selectedParent) }}</p>
                <button @click="selectedParent = null" class="text-[11.5px] text-emerald-700 font-medium hover:underline">
                  Change
                </button>
              </div>

              <p class="text-[12px] font-medium text-stone-600 mb-2">Select child(ren) to check in</p>
              <div class="space-y-1.5">
                <label
                  v-for="c in childrenForSelectedParent"
                  :key="getChildId(c)"
                  class="flex items-center gap-2.5 rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] cursor-pointer hover:bg-stone-50"
                >
                  <input
                    type="checkbox"
                    :checked="selectedChildIds.includes(getChildId(c))"
                    @change="toggleChildSelection(getChildId(c))"
                    class="rounded border-stone-300 text-emerald-700 focus:ring-emerald-600/30"
                  />
                  {{ getChildName(c) }}
                </label>
                <p v-if="childrenForSelectedParent.length === 0" class="text-[12.5px] text-stone-400">
                  No children on file for this parent.
                </p>
              </div>
            </div>

            <p v-if="checkInError" class="text-[12.5px] text-rose-700">{{ checkInError }}</p>
          </template>
        </div>

        <div class="flex items-center justify-end gap-2 px-5 py-4 border-t border-stone-200">
          <button @click="closeCheckInModal" class="rounded-xl px-4 py-2.5 text-[13px] font-medium text-stone-600 hover:bg-stone-50">
            Cancel
          </button>
          <button
            @click="submitCheckIn"
            :disabled="!selectedParent || selectedChildIds.length === 0 || submittingCheckIn"
            class="rounded-xl bg-emerald-700 px-4 py-2.5 text-[13px] font-medium text-white hover:bg-emerald-800 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {{ submittingCheckIn ? "Checking in…" : "Check In" }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
    </main>
  </div>
</template>