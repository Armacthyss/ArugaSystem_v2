<script setup>
import { API_ORIGIN } from '@/utils/apiBase'
import { ref, reactive, computed, watch, onMounted, onUnmounted, h } from "vue";
import { useRoute } from "vue-router";
import {
  Users, Syringe, Search,
  UserPlus, Eye, Pencil, Link2, KeyRound, Ban,
  MoreHorizontal, X, MapPin, Phone, Mail, Baby, Check,
  ArrowRightLeft, UserCircle2, Star, ShieldCheck, Trash2, AlertTriangle,
} from "lucide-vue-next";
import axios from "axios";

import StaffSidebar from "./StaffSidebar.vue";
import StaffTopbar from "./StaffTopbar.vue";
import PrivacyConsentCheckbox from "@/components/Shared/PrivacyConsentCheckbox.vue";
import { barangayChoices, isServedBarangay, BARANGAY_HINT } from "@/utils/barangays";
import { toISODate } from "@/utils/format";
import { showAlert } from "@/utils/dialog";
import { phMobileError } from "@/utils/phone";

const route = useRoute();
// Birth dates and Yellow Book doses can't be in the future
const todayISO = toISODate();

onMounted(() => {
    loadAll();
    loadVaccineCatalog();
    // Deep-links from StaffDashboard's Quick Actions land here now,
    // instead of the old separate Parent/Children Record pages.
    if (route.query.tab === "children") activeTab.value = "children";
    if (route.query.openRegister === "1") {
      openRegisterModal(activeTab.value === "children" ? "child" : "parent");
    }
});

/* =========================================================================
   API LAYER — talks directly to ParentsController / ChildrenController.
   (No api.js service file was provided, so this wires axios straight to the
   endpoints that exist in the two controllers you shared. If you already
   have a shared axios instance with baseURL / auth headers configured, 
   swap this `axios` import for that instance.)
========================================================================= */
const API_BASE = `${API_ORIGIN}/api`;
const api = {
  getAllParents:                ()            => axios.get(`${API_BASE}/Parents/all`).then(r => r.data),
  createParent:                 (payload)     => axios.post(`${API_BASE}/Parents`, payload).then(r => r.data),
  updateParent:                 (id, payload) => axios.put(`${API_BASE}/Parents/${id}`, payload).then(r => r.data),
  getAllChildren:               ()            => axios.get(`${API_BASE}/Children/all`).then(r => r.data),
  getChildrenOverview:          ()            => axios.get(`${API_BASE}/Children/overview`).then(r => r.data),
  getChildrenByParent:          (parentId)    => axios.get(`${API_BASE}/Children/parent/${parentId}`).then(r => r.data),
  createChild:                  (payload)     => axios.post(`${API_BASE}/Children`, payload).then(r => r.data),
  updateChild:                  (id, payload) => axios.put(`${API_BASE}/Children/${id}`, payload).then(r => r.data),
  getChildVaccinations:         (childId)     => axios.get(`${API_BASE}/VaccinationRecords/child/${childId}`).then(r => r.data),
  saveHistoricalVaccinations:   (payload)     => axios.post(`${API_BASE}/VaccinationRecords/historical`, payload).then(r => r.data),
  getVaccinesWithDoses:         ()            => axios.get(`${API_BASE}/Vaccines/with-doses`).then(r => r.data),

  // Real Child <-> Parent/Guardian relationship API
  getRelationshipsByChild:      (childId)     => axios.get(`${API_BASE}/ChildParentRelationships/child/${childId}`).then(r => r.data),
  getRelationshipsByParent:     (parentId)    => axios.get(`${API_BASE}/ChildParentRelationships/parent/${parentId}`).then(r => r.data),
  createRelationship:           (payload)     => axios.post(`${API_BASE}/ChildParentRelationships`, payload).then(r => r.data),
  deleteRelationship:           (relationshipId) => axios.delete(`${API_BASE}/ChildParentRelationships/${relationshipId}`).then(r => r.data),
  makePrimaryContact:           (relationshipId) => axios.patch(`${API_BASE}/ChildParentRelationships/${relationshipId}/primary`).then(r => r.data),
  setReminderRecipients:        (childId, mode)  => axios.patch(`${API_BASE}/ChildParentRelationships/child/${childId}/notifications`, { mode }).then(r => r.data),

  // A parent's portal login (AccountController; staff may change parent logins only)
  setAccountStatus:             (accountId, status) => axios.patch(`${API_BASE}/accounts/${accountId}/status`, { status }).then(r => r.data),
  resetAccountPassword:         (accountId)   => axios.post(`${API_BASE}/accounts/${accountId}/reset-password`).then(r => r.data),
};

/* ---------- tiny render-fn components (badge / avatar markup, used everywhere) ---------- */
const statusStyle = { Active:"bg-emerald-50 text-emerald-700", Inactive:"bg-stone-100 text-stone-600", Pending:"bg-amber-50 text-amber-700", Archived:"bg-rose-50 text-rose-700", "No Login":"bg-sky-50 text-sky-700" };
const statusDot   = { Active:"bg-emerald-600", Inactive:"bg-stone-400", Pending:"bg-amber-500", Archived:"bg-rose-600", "No Login":"bg-sky-500" };
const vaccStyle   = { "Fully Vaccinated":"bg-emerald-50 text-emerald-700", "In Progress":"bg-amber-50 text-amber-700", Delayed:"bg-rose-50 text-rose-700", Upcoming:"bg-sky-50 text-sky-700" };
const avatarSize  = { sm:"h-9 w-9 text-[11px]", md:"h-10 w-10 text-[11px]", lg:"h-14 w-14 text-[15px]" };

const StatusBadge = (p) => h("span", { class:`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ${statusStyle[p.status]}` }, [h("span",{class:`h-1.5 w-1.5 rounded-full ${statusDot[p.status]}`}), p.status]);
const VaccBadge   = (p) => h("span", { class:`inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ${vaccStyle[p.status]}` }, p.status);
const Avatar      = (p) => h("div", { class:`flex items-center justify-center rounded-full text-white font-semibold shrink-0 bg-sky-700 ${avatarSize[p.size||"sm"]}` }, p.text);

const isLoading = ref(false);
const error = ref(null);
const activeTab = ref("parents"); // "parents" | "children"



const relationshipOptions = ["Mother","Father","Guardian","Grandparent","Foster Parent","Other Authorized Guardian"];
const linkRelationshipOptions = ["Mother","Father","Guardian","Grandmother","Grandfather","Aunt","Uncle","Sibling","Foster Parent","Relative","Other"];

// Real vaccine catalog, pulled from GET /api/Vaccines/with-doses (same
// endpoint the Doctor side uses) instead of a hardcoded list — this is what
// lets the Dose Number field below be a real dropdown per vaccine, rather
// than a free-entry number. Loaded once in onMounted via loadVaccineCatalog().
const vaccineCatalog = ref([]); // [{ vaccineID, vaccineName, doses:[{doseNumber, minIntervalDays}] }]
async function loadVaccineCatalog() {
  try {
    const data = await api.getVaccinesWithDoses();
    vaccineCatalog.value = Array.isArray(data) ? data : [];
  } catch (e) {
    console.error("Error loading vaccine catalog:", e);
    vaccineCatalog.value = [];
  }
}
const vaccineNameById = (id) => vaccineCatalog.value.find(v => v.vaccineID === Number(id))?.vaccineName || `Vaccine #${id}`;

// Dose numbers actually configured for a given vaccine (e.g. Pentavalent has
// 3 doses, BCG has 1) — falls back to a generic 1–5 range if the vaccine's
// dose schedule isn't in the catalog yet, so the form still works rather
// than blocking staff on a backend data gap.
function doseOptionsForVaccine(vaccineID) {
  if (!vaccineID) return [];
  const vaccine = vaccineCatalog.value.find(v => v.vaccineID === Number(vaccineID));
  if (vaccine && Array.isArray(vaccine.doses) && vaccine.doses.length > 0) {
    return [...vaccine.doses].map(d => d.doseNumber).sort((a, b) => a - b);
  }
  return [1, 2, 3, 4, 5];
}

/* ============================= Core data (loaded from API) ============================= */
const parents = ref([]);
const children = ref([]);

function calculateAge(birth) {
  const today = new Date();
  let age = today.getFullYear() - birth.getFullYear();
  const m = (today.getMonth() - birth.getMonth() + 12) % 12;
  if (today.getMonth() - birth.getMonth() < 0 || (today.getMonth()===birth.getMonth() && today.getDate()<birth.getDate())) age--;
  return `${age}y ${m}m`;
}
function toDateInputValue(iso) {
  if (!iso) return "";
  // "2026-07-22T00:00:00" -> "2026-07-22". Don't go through toISOString():
  // that converts to UTC and shifts Philippine dates back one day.
  if (/^\d{4}-\d{2}-\d{2}/.test(String(iso))) return String(iso).slice(0, 10);
  const d = new Date(iso);
  return isNaN(d) ? "" : `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

const mapParent = (p) => ({
  id:p.parentID, initials:`${p.firstName?.[0] || ""}${p.lastName?.[0] || ""}`.toUpperCase(), name:`${p.firstName} ${p.lastName}`,
  // A contact-only guardian (no portal login) has no username, even with an email
  relationship:"Parent", contact:p.contactNo, email:p.email || "", username:p.username || "—",
  // Portal login: Active / Inactive (deactivated), or "No Login" for a contact-only guardian
  accountId:p.accountID || null, status:p.accountStatus || "Active", address:p.address, barangay:p.barangayNo,
  raw:p, // original API record, kept so edits that don't touch every field don't lose data
});

// "Next Vaccine" and the status badge come from GET /api/Children/overview,
// i.e. the child's real vaccination timeline (the same schedule parents and
// health workers see, recalculated after every dose), instead of a copy of
// the DOH schedule computed here in the browser.
const OVERVIEW_STATUS = { "Partially Vaccinated": "In Progress" };

const mapChild = (c, overview = null) => {
  const birth = new Date(c.birthDate);
  const vaccStatus = OVERVIEW_STATUS[overview?.vaccinationStatus] || overview?.vaccinationStatus || "Upcoming";
  const nextVaccine = overview?.nextVaccine
    ? `${overview.nextVaccine} · ${new Date(overview.nextDueDate).toLocaleDateString("en-US",{year:"numeric",month:"short",day:"numeric"})}`
    : "—";
  return {
    id:c.childID, name:`${c.firstName} ${c.middleName||""} ${c.lastName}`.trim(),
    birthDate:birth.toLocaleDateString("en-US",{year:"numeric",month:"short",day:"numeric"}),
    birthDateRaw:c.birthDate, // ISO value straight from the API, used for editing/saving
    birthPlace:c.placeOfBirth||"—", age:calculateAge(birth), sex:c.sex||"—",
    height:c.birthHeight?`${c.birthHeight} cm`:"—", weight:c.birthWeight?`${c.birthWeight} kg`:"—",
    allergies:c.allergies||"",
    existingConditions:c.existingConditions||"",
    familyNo:c.familyNo||"",
    vaccStatus, nextVaccine, status:"Active", address:c.address||"—", barangay:c.barangay||"—",
    raw:c, // original API record, kept so edits that don't touch every field don't lose data
  };
};


async function loadAll() {
    isLoading.value = true;

    try {
        // One request for every child's schedule summary (not one per child)
        const [pd, cd, ov] = await Promise.all([
            api.getAllParents(),
            api.getAllChildren(),
            api.getChildrenOverview().catch(() => []),
        ]);

        parents.value = Array.isArray(pd)
            ? pd.map(mapParent)
            : [];

        const childList = Array.isArray(cd) ? cd : [];
        const overviewById = new Map((Array.isArray(ov) ? ov : []).map(o => [String(o.childID).toLowerCase(), o]));

        children.value = childList.map(c => mapChild(c, overviewById.get(String(c.childID).toLowerCase())));

        // Load actual parent-child links after children are available.
        await loadRelationships();
    }
    catch (e) {
        console.error(e);
    }
    finally {
        isLoading.value = false;
    }
}





// Relationships are loaded from the real ChildParentRelationships API.
// The old demo REL-001...REL-005 records have intentionally been removed.
const relationships = ref([]);

function mapRelationship(r) {
  return {
    id: r.relationshipID,
    parentId: r.parentID,
    childId: r.childID,
    relationship: r.relationshipType,
    isPrimary: !!r.isPrimaryContact,
    canReceiveNotifications: r.canReceiveNotifications !== false,
    status: r.status || "Active",
    createdDate: r.createdAt
      ? new Date(r.createdAt).toLocaleDateString("en-US", {
          year: "numeric",
          month: "short",
          day: "numeric"
        })
      : "—",
    raw: r,
  };
}

async function loadRelationships() {
  if (!children.value.length) {
    relationships.value = [];
    return;
  }

  try {
    const results = await Promise.all(
      children.value.map(child => api.getRelationshipsByChild(child.id))
    );

    relationships.value = results
      .flatMap(result => Array.isArray(result) ? result : [])
      .map(mapRelationship)
      .filter(r => r.status === "Active");

    console.log("Relationships loaded from API:", relationships.value);
  } catch (e) {
    console.error("Error loading parent-child relationships:", e);
    relationships.value = [];
  }
}

/* -- relationship helpers: everything about "who is linked to whom" flows through these -- */
const relationshipsOfChild = (child) =>
  relationships.value.filter(r => r.childId === child.id && r.status === "Active");

const relationshipsOfParent = (parent) =>
  relationships.value.filter(r => r.parentId === parent.id && r.status === "Active");

const linkedAccountsOf = (child) => relationshipsOfChild(child)
  .map(rel => ({
    rel,
    parent: parents.value.find(p => p.id === rel.parentId)
  }))
  .filter(x => x.parent)
  .sort((a, b) => (b.rel.isPrimary ? 1 : 0) - (a.rel.isPrimary ? 1 : 0));

const childrenOf = (parent) => relationshipsOfParent(parent)
  .map(rel => ({
    rel,
    child: children.value.find(c => c.id === rel.childId)
  }))
  .filter(x => x.child);

async function setPrimary(childId, relId) {
  try {
    await api.makePrimaryContact(relId);
    await loadRelationships();
  } catch (e) {
    showAlert({ title: "Couldn't change the primary contact", message: e.response?.data?.message || e.message, tone: "error" });
  }
}

// Reminders go to every linked account unless some links are switched off,
// in which case only the primary contact gets them (CanReceiveNotifications).
const notifyModeOf = (child) =>
  relationshipsOfChild(child).every(r => r.canReceiveNotifications) ? "all" : "primary";

async function setNotifyMode(child, mode) {
  if (notifyModeOf(child) === mode) return;
  try {
    await api.setReminderRecipients(child.id, mode);
    await loadRelationships();
  } catch (e) {
    showAlert({ title: "Couldn't change who gets reminders", message: e.response?.data?.message || e.message, tone: "error" });
  }
}

/* ============================= Summary cards (per tab) ============================= */
const count = (arr, pred) => arr.filter(pred).length;
const parentSummary = computed(() => [
  { label:"Total Parents", value:parents.value.length, tint:"text-emerald-700", tintBg:"bg-emerald-50", icon:Users },
  { label:"Active Parents", value:count(parents.value,p=>p.status==="Active"), tint:"text-emerald-700", tintBg:"bg-emerald-50", icon:Check },
  { label:"Inactive Parents", value:count(parents.value,p=>p.status==="Inactive"), tint:"text-stone-500", tintBg:"bg-stone-100", icon:Ban },
  { label:"Pending Accounts", value:count(parents.value,p=>p.status==="Pending"), tint:"text-amber-700", tintBg:"bg-amber-50", icon:UserPlus },
]);
const childSummary = computed(() => [
  { label:"Total Children", value:children.value.length, tint:"text-emerald-700", tintBg:"bg-emerald-50", icon:Baby },
  { label:"Fully Vaccinated", value:count(children.value,c=>c.vaccStatus==="Fully Vaccinated"), tint:"text-emerald-700", tintBg:"bg-emerald-50", icon:Syringe },
  { label:"In Progress", value:count(children.value,c=>c.vaccStatus==="In Progress"), tint:"text-amber-700", tintBg:"bg-amber-50", icon:Syringe },
  { label:"Delayed", value:count(children.value,c=>c.vaccStatus==="Delayed"), tint:"text-rose-700", tintBg:"bg-rose-50", icon:AlertTriangle },
]);

/* ============================= Search ============================= */
const searchQuery = ref("");
const searchTerm = () => searchQuery.value.trim().toLowerCase();
const filteredParents = computed(() => {
  const q = searchTerm();
  return q ? parents.value.filter(p=>p.name.toLowerCase().includes(q)||p.id.toLowerCase().includes(q)||(p.contact||"").includes(q)||(p.email||"").toLowerCase().includes(q)||String(p.barangay||"").toLowerCase().includes(q)) : parents.value;
});
const filteredChildren = computed(() => {
  const q = searchTerm();
  return q ? children.value.filter(c=>c.name.toLowerCase().includes(q)||c.familyNo.toLowerCase().includes(q)||String(c.barangay).toLowerCase().includes(q)||c.id.toLowerCase().includes(q)) : children.value;
});

/* ============================= Pagination ============================= */
// Shared by both tables since only one is visible at a time (activeTab).
const pageSize   = ref(10);
const pageSizeOptions = [10, 20, 30, 50];
const currentPage = ref(1);

const activeFilteredCount = computed(() => activeTab.value === "parents" ? filteredParents.value.length : filteredChildren.value.length);
const totalPages = computed(() => Math.max(1, Math.ceil(activeFilteredCount.value / pageSize.value)));

// Reset back to page 1 whenever the underlying list changes shape —
// otherwise switching tabs, searching, or changing page size can strand
// the user on a page number that no longer has any rows.
watch([searchQuery, activeTab, pageSize], () => { currentPage.value = 1; });

const paginatedParents = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredParents.value.slice(start, start + pageSize.value);
});
const paginatedChildren = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value;
  return filteredChildren.value.slice(start, start + pageSize.value);
});

const paginationRangeLabel = computed(() => {
  if (activeFilteredCount.value === 0) return "0 of 0";
  const start = (currentPage.value - 1) * pageSize.value + 1;
  const end = Math.min(currentPage.value * pageSize.value, activeFilteredCount.value);
  return `${start}–${end} of ${activeFilteredCount.value}`;
});

// Compact page list with ellipses, e.g. [1, 2, '...', 8, 9] — always keeps
// first, last, and a small window around the current page.
function getPageNumbers(total, current) {
  const delta = 1;
  const range = [];
  for (let i = 1; i <= total; i++) {
    if (i === 1 || i === total || (i >= current - delta && i <= current + delta)) range.push(i);
  }
  const withDots = [];
  let last = null;
  for (const i of range) {
    if (last !== null) {
      if (i - last === 2) withDots.push(last + 1);
      else if (i - last > 2) withDots.push("...");
    }
    withDots.push(i);
    last = i;
  }
  return withDots;
}
const pageNumbers = computed(() => getPageNumbers(totalPages.value, currentPage.value));

function goToPage(n) {
  if (n === "...") return;
  if (n < 1 || n > totalPages.value) return;
  currentPage.value = n;
}

/* =========================================================================
   TABLE COLUMN CONFIGS — this is the big compression win. Instead of two
   full hand-written <table> markups (one per tab), each table is just a
   v-for over one of these arrays. "badge" picks a special renderer,
   "muted" applies the gray text style, "value" computes derived cells.
   Add/remove a column here and both header + body update automatically.
========================================================================= */
const parentColumns = [
  { key:"avatar", badge:"avatar" },
  { key:"barangay", label:"Barangay No.", muted:true, value:(p)=>p.barangay || "—" },
  { key:"name", label:"Full Name" },
  { key:"relationship", label:"Relationship", muted:true },
  { key:"contact", label:"Contact Number", muted:true },
  { key:"username", label:"Username", muted:true },
  { key:"status", label:"Status", badge:"status" },
  { key:"childrenCount", label:"Children Linked", muted:true, value:(p)=>relationshipsOfParent(p).length },
];
const childColumns = [
  { key:"avatar", badge:"childIcon" },
  // The health center identifies families by Family No. and Barangay, not by a system ID
  { key:"familyNo", label:"Family No.", value:(c)=>c.familyNo || "—" },
  { key:"barangay", label:"Barangay", muted:true },
  { key:"name", label:"Child Name" },
  { key:"age", label:"Age", muted:true },
  { key:"sex", label:"Sex", muted:true },
  { key:"vaccStatus", label:"Vaccination Status", badge:"vacc" },
  { key:"linked", label:"Linked Accounts", badge:"linked" },
  { key:"nextVaccine", label:"Next Vaccine", muted:true },
  { key:"status", label:"Status", badge:"status" },
];

/* -- row "..." action menus, same idea: config array instead of two hand-written dropdowns -- */
// Login actions only apply to a parent who has a portal login.
const parentMenuItems = (p) => [
  { icon:Baby, label:"Register Child", action:(p)=>openRegisterModal("child", { parent:p }) },
  { icon:Link2, label:"Link Existing Child", action:(p)=>openPicker("linkChild",{parent:p}) },
  ...(p.accountId ? [
    { divider:true },
    { icon:KeyRound, label:"Reset Password", action:(p)=>askResetPassword(p) },
    p.status === "Inactive"
      ? { icon:ShieldCheck, label:"Reactivate Login", class:"text-emerald-700 hover:bg-emerald-50", action:(p)=>askSetLoginActive(p, true) }
      : { icon:Ban, label:"Deactivate Login", class:"text-amber-700 hover:bg-amber-50", action:(p)=>askSetLoginActive(p, false) },
  ] : []),
];
const childMenuItems = [
  { icon:Link2, label:"Link Parent / Guardian", action:(c)=>openPicker("linkParent",{child:c}) },
  { icon:UserCircle2, label:"Manage Linked Accounts", action:(c)=>viewChild(c) },
  { icon:Syringe, label:"Add Historical Vaccination Records", action:(c)=>{ selectedChild.value=c; loadChildVaccinations(c.id); openHistoricalModal(); } },
];

// The tables scroll sideways, which cut the menu off, so the menu is drawn on
// top of the page at the button's position (opening upward near the bottom).
const actionsMenuOpenFor = ref(null);
const actionsMenuStyle = ref({});
const toggleActionsMenu = (id, event) => {
  if (actionsMenuOpenFor.value === id) { actionsMenuOpenFor.value = null; return; }
  const r = event.currentTarget.getBoundingClientRect();
  const left = `${Math.max(8, r.right - 224)}px`;   // menu is w-56 (224px), right-aligned to the button
  actionsMenuStyle.value = window.innerHeight - r.bottom < 240
    ? { left, bottom: `${window.innerHeight - r.top + 6}px` }
    : { left, top: `${r.bottom + 6}px` };
  actionsMenuOpenFor.value = id;
};
const closeActionsMenu = () => { actionsMenuOpenFor.value = null; };
onMounted(() => { window.addEventListener("scroll", closeActionsMenu, true); window.addEventListener("resize", closeActionsMenu); });
onUnmounted(() => { window.removeEventListener("scroll", closeActionsMenu, true); window.removeEventListener("resize", closeActionsMenu); });

/* ---- Parent login: deactivate / reactivate / reset password (confirm window) ---- */
const accountDialog = ref(null); // { title, message, confirmLabel, danger, run, busy, error, result }

function askSetLoginActive(p, active) {
  accountDialog.value = {
    title: active ? `Reactivate ${p.name}'s login?` : `Deactivate ${p.name}'s login?`,
    message: active
      ? "They'll be able to sign in to the parent portal again."
      : "They won't be able to sign in to the parent portal until the login is reactivated. Their children's records, schedule and reminders stay as they are.",
    confirmLabel: active ? "Reactivate" : "Deactivate",
    danger: !active,
    run: async () => {
      await api.setAccountStatus(p.accountId, active);
      await loadAll();
      return { text: `${p.name}'s login is now ${active ? "active" : "deactivated"}.` };
    },
  };
}

// "It was also emailed to x and sent by text." (empty when neither went out)
function sentNote(emailed, texted, email) {
  if (emailed && texted) return `It was also emailed to ${email} and sent by text.`;
  if (emailed) return `It was also emailed to ${email}.`;
  if (texted) return "It was also sent to them by text.";
  return "";
}

function askResetPassword(p) {
  accountDialog.value = {
    title: `Reset ${p.name}'s password?`,
    message: "A temporary password is made and shown here. They must choose a new password the next time they sign in.",
    confirmLabel: "Reset Password",
    danger: false,
    run: async () => {
      const r = await api.resetAccountPassword(p.accountId);
      const realEmail = p.email && !/@example\.com$|@demo\./i.test(p.email);
      return {
        text: sentNote(realEmail && r.emailed, r.texted, p.email) || "Give it to the parent; it wasn't emailed or texted.",
        password: r.temporaryPassword,
      };
    },
  };
}

async function runAccountDialog() {
  const d = accountDialog.value;
  d.busy = true; d.error = null;
  try { d.result = await d.run(); }
  catch (e) { d.error = e.response?.status === 403 ? "You're not allowed to change this account." : (e.response?.data?.message || e.message); }
  finally { d.busy = false; }
}

/* ============================= View drawers ============================= */
const showParentDrawer = ref(false);
const selectedParent = ref(null);
// The parent and child panels open in the same place: one replaces the other
function viewParent(p) { showChildDrawer.value=false; selectedParent.value=p; showParentDrawer.value=true; actionsMenuOpenFor.value=null; }

const showChildDrawer = ref(false);
const selectedChild = ref(null);
const removeConfirmId = ref(null); // relationship id currently showing the "remove?" confirm buttons

/* ============================= Vaccination history (per child) ============================= */
const vaccinationRecordsByChild = ref({}); // { [childId]: VaccinationRecordDTO[] }
const vaccinationLoading = ref(false);
const selectedChildVaccinations = computed(() => vaccinationRecordsByChild.value[selectedChild.value?.id] || []);

function formatVaccDate(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  return isNaN(d) ? String(iso).slice(0,10) : d.toLocaleDateString("en-US",{year:"numeric",month:"short",day:"numeric"});
}

async function loadChildVaccinations(childId) {
  if (!childId) return;
  vaccinationLoading.value = true;
  try {
    const data = await api.getChildVaccinations(childId);
    vaccinationRecordsByChild.value = { ...vaccinationRecordsByChild.value, [childId]: Array.isArray(data) ? data : [] };
  } catch (e) {
    console.error("Error loading vaccination records:", e);
  } finally {
    vaccinationLoading.value = false;
  }
}

/* -- Historical Vaccination Records modal -- */
function emptyHistoricalRow() { return { vaccineID:"", doseNumber:"", vaccinationDate:"" }; }
const historicalModal = ref({ open:false, rows:[emptyHistoricalRow()], error:null, submitting:false, success:false });

function openHistoricalModal() {
  historicalModal.value = { open:true, rows:[emptyHistoricalRow()], error:null, submitting:false, success:false };
  actionsMenuOpenFor.value = null;
}
const closeHistoricalModal = () => (historicalModal.value.open = false);
const addHistoricalRow = () => historicalModal.value.rows.push(emptyHistoricalRow());
function removeHistoricalRow(idx) {
  historicalModal.value.rows.splice(idx, 1);
  if (historicalModal.value.rows.length === 0) historicalModal.value.rows.push(emptyHistoricalRow());
}

async function submitHistoricalVaccinations() {
  const m = historicalModal.value;
  m.error = null;

  if (!selectedChild.value?.id) { m.error = "Select a child first."; return; }
  if (!m.rows.length) { m.error = "Add at least one vaccination record."; return; }

  for (const row of m.rows) {
    if (!row.vaccineID) { m.error = "Every row needs a vaccine selected."; return; }
    if (!row.doseNumber || Number(row.doseNumber) <= 0) { m.error = "Dose number must be greater than 0."; return; }
    if (!row.vaccinationDate) { m.error = "Every row needs a vaccination date."; return; }
    if (row.vaccinationDate > todayISO) { m.error = "A vaccination date can't be in the future."; return; }
  }
  // prevent obvious accidental duplicates: same vaccine + dose + date entered twice in this submission
  const seen = new Set();
  for (const row of m.rows) {
    const key = `${row.vaccineID}-${row.doseNumber}-${row.vaccinationDate}`;
    if (seen.has(key)) { m.error = "Duplicate vaccine/dose/date rows found — please review your entries."; return; }
    seen.add(key);
  }

  m.submitting = true;
  try {
    await api.saveHistoricalVaccinations({
      childID: selectedChild.value.id,
      vaccinations: m.rows.map(row => ({
        vaccineID: Number(row.vaccineID),
        doseNumber: Number(row.doseNumber),
        vaccinationDate: row.vaccinationDate,
      })),
    });
    m.success = true;
    m.rows = [emptyHistoricalRow()];
    await loadChildVaccinations(selectedChild.value.id);
    // vaccStatus/nextVaccine are computed once in loadAll() and baked into
    // each child object — loadChildVaccinations() above only refreshes the
    // history table, so without this the summary badge keeps showing
    // whatever was true before these records were saved.
    await loadAll();
    const refreshed = children.value.find(c => c.id === selectedChild.value?.id);
    if (refreshed) selectedChild.value = refreshed;
  } catch (e) {
    console.error("Error saving historical vaccinations:", e);
    m.error = `Save failed: ${e.response?.data?.message || e.message}`;
  } finally {
    m.submitting = false;
  }
}
function viewChild(c) { showParentDrawer.value=false; selectedChild.value=c; showChildDrawer.value=true; actionsMenuOpenFor.value=null; removeConfirmId.value=null; loadChildVaccinations(c.id); }
const askRemoveAccount = (rel) => (removeConfirmId.value = rel.id);
const cancelRemoveAccount = () => (removeConfirmId.value = null);

async function confirmRemoveAccount(rel) {
  try {
    await api.deleteRelationship(rel.id);
    await loadRelationships();
    removeConfirmId.value = null;
  } catch (e) {
    console.error("Error removing parent-child relationship:", e);
    error.value = `Remove failed: ${e.response?.data?.message || e.message}`;
    removeConfirmId.value = null;
  }
}

/* -- Linked Accounts quick-view modal (from the child table cell) -- */
const showLinkedAccountsModal = ref(false);
const viewingChildAccounts = ref(null);
const openLinkedAccountsModal = (c) => { viewingChildAccounts.value=c; showLinkedAccountsModal.value=true; };
const manageFromLinkedAccountsModal = () => { showLinkedAccountsModal.value=false; viewChild(viewingChildAccounts.value); };

/* =========================================================================
   GENERIC PICKER MODAL — replaces "Link Existing Child" / "Link Parent-
   Guardian" / "Transfer Guardian". picker.mode drives behavior; .parent /
   .child / .rel carry whatever context the caller had.
========================================================================= */
const PICKER_CONFIG = {
  linkChild:  { title:"Link Existing Child",   searchPlaceholder:"Search by Child Name, Family No., or Birth Date...", showPrimary:true },
  linkParent: { title:"Link Parent / Guardian", searchPlaceholder:"Search by Name, Phone, Email, or Barangay...",     showPrimary:true },
  transfer:   { title:"Transfer Guardian",      searchPlaceholder:"Search for a parent account...",                    showPrimary:false },
};
const picker = ref({
  open:false,
  mode:null,
  query:"",
  selected:null,
  relationship:"",
  isPrimary:false,
  parent:null,
  child:null,
  rel:null,
  submitting:false,
  error:null
});

function openPicker(mode, ctx={}) {
  picker.value = {
    open:true,
    mode,
    query:"",
    selected:null,
    relationship:"",
    isPrimary:false,
    parent:null,
    child:null,
    rel:null,
    submitting:false,
    error:null,
    ...ctx
  };
  actionsMenuOpenFor.value=null;
}

const closePicker = () => {
  if (picker.value.submitting) return;
  picker.value.open = false;
};

const pickerResults = computed(() => {
  const s = picker.value, q = s.query.trim().toLowerCase();
  if (s.mode==="linkChild") {
    const linked = s.parent ? relationshipsOfParent(s.parent).map(r=>r.childId) : [];
    const list = children.value.filter(c=>!linked.includes(c.id));
    return q ? list.filter(c=>c.name.toLowerCase().includes(q)||c.familyNo.toLowerCase().includes(q)||c.birthDate.toLowerCase().includes(q)) : list;
  }
  if (s.mode==="linkParent") {
    const linked = s.child ? relationshipsOfChild(s.child).map(r=>r.parentId) : [];
    const list = parents.value.filter(p=>!linked.includes(p.id));
    return q ? list.filter(p=>p.name.toLowerCase().includes(q)||(p.contact||"").includes(q)||(p.email||"").toLowerCase().includes(q)||String(p.barangay||"").includes(q)) : list;
  }
  if (s.mode==="transfer") {
    const list = parents.value.filter(p=>p.id!==s.rel?.parentId);
    return q ? list.filter(p=>p.name.toLowerCase().includes(q)||(p.contact||"").includes(q)||(p.email||"").toLowerCase().includes(q)) : list;
  }
  return [];
});
async function confirmPicker() {
  const s = picker.value;

  if (!s.selected || !s.relationship) return;

  s.error = null;
  s.submitting = true;

  try {
    if (s.mode === "transfer") {
      // Transfer = deactivate old relationship, then create the new one.
      await api.deleteRelationship(s.rel.id);

      await api.createRelationship({
        childID: s.rel.childId,
        parentID: s.selected.id,
        relationshipType: s.relationship,
        isPrimaryContact: !!s.rel.isPrimary,
        canReceiveNotifications: s.rel.canReceiveNotifications !== false
      });
    } else {
      const parentId = s.mode === "linkChild"
        ? s.parent.id
        : s.selected.id;

      const childId = s.mode === "linkChild"
        ? s.selected.id
        : s.child.id;

      // This is the real API call that creates the link.
      await api.createRelationship({
        childID: childId,
        parentID: parentId,
        relationshipType: s.relationship,
        isPrimaryContact: !!s.isPrimary,
        canReceiveNotifications: true
      });
    }

    // Re-read the database so the UI is never relying on fake/local links.
    await loadRelationships();

    // Keep the currently opened child drawer showing the fresh relationship list.
    if (selectedChild.value?.id) {
      const freshChild = children.value.find(c => c.id === selectedChild.value.id);
      if (freshChild) selectedChild.value = freshChild;
    }

    // closePicker() won't close while a save is running, so finish it first
    s.submitting = false;
    closePicker();
  } catch (e) {
    console.error("Error saving parent-child relationship:", e);
    s.error = `Link failed: ${e.response?.data?.message || e.message}`;
  } finally {
    s.submitting = false;
  }
}

/* =========================================================================
   GENERIC EDIT MODAL — replaces Edit Parent / Edit Child. Fields v-model
   onto a working copy (editModal.item).
========================================================================= */
// Names are edited as separate First / Middle / Last fields: splitting one
// "Full Name" box on spaces broke two-word first names ("Mark Anthony").
const EDIT_FIELDS = {
  parent: [
    { key:"firstName", label:"First Name", group:"name" }, { key:"middleName", label:"Middle Name", group:"name" }, { key:"lastName", label:"Last Name", group:"name" },
    { key:"contact", label:"Contact Number", type:"phone", group:"contact" }, { key:"barangay", label:"Barangay No.", type:"barangay", group:"contact" },
    { key:"email", label:"Email" }, { key:"address", label:"Address" },
  ],
  child: [
    { key:"firstName", label:"First Name", group:"name" }, { key:"middleName", label:"Middle Name", group:"name" }, { key:"lastName", label:"Last Name", group:"name" },
    { key:"birthDateInput", label:"Birth Date", type:"date", group:"birth" }, { key:"sex", label:"Sex", type:"select", options:["Male","Female"], group:"birth" },
    { key:"familyNo", label:"Family No.", group:"family" }, { key:"barangayInput", label:"Barangay", type:"barangay", group:"family" },
    { key:"height", label:"Birth Height (cm)", type:"number", group:"metrics" }, { key:"weight", label:"Birth Weight (kg)", type:"number", group:"metrics" },
    { key:"allergies", label:"Allergies (leave blank if none)" },
    { key:"address", label:"Address" },
  ],
};
const editModal = ref({ open:false, kind:null, item:null });
function openEdit(kind, item) {
  const working = { ...item };
  const raw = item.raw || {};
  if (kind === "child") {
    // Seed every field from the original API record, not from the display
    // strings in the table ("50 cm", "—", "Jan 5, 2026").
    working.firstName = raw.firstName || "";
    working.middleName = raw.middleName || "";
    working.lastName = raw.lastName || "";
    working.birthDateInput = toDateInputValue(raw.birthDate);
    working.sex = raw.sex || "";
    working.height = raw.birthHeight ?? "";
    working.weight = raw.birthWeight ?? "";
    working.allergies = raw.allergies || "";
    working.familyNo = raw.familyNo || "";
    working.barangayInput = raw.barangay == null ? "" : String(raw.barangay);
    working.address = raw.address || "";
  } else {
    working.firstName = raw.firstName || "";
    working.middleName = raw.middleName || "";
    working.lastName = raw.lastName || "";
    working.contact = raw.contactNo || "";
    working.barangay = raw.barangayNo ? String(raw.barangayNo).trim() : "";
    working.email = raw.email || "";
    working.address = raw.address || "";
  }
  editModal.value = { open:true, kind, item:working };
  actionsMenuOpenFor.value = null;
}

const toNumberOrNull = (v) => (v === "" || v == null || isNaN(parseFloat(v)) ? null : parseFloat(v));

async function submitEdit() {
  const kind = editModal.value.kind, item = editModal.value.item;
  try {
    if (kind==="parent") {
      if (!item.firstName?.trim() || !item.lastName?.trim()) { error.value = "First and last name are required."; return; }
      const phoneError = phMobileError(item.contact, { required: true });
      if (phoneError) { error.value = phoneError; return; }
      await api.updateParent(item.id, {
        firstName:item.firstName.trim(), middleName:item.middleName?.trim() || "", lastName:item.lastName.trim(),
        email:item.email?.trim() || null, contactNo:item.contact, address:item.address || "", barangayNo:item.barangay || "",
      });
    } else {
      const raw = item.raw || {};
      if (!item.firstName?.trim() || !item.lastName?.trim()) { error.value = "First and last name are required."; return; }
      if (!item.birthDateInput) { error.value = "Birth date is required."; return; }
      // UpdateChild (see ChildrenController) overwrites every column from the DTO, so fields this
      // form doesn't edit are re-sent from the last-known raw record to avoid wiping them out.
      await api.updateChild(item.id, {
        firstName:item.firstName.trim(), middleName:item.middleName || "", lastName:item.lastName.trim(),
        birthDate:item.birthDateInput,
        placeOfBirth:raw.placeOfBirth||"", sex:item.sex || raw.sex || "",
        barangay:item.barangayInput === "" || item.barangayInput == null ? null : parseInt(item.barangayInput, 10),
        familyNo:item.familyNo?.trim() || null,
        birthHeight:toNumberOrNull(item.height),
        birthWeight:toNumberOrNull(item.weight),
        allergies:item.allergies?.trim() || null,
        address:item.address || "", healthCenter:raw.healthCenter||"",
      });
    }
    await loadAll();
    editModal.value.open = false; error.value = null;
  } catch (e) { console.error("Error updating record:", e); error.value = e.response?.status === 409 ? e.response.data.message : `Update failed: ${e.response?.data?.message || e.message}`; }
}
// Groups fields into a shared row when consecutive fields share the same `group` tag
// (e.g. First/Middle/Last Name together, Height/Weight together).
function fieldRows(fields) {
  const rows = [];
  for (let i = 0; i < fields.length; ) {
    const group = fields[i].group;
    if (group) {
      const row = [fields[i]];
      let j = i + 1;
      while (fields[j] && fields[j].group === group) { row.push(fields[j]); j++; }
      rows.push(row);
      i = j;
    } else {
      rows.push([fields[i]]);
      i++;
    }
  }
  return rows;
}

/* =========================================================================
   REGISTER MODAL — ported from StaffParentRecord.vue (parent form + optional
   child-linking) and StaffChildRecord.vue (Step 1/2/3 child form with
   parent search/linking). Patient Records is now the single place both
   record types get created, using the `parents.value` / `children.value`
   already loaded by loadAll() instead of a separate fetch.
========================================================================= */
const registerModal = ref({ open:false, kind:null }); // kind: "parent" | "child"
const registerSubmitting = ref(false);

function openRegisterModal(kind, ctx = {}) {
  registerModal.value = { open:true, kind };
  actionsMenuOpenFor.value = null;
  error.value = null;
  if (kind === "parent") {
    resetParentForm();
  } else {
    resetChildForm();
    // Opened from a specific parent's row/drawer — pre-link that parent.
    if (ctx.parent) {
      regSelectedRole.value = "Mother";
      regLinkExistingParent(ctx.parent);
    }
  }
}
const closeRegisterModal = () => { registerModal.value.open = false; };

// ── PARENT FORM ─────────────────────────────────────────────────────
const regParentForm = reactive({ GivenName:"", MiddleName:"", LastName:"", Email:"", ContactNo:"", BarangayNo:"", Address:"", CreateLogin:true, PrivacyConsent:false });
// Letters/spaces/hyphens/apostrophes/periods only — blocks digits in name fields (allows names like "St. Clair", "O'Brien").
const lettersOnlyInput = (field) => (e) => { regParentForm[field] = e.target.value.replace(/[^a-zA-Z\s.'-]/g, ""); };
function resetParentForm() {
  Object.assign(regParentForm, { GivenName:"", MiddleName:"", LastName:"", Email:"", ContactNo:"", BarangayNo:"", Address:"", CreateLogin:true, PrivacyConsent:false });
  regChildSearch.value = "";
  regLinkedChildren.value = [];
}

// Link to Existing Child (optional, at parent-registration time)
const regChildSearch = ref("");
const regLinkRole = ref("Mother");
const regLinkRoleOptions = ["Mother","Father","Guardian","Grandmother","Grandfather","Aunt","Uncle","Sibling","Foster Parent","Relative","Other"];
const regLinkedChildren = ref([]); // [{ childID, name, role }]
const regFilteredChildrenForLink = computed(() => {
  const q = regChildSearch.value.trim().toLowerCase();
  if (!q) return [];
  return children.value
    .filter(c => !regLinkedChildren.value.some(lc => lc.childID === c.id))
    .filter(c => c.name.toLowerCase().includes(q))
    .slice(0, 8);
});
function regAddLinkedChild(child) {
  regLinkedChildren.value.push({ childID: child.id, name: child.name, role: regLinkRole.value });
  regChildSearch.value = "";
}
function regRemoveLinkedChild(childID) {
  regLinkedChildren.value = regLinkedChildren.value.filter(lc => lc.childID !== childID);
}

async function submitParentRegister() {
  if (!regParentForm.GivenName || !regParentForm.LastName || !regParentForm.ContactNo) {
    error.value = "Please fill all required parent fields."; return;
  }
  const phoneError = phMobileError(regParentForm.ContactNo);
  if (phoneError) { error.value = phoneError; return; }
  if (regParentForm.CreateLogin) {
    if (!regParentForm.Email) {
      error.value = "Email is required to give this guardian a login."; return;
    }
  }
  if (!regParentForm.PrivacyConsent) {
    error.value = "The parent/guardian must agree to the Data Privacy Notice before they can be registered."; return;
  }

  registerSubmitting.value = true;
  try {
    const created = await api.createParent({
      firstName: regParentForm.GivenName,
      lastName: regParentForm.LastName,
      email: regParentForm.CreateLogin ? regParentForm.Email : null,
      contactNo: regParentForm.ContactNo,
      address: regParentForm.Address,
      barangayNo: regParentForm.BarangayNo,
      // No password: the system makes a temporary one (same as the admin's
      // Add User) and returns it once so staff can hand it to the parent.
      password: null,
      createLogin: regParentForm.CreateLogin,
      privacyConsent: regParentForm.PrivacyConsent,
    });

    // NOTE: assumes ParentsController's POST returns the created parent's
    // ID as `parentID` (matching the camelCase shape every other endpoint
    // here returns, e.g. ChildrenController's `childID`). If that's wrong,
    // the parent still saves — just the child links below silently no-op
    // and can be added afterward via "Link Existing Child".
    const newParentID = created?.parentID;
    let linkFailures = 0;
    if (newParentID) {
      for (const lc of regLinkedChildren.value) {
        try {
          await api.createRelationship({
            childID: lc.childID, parentID: newParentID,
            relationshipType: lc.role, isPrimaryContact:false, canReceiveNotifications:true,
          });
        } catch (e) { console.error("Link failed for child", lc.childID, e); linkFailures++; }
      }
    }

    await loadAll();
    closeRegisterModal();
    if (created?.temporaryPassword) {
      const realEmail = created.email && !/@example\.com$|@demo\./i.test(created.email);
      accountDialog.value = {
        result: {
          password: created.temporaryPassword,
          text: `Give this to ${created.firstName} ${created.lastName}. They sign in with ${created.email} and will choose their own password the first time.`
            + " " + sentNote(realEmail && created.emailed, created.texted, created.email),
        },
      };
    }
    if (linkFailures > 0) {
      error.value = null;
      showAlert({
        title: "Parent registered, but some children weren't linked",
        message: `${linkFailures} child link(s) failed to save. You can link them from the parent's profile instead.`,
        tone: "error",
      });
    }
  } catch (e) {
    console.error("Error registering parent:", e);
    let errorMsg = e.response?.data?.message || e.message;
    // Convert database truncation errors to user-friendly message
    if (errorMsg?.includes("String or binary data would be truncated") || errorMsg?.includes("ContactNo")) {
      errorMsg = "Contact number is too long. Please enter a valid Philippines mobile number (e.g., 09171234567).";
    }
    // 409 = already registered (email / phone): show the plain message
    error.value = e.response?.status === 409 ? errorMsg : `Registration failed: ${errorMsg}`;
  } finally { registerSubmitting.value = false; }
}

// ── CHILD FORM (Step 1/2/3) ─────────────────────────────────────────
const regSelectedRole = ref("Mother");
const regParentSearch = ref("");
const regChildForm = reactive({
  FirstName:"", MiddleName:"", LastName:"", BirthDate:"", Sex:"Male", PlaceOfBirth:"", Address:"",
  MotherName:"", MotherEmail:"", MotherID:null,
  FatherName:"", FatherEmail:"", FatherID:null,
  GuardianName:"", GuardianEmail:"", GuardianID:null,
  BirthWeight:null, BirthHeight:null,
  HealthCenter:"Leveriza Health Center", Barangay:"", FamilyNo:"",
});
function resetChildForm() {
  Object.assign(regChildForm, {
    FirstName:"", MiddleName:"", LastName:"", BirthDate:"", Sex:"Male", PlaceOfBirth:"", Address:"",
    MotherName:"", MotherEmail:"", MotherID:null,
    FatherName:"", FatherEmail:"", FatherID:null,
    GuardianName:"", GuardianEmail:"", GuardianID:null,
    BirthWeight:null, BirthHeight:null,
    HealthCenter:"Leveriza Health Center", Barangay:"", FamilyNo:"",
  });
  regParentSearch.value = "";
  regSelectedRole.value = "Mother";
  regHasPriorVaccinations.value = false;
  regPriorVaccinationRows.value = [emptyHistoricalRow()];
}

// ── STEP 4: PRIOR VACCINATION HISTORY (OPTIONAL, AT REGISTRATION TIME) ──
// Lets staff record vaccines the child already received (e.g. from the
// newborn screening / prior clinic papers the parent brings in) as part of
// the same registration flow, instead of requiring a separate follow-up
// action after the child is saved. Reuses the same row shape and vaccine
// list as the standalone "Add Historical Vaccination Records" modal, and
// posts through the same api.saveHistoricalVaccinations endpoint once the
// new child's ID is known.
const regHasPriorVaccinations = ref(false);
const regPriorVaccinationRows = ref([emptyHistoricalRow()]);
const addRegPriorVaccinationRow = () => regPriorVaccinationRows.value.push(emptyHistoricalRow());
function removeRegPriorVaccinationRow(idx) {
  regPriorVaccinationRows.value.splice(idx, 1);
  if (regPriorVaccinationRows.value.length === 0) regPriorVaccinationRows.value.push(emptyHistoricalRow());
}

const regFilteredParents = computed(() => {
  if (!regParentSearch.value) return [];
  const q = regParentSearch.value.toLowerCase();
  return parents.value.filter(p => p.name.toLowerCase().includes(q) || p.email.toLowerCase().includes(q)).slice(0, 5);
});

function regHandleManualNameEntry() {
  const role = regSelectedRole.value;
  regChildForm[`${role}Name`] = regParentSearch.value;
  regChildForm[`${role}ID`] = null;
  regChildForm[`${role}Email`] = "";
}
function regLinkExistingParent(parent) {
  const role = regSelectedRole.value;
  regChildForm[`${role}Name`] = parent.name;
  regChildForm[`${role}ID`] = parent.id;
  regChildForm[`${role}Email`] = parent.email;
  regParentSearch.value = "";
  // Siblings share the family's Family No., barangay and address: fill
  // them in from the parent (and an existing child) when still empty.
  const sibling = relationshipsOfParent(parent).map(r => children.value.find(c => c.id === r.childId)).find(Boolean);
  if (!regChildForm.FamilyNo && sibling?.familyNo) regChildForm.FamilyNo = sibling.familyNo;
  if (!regChildForm.Barangay && parent.barangay && isServedBarangay(parent.barangay)) regChildForm.Barangay = String(parent.barangay).trim();
  if (!regChildForm.Address && parent.address) regChildForm.Address = parent.address;
}
function regClearParentRole(role) {
  regChildForm[`${role}Name`] = ""; regChildForm[`${role}ID`] = null; regChildForm[`${role}Email`] = "";
}

// ChildrenController's real DTO takes a Parents ARRAY (confirmed from the
// controller source) — one entry per role that got LINKED to an existing
// account. A role typed in manually has no ParentID and can't be included;
// the backend only accepts real linked accounts, not free-text names.
async function submitChildRegister() {
  if (!regChildForm.FirstName || !regChildForm.LastName) { error.value = "Please enter the child's name."; return; }
  if (!regChildForm.BirthDate) { error.value = "Please enter a birth date."; return; }
  if (regChildForm.BirthDate > todayISO) { error.value = "The birth date can't be in the future."; return; }

  const linkedParents = [];
  for (const role of ["Mother", "Father", "Guardian"]) {
    const parentID = regChildForm[`${role}ID`];
    if (parentID) {
      linkedParents.push({ parentID, relationshipType: role, isPrimaryContact: linkedParents.length === 0, canReceiveNotifications: true });
    }
  }
  if (linkedParents.length === 0) {
    error.value = "Link at least one parent/guardian to an existing account (search and select from the results) — a manually-typed name with no account can't be saved yet.";
    return;
  }

  // Validate prior-vaccination rows up front, same rules as the standalone
  // historical modal, so a bad row is caught before the child is even created.
  let priorVaccinationsToSave = [];
  if (regHasPriorVaccinations.value) {
    const rows = regPriorVaccinationRows.value;
    for (const row of rows) {
      if (!row.vaccineID) { error.value = "Every prior vaccination row needs a vaccine selected."; return; }
      if (!row.doseNumber || Number(row.doseNumber) <= 0) { error.value = "Dose number must be greater than 0 for every prior vaccination row."; return; }
      if (!row.vaccinationDate) { error.value = "Every prior vaccination row needs a vaccination date."; return; }
      if (row.vaccinationDate > todayISO || row.vaccinationDate < regChildForm.BirthDate) { error.value = "Prior vaccination dates must be between the birth date and today."; return; }
    }
    const seen = new Set();
    for (const row of rows) {
      const key = `${row.vaccineID}-${row.doseNumber}-${row.vaccinationDate}`;
      if (seen.has(key)) { error.value = "Duplicate vaccine/dose/date rows found in prior vaccinations — please review."; return; }
      seen.add(key);
    }
    priorVaccinationsToSave = rows.map(row => ({
      vaccineID: Number(row.vaccineID),
      doseNumber: Number(row.doseNumber),
      vaccinationDate: row.vaccinationDate,
    }));
  }

  registerSubmitting.value = true;
  try {
    const createdChild = await api.createChild({
      firstName: regChildForm.FirstName, middleName: regChildForm.MiddleName || null, lastName: regChildForm.LastName,
      birthDate: regChildForm.BirthDate, placeOfBirth: regChildForm.PlaceOfBirth || null, sex: regChildForm.Sex,
      barangay: regChildForm.Barangay ? Number(regChildForm.Barangay) : null,
      address: regChildForm.Address || null, healthCenter: regChildForm.HealthCenter || null,
      familyNo: regChildForm.FamilyNo?.trim() || null,
      // Saved to Children.BirthHeight (cm) / BirthWeight (kg)
      birthHeight: regChildForm.BirthHeight !== null && regChildForm.BirthHeight !== "" ? Number(regChildForm.BirthHeight) : null,
      birthWeight: regChildForm.BirthWeight !== null && regChildForm.BirthWeight !== "" ? Number(regChildForm.BirthWeight) : null,
      parents: linkedParents,
    });

    // Same assumption as the parent-link flow above: createChild's response
    // is expected to return the new record's ID as `childID`. If that's
    // wrong, the child still saves — the prior vaccinations just silently
    // don't, and can be added afterward via "Add Historical Vaccination
    // Records" from the child's row.
    const newChildID = createdChild?.childID;
    let priorVaccinationSaveFailed = false;
    let priorVaccinationError = "";
    if (priorVaccinationsToSave.length > 0 && newChildID) {
      try {
        await api.saveHistoricalVaccinations({ childID: newChildID, vaccinations: priorVaccinationsToSave });
      } catch (e) {
        console.error("Error saving prior vaccinations at registration:", e);
        priorVaccinationSaveFailed = true;
        priorVaccinationError = e.response?.data?.message || "";
      }
    } else if (priorVaccinationsToSave.length > 0) {
      priorVaccinationSaveFailed = true;
    }

    await loadAll();
    closeRegisterModal();
    if (priorVaccinationSaveFailed) {
      error.value = null;
      showAlert({
        title: "Child registered, but the Yellow Book doses weren't saved",
        message: (priorVaccinationError ? `${priorVaccinationError}\n\n` : "")
          + "Add them from the child's row with \"Add Historical Vaccination Records\".",
        tone: "error",
      });
    }
  } catch (e) {
    console.error("Error registering child:", e);
    error.value = e.response?.status === 409 ? e.response.data.message : `Registration failed: ${e.response?.data?.message || e.message}`;
  } finally { registerSubmitting.value = false; }
}
</script>

<template>
  <div class="flex min-h-screen w-full bg-stone-50 text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;">
    <!-- ---------------- Sidebar ---------------- -->
    <StaffSidebar />

    <!-- ---------------- Main ---------------- -->
    <main class="flex-1 min-w-0 pb-10">
      <StaffTopbar title="Patient Management" breadcrumb="Aruga / Patient Management" :unread-count="1" />

      <div class="px-8 py-6 space-y-6">
        <!-- Summary cards -->
        <div class="grid grid-cols-4 gap-4">
          <div v-for="c in (activeTab === 'parents' ? parentSummary : childSummary)" :key="c.label" class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
            <div class="flex items-center justify-between">
              <p class="text-[10.5px] font-semibold uppercase tracking-wide text-stone-500 leading-tight">{{ c.label }}</p>
              <div class="flex h-7 w-7 items-center justify-center rounded-lg shrink-0" :class="c.tintBg"><component :is="c.icon" :size="14" :class="c.tint" /></div>
            </div>
            <p class="text-[24px] font-bold mt-2">{{ c.value }}</p>
          </div>
        </div>

        <!-- Tabs + toolbar -->
        <div class="rounded-2xl border border-stone-200 bg-white p-4 shadow-sm">
          <div class="flex flex-wrap items-center gap-3">
            <div class="flex items-center rounded-xl border border-stone-200 p-1">
              <button @click="activeTab = 'parents'; searchQuery = ''" class="flex items-center gap-1.5 rounded-lg px-4 py-1.5 text-[12.5px] font-medium" :class="activeTab === 'parents' ? 'bg-emerald-700 text-white' : 'text-stone-500'"><UserCircle2 :size="14" /> Parents</button>
              <button @click="activeTab = 'children'; searchQuery = ''" class="flex items-center gap-1.5 rounded-lg px-4 py-1.5 text-[12.5px] font-medium" :class="activeTab === 'children' ? 'bg-emerald-700 text-white' : 'text-stone-500'"><Baby :size="14" /> Children</button>
            </div>
            <div class="flex items-center gap-2 flex-1 min-w-[240px] rounded-xl border border-stone-200 px-3 py-2.5">
              <Search :size="16" class="text-stone-400 shrink-0" />
              <input v-model="searchQuery" type="text" :placeholder="activeTab === 'parents' ? 'Search by name, contact, email or barangay...' : 'Search by name, Family No. or barangay...'" class="flex-1 text-[13px] outline-none placeholder:text-stone-400" />
            </div>
            <div class="flex-1"></div>
            <button v-if="activeTab === 'parents'" @click="openRegisterModal('parent')" class="flex items-center gap-2 rounded-xl px-4 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 shadow-sm"><UserPlus :size="16" /> Register Parent</button>
            <button v-else @click="openRegisterModal('child')" class="flex items-center gap-2 rounded-xl px-4 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 shadow-sm"><UserPlus :size="16" /> Register Child</button>
          </div>
        </div>

        <!-- ============== PARENTS TABLE (columns driven by parentColumns) ============== -->
        <div v-if="activeTab === 'parents'" class="rounded-2xl border border-stone-200 bg-white shadow-sm overflow-visible">
          <div class="flex items-center justify-between px-5 py-4 border-b border-stone-200"><p class="text-[14px] font-semibold">Parent Directory</p><span class="text-[12px] text-stone-500">{{ filteredParents.length }} parents</span></div>
          <div class="overflow-x-auto">
            <table class="w-full text-[13px]">
              <thead><tr class="bg-stone-50">
                <th v-for="col in parentColumns" :key="col.key" class="text-left font-semibold px-4 py-3 whitespace-nowrap text-[11px] uppercase tracking-wide text-stone-500">{{ col.label }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                <tr v-for="p in paginatedParents" :key="p.id" class="border-t border-stone-200">
                  <td v-for="col in parentColumns" :key="col.key" class="whitespace-nowrap" :class="[col.muted && 'text-stone-500', col.badge==='avatar' ? 'pl-5 pr-2 py-3' : 'px-4 py-3']">
                    <Avatar v-if="col.badge==='avatar'" :text="p.initials" size="sm" />
                    <StatusBadge v-else-if="col.badge==='status'" :status="p.status" />
                    <template v-else>{{ col.value ? col.value(p) : p[col.key] }}</template>
                  </td>
                  <td class="px-4 py-3 whitespace-nowrap">
                    <div class="flex items-center gap-1 relative">
                      <button @click="viewParent(p)" class="p-1.5 rounded-lg hover:bg-stone-100" title="View Details"><Eye :size="15" class="text-stone-500" /></button>
                      <button @click="openEdit('parent', p)" class="p-1.5 rounded-lg hover:bg-stone-100" title="Edit Parent"><Pencil :size="15" class="text-stone-500" /></button>
                      <button @click="toggleActionsMenu(p.id, $event)" class="p-1.5 rounded-lg hover:bg-stone-100" title="More actions"><MoreHorizontal :size="15" class="text-stone-500" /></button>
                      <Teleport to="body">
                        <template v-if="actionsMenuOpenFor === p.id">
                          <div class="fixed inset-0 z-40" @click="closeActionsMenu"></div>
                          <div class="fixed z-50 w-56 rounded-xl border border-stone-200 bg-white shadow-lg py-1.5" :style="actionsMenuStyle">
                            <template v-for="(m,i) in parentMenuItems(p)" :key="i">
                              <div v-if="m.divider" class="my-1 border-t border-stone-100"></div>
                              <button v-else @click="closeActionsMenu(); m.action(p)" class="flex w-full items-center gap-2.5 px-3.5 py-2 text-[12.5px] hover:bg-stone-50" :class="m.class || 'text-stone-700'">
                                <component :is="m.icon" :size="15" :class="m.class ? '' : 'text-stone-500'" /> {{ m.label }}
                              </button>
                            </template>
                          </div>
                        </template>
                      </Teleport>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- ============== CHILDREN TABLE (columns driven by childColumns) ============== -->
        <div v-else class="rounded-2xl border border-stone-200 bg-white shadow-sm overflow-visible">
          <div class="flex items-center justify-between px-5 py-4 border-b border-stone-200"><p class="text-[14px] font-semibold">Child Directory</p><span class="text-[12px] text-stone-500">{{ filteredChildren.length }} children</span></div>
          <div class="overflow-x-auto">
            <table class="w-full text-[13px]">
              <thead><tr class="bg-stone-50">
                <th v-for="col in childColumns" :key="col.key" class="text-left font-semibold px-4 py-3 whitespace-nowrap text-[11px] uppercase tracking-wide text-stone-500">{{ col.label }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                <tr v-for="ch in paginatedChildren" :key="ch.id" class="border-t border-stone-200">
                  <td v-for="col in childColumns" :key="col.key" class="whitespace-nowrap" :class="[col.muted && 'text-stone-500', col.badge==='childIcon' ? 'pl-5 pr-2 py-3' : 'px-4 py-3']">
                    <div v-if="col.badge==='childIcon'" class="flex h-9 w-9 items-center justify-center rounded-full bg-emerald-50"><Baby :size="16" class="text-emerald-700" /></div>
                    <VaccBadge v-else-if="col.badge==='vacc'" :status="ch.vaccStatus" />
                    <StatusBadge v-else-if="col.badge==='status'" :status="ch.status" />
                    <template v-else-if="col.badge==='linked'">
                      <button v-if="linkedAccountsOf(ch).length > 0" @click="openLinkedAccountsModal(ch)" class="text-left rounded-lg -mx-1 px-1 py-0.5 hover:bg-stone-50 transition-colors">
                        <span class="block text-stone-700">{{ linkedAccountsOf(ch)[0].rel.relationship }}: {{ linkedAccountsOf(ch)[0].parent.name }}</span>
                        <span v-if="linkedAccountsOf(ch).length > 1" class="block text-[11.5px] font-medium text-emerald-700">+{{ linkedAccountsOf(ch).length - 1 }} more</span>
                      </button>
                      <button v-else @click="viewChild(ch)" class="inline-flex items-center gap-1 text-rose-700 text-[12px] font-medium hover:underline"><Link2 :size="12" /> Not linked</button>
                    </template>
                    <template v-else>{{ col.value ? col.value(ch) : ch[col.key] }}</template>
                  </td>
                  <td class="px-4 py-3 whitespace-nowrap">
                    <div class="flex items-center gap-1 relative">
                      <button @click="viewChild(ch)" class="p-1.5 rounded-lg hover:bg-stone-100" title="View Child"><Eye :size="15" class="text-stone-500" /></button>
                      <button @click="openEdit('child', ch)" class="p-1.5 rounded-lg hover:bg-stone-100" title="Edit Child"><Pencil :size="15" class="text-stone-500" /></button>
                      <button @click="toggleActionsMenu(ch.id, $event)" class="p-1.5 rounded-lg hover:bg-stone-100" title="More actions"><MoreHorizontal :size="15" class="text-stone-500" /></button>
                      <Teleport to="body">
                        <template v-if="actionsMenuOpenFor === ch.id">
                          <div class="fixed inset-0 z-40" @click="closeActionsMenu"></div>
                          <div class="fixed z-50 w-56 rounded-xl border border-stone-200 bg-white shadow-lg py-1.5" :style="actionsMenuStyle">
                            <template v-for="(m,i) in childMenuItems" :key="i">
                              <div v-if="m.divider" class="my-1 border-t border-stone-100"></div>
                              <button v-else @click="closeActionsMenu(); m.action(ch)" class="flex w-full items-center gap-2.5 px-3.5 py-2 text-[12.5px] hover:bg-stone-50" :class="m.class || 'text-stone-700'">
                                <component :is="m.icon" :size="15" :class="m.class ? '' : 'text-stone-500'" /> {{ m.label }}
                              </button>
                            </template>
                          </div>
                        </template>
                      </Teleport>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- ============== PAGINATION FOOTER (shared — only one table is visible at a time) ============== -->
      <div v-if="activeFilteredCount > 0" class="flex flex-wrap items-center justify-between gap-3">
        <div class="flex items-center gap-2 text-[12.5px] text-stone-500">
          <span>Show</span>
          <select v-model.number="pageSize" class="rounded-lg border border-stone-200 px-2 py-1 text-[12.5px] outline-none bg-white">
            <option v-for="n in pageSizeOptions" :key="n" :value="n">{{ n }}</option>
          </select>
          <span>entries · {{ paginationRangeLabel }}</span>
        </div>
        <div class="flex items-center gap-1">
          <button @click="goToPage(currentPage - 1)" :disabled="currentPage === 1" class="px-2.5 py-1.5 rounded-lg text-[12.5px] font-medium text-stone-500 hover:bg-stone-100 disabled:opacity-40 disabled:hover:bg-transparent">Prev</button>
          <template v-for="(n, i) in pageNumbers" :key="i">
            <span v-if="n === '...'" class="px-2 text-[12.5px] text-stone-400">...</span>
            <button v-else @click="goToPage(n)" class="min-w-[30px] px-2.5 py-1.5 rounded-lg text-[12.5px] font-medium" :class="n === currentPage ? 'bg-emerald-700 text-white' : 'text-stone-600 hover:bg-stone-100'">{{ n }}</button>
          </template>
          <button @click="goToPage(currentPage + 1)" :disabled="currentPage === totalPages" class="px-2.5 py-1.5 rounded-lg text-[12.5px] font-medium text-stone-500 hover:bg-stone-100 disabled:opacity-40 disabled:hover:bg-transparent">Next</button>
        </div>
      </div>
    </main>

    <!-- Linked Accounts quick-view modal -->
    <div v-if="showLinkedAccountsModal && viewingChildAccounts" class="fixed inset-0 z-50 flex items-center justify-center bg-stone-900/40 px-4">
      <div class="w-full max-w-md rounded-2xl bg-white shadow-xl">
        <div class="flex items-center justify-between px-6 py-4 border-b border-stone-200">
          <div><p class="text-[15px] font-semibold">Linked Parent / Guardian Accounts</p><p class="text-[11.5px] text-stone-500">{{ viewingChildAccounts.name }} · {{ viewingChildAccounts.id }}</p></div>
          <button @click="showLinkedAccountsModal = false" class="p-1.5 rounded-lg hover:bg-stone-100"><X :size="18" class="text-stone-500" /></button>
        </div>
        <div class="p-6 space-y-2 max-h-[60vh] overflow-y-auto">
          <div v-for="a in linkedAccountsOf(viewingChildAccounts)" :key="a.rel.id" class="flex items-center gap-3 rounded-xl border border-stone-200 p-3">
            <Avatar :text="a.parent.initials" size="md" />
            <div class="flex-1 min-w-0">
              <p class="text-[12.5px] font-semibold flex items-center gap-1.5">{{ a.parent.name }} <Star v-if="a.rel.isPrimary" :size="12" class="text-amber-500 fill-amber-500" /></p>
              <p class="text-[11px] text-stone-500">{{ a.rel.relationship }} · {{ a.parent.contact }}</p>
            </div>
            <span v-if="a.rel.isPrimary" class="text-[10.5px] font-medium text-amber-700 bg-amber-50 rounded-full px-2 py-1 shrink-0">Primary</span>
          </div>
        </div>
        <div class="flex items-center justify-end gap-3 px-6 py-4 border-t border-stone-200">
          <button @click="showLinkedAccountsModal = false" class="rounded-xl px-4 py-2.5 text-[13px] font-medium text-stone-600 hover:bg-stone-50">Close</button>
          <button @click="manageFromLinkedAccountsModal" class="rounded-xl px-5 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 shadow-sm">Manage Accounts</button>
        </div>
      </div>
    </div>

    <!-- VIEW PARENT DRAWER -->
    <transition name="fade"><div v-if="showParentDrawer" class="fixed inset-0 z-30 bg-stone-900/30" @click="showParentDrawer = false"></div></transition>
    <transition name="slide">
      <aside v-if="showParentDrawer && selectedParent" class="fixed right-0 top-0 z-40 h-full w-[440px] bg-white border-l border-stone-200 shadow-xl overflow-y-auto">
        <div class="flex items-center justify-between px-5 py-4 border-b border-stone-200 sticky top-0 bg-white">
          <div><p class="text-[15px] font-semibold">Parent Information</p><p class="text-[11.5px] text-stone-500">{{ selectedParent.id }}</p></div>
          <button @click="showParentDrawer = false" class="p-1.5 rounded-lg hover:bg-stone-100"><X :size="17" class="text-stone-500" /></button>
        </div>
        <div class="p-5 space-y-6">
          <div class="flex items-center gap-4">
            <Avatar :text="selectedParent.initials" size="lg" />
            <div><p class="text-[16px] font-bold">{{ selectedParent.name }}</p><p class="text-[12px] text-stone-500">{{ selectedParent.relationship }}</p><div class="mt-1"><StatusBadge :status="selectedParent.status" /></div></div>
          </div>
          <div class="rounded-xl bg-stone-50 p-3.5 space-y-2">
            <div class="flex items-center justify-between"><span class="text-[12px] text-stone-500">Contact Number</span><span class="text-[13px] font-medium flex items-center gap-1.5"><Phone :size="13" class="text-stone-400" /> {{ selectedParent.contact }}</span></div>
            <div class="flex items-center justify-between"><span class="text-[12px] text-stone-500">Email</span><span class="text-[13px] font-medium flex items-center gap-1.5"><Mail :size="13" class="text-stone-400" /> {{ selectedParent.email }}</span></div>
            <div class="flex items-center justify-between"><span class="text-[12px] text-stone-500">Username</span><span class="text-[13px] font-medium" :class="selectedParent.accountId ? '' : 'text-stone-400'">{{ selectedParent.accountId ? selectedParent.username : 'No portal login' }}</span></div>
            <div class="flex items-start justify-between"><span class="text-[12px] text-stone-500">Address</span><span class="text-[13px] font-medium flex items-center gap-1.5 text-right max-w-[220px]"><MapPin :size="13" class="text-stone-400 shrink-0" /> {{ selectedParent.address }}</span></div>
          </div>
          <div class="border-t border-stone-200 pt-5">
            <p class="text-[13px] font-semibold mb-3">Quick Statistics</p>
            <div class="grid grid-cols-3 gap-2">
              <div class="rounded-xl bg-stone-50 p-3 text-center"><p class="text-[18px] font-bold">{{ childrenOf(selectedParent).length }}</p><p class="text-[10.5px] text-stone-500 mt-0.5">Children</p></div>
              <div class="rounded-xl bg-sky-50 p-3 text-center"><p class="text-[18px] font-bold text-sky-700">{{ childrenOf(selectedParent).filter(x => x.child.vaccStatus === 'Upcoming').length }}</p><p class="text-[10.5px] text-stone-500 mt-0.5">Upcoming</p></div>
              <div class="rounded-xl bg-rose-50 p-3 text-center"><p class="text-[18px] font-bold text-rose-700">{{ childrenOf(selectedParent).filter(x => x.child.vaccStatus === 'Delayed').length }}</p><p class="text-[10.5px] text-stone-500 mt-0.5">Delayed</p></div>
            </div>
          </div>
          <div class="border-t border-stone-200 pt-5">
            <p class="text-[13px] font-semibold mb-3">Children Linked</p>
            <div class="space-y-2">
              <div v-for="x in childrenOf(selectedParent)" :key="x.rel.id" class="flex items-center gap-3 rounded-xl border border-stone-200 p-3">
                <div class="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-50 shrink-0"><Baby :size="17" class="text-emerald-700" /></div>
                <div class="flex-1 min-w-0"><p class="text-[12.5px] font-semibold truncate flex items-center gap-1.5">{{ x.child.name }} <Star v-if="x.rel.isPrimary" :size="11" class="text-amber-500 fill-amber-500" /></p><p class="text-[11px] text-stone-500">{{ x.child.id }} · {{ x.rel.relationship }} · {{ x.child.age }}</p></div>
                <div class="shrink-0"><VaccBadge :status="x.child.vaccStatus" /></div>
              </div>
              <p v-if="childrenOf(selectedParent).length === 0" class="text-[12.5px] text-stone-500">No children linked yet.</p>
            </div>
          </div>
          <div class="flex gap-2">
            <button @click="openRegisterModal('child', { parent: selectedParent })" class="flex-1 flex items-center justify-center gap-1.5 rounded-xl border border-stone-200 px-3 py-2 text-[12.5px] font-medium hover:bg-stone-50"><Baby :size="14" /> Register Child</button>
            <button @click="openPicker('linkChild', { parent: selectedParent })" class="flex-1 flex items-center justify-center gap-1.5 rounded-xl border border-stone-200 px-3 py-2 text-[12.5px] font-medium hover:bg-stone-50"><Link2 :size="14" /> Link Existing Child</button>
          </div>
        </div>
      </aside>
    </transition>

    <!-- VIEW CHILD DRAWER -->
    <transition name="fade"><div v-if="showChildDrawer" class="fixed inset-0 z-30 bg-stone-900/30" @click="showChildDrawer = false"></div></transition>
    <transition name="slide">
      <aside v-if="showChildDrawer && selectedChild" class="fixed right-0 top-0 z-40 h-full w-[440px] bg-white border-l border-stone-200 shadow-xl overflow-y-auto">
        <div class="flex items-center justify-between px-5 py-4 border-b border-stone-200 sticky top-0 bg-white">
          <div><p class="text-[15px] font-semibold">Child Information</p><p class="text-[11.5px] text-stone-500">{{ selectedChild.id }}</p></div>
          <button @click="showChildDrawer = false" class="p-1.5 rounded-lg hover:bg-stone-100"><X :size="17" class="text-stone-500" /></button>
        </div>
        <div class="p-5 space-y-6">
          <div class="flex items-center gap-4">
            <div class="flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-50 shrink-0"><Baby :size="24" class="text-emerald-700" /></div>
            <div><p class="text-[16px] font-bold">{{ selectedChild.name }}</p><p class="text-[12px] text-stone-500">{{ selectedChild.age }} · {{ selectedChild.sex }}</p></div>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="rounded-xl bg-stone-50 p-3"><p class="text-[10.5px] uppercase tracking-wide text-stone-500">Family No.</p><p class="text-[13px] font-medium mt-0.5">{{ selectedChild.familyNo || "—" }}</p></div>
            <div class="rounded-xl bg-stone-50 p-3"><p class="text-[10.5px] uppercase tracking-wide text-stone-500">Barangay</p><p class="text-[13px] font-medium mt-0.5">{{ selectedChild.barangay }}</p></div>
            <div class="rounded-xl bg-stone-50 p-3"><p class="text-[10.5px] uppercase tracking-wide text-stone-500">Birth Date</p><p class="text-[13px] font-medium mt-0.5">{{ selectedChild.birthDate }}</p></div>
            <div class="rounded-xl bg-stone-50 p-3"><p class="text-[10.5px] uppercase tracking-wide text-stone-500">Birth Place</p><p class="text-[13px] font-medium mt-0.5">{{ selectedChild.birthPlace }}</p></div>
            <div class="rounded-xl bg-stone-50 p-3"><p class="text-[10.5px] uppercase tracking-wide text-stone-500">Birth Height</p><p class="text-[13px] font-medium mt-0.5">{{ selectedChild.height }}</p></div>
            <div class="rounded-xl bg-stone-50 p-3"><p class="text-[10.5px] uppercase tracking-wide text-stone-500">Birth Weight</p><p class="text-[13px] font-medium mt-0.5">{{ selectedChild.weight }}</p></div>
            <div class="rounded-xl bg-stone-50 p-3 col-span-2"><p class="text-[10.5px] uppercase tracking-wide text-stone-500">Allergies</p><p class="text-[13px] font-medium mt-0.5" :class="selectedChild.allergies ? 'text-rose-700' : ''">{{ selectedChild.allergies || "None recorded" }}</p></div>
            <div class="rounded-xl bg-stone-50 p-3 col-span-2"><p class="text-[10.5px] uppercase tracking-wide text-stone-500">Existing Conditions</p><p class="text-[13px] font-medium mt-0.5" :class="selectedChild.existingConditions ? 'text-rose-700' : ''">{{ selectedChild.existingConditions || "None recorded" }}</p></div>
          </div>
          <div class="border-t border-stone-200 pt-5">
            <p class="text-[13px] font-semibold mb-3">Vaccination Summary</p>
            <div class="rounded-xl bg-stone-50 p-3.5 space-y-2">
              <div class="flex items-center justify-between"><span class="text-[12px] text-stone-500">Status</span><VaccBadge :status="selectedChild.vaccStatus" /></div>
              <div class="flex items-center justify-between"><span class="text-[12px] text-stone-500">Next Vaccine</span><span class="text-[13px] font-medium">{{ selectedChild.nextVaccine }}</span></div>
            </div>
          </div>

          <!-- Vaccination History (incl. historical records entered before Aruga registration) -->
          <div class="border-t border-stone-200 pt-5">
            <div class="flex items-center justify-between mb-3">
              <p class="text-[13px] font-semibold">Vaccination History</p>
              <button @click="openHistoricalModal" class="flex items-center gap-1.5 rounded-lg bg-emerald-700 hover:bg-emerald-800 text-white px-2.5 py-1.5 text-[11.5px] font-semibold"><Syringe :size="13" /> Add Historical Record</button>
            </div>
            <p v-if="vaccinationLoading" class="text-[12px] text-stone-500 py-2">Loading vaccination records...</p>
            <div v-else-if="selectedChildVaccinations.length" class="rounded-xl border border-stone-200 overflow-hidden">
              <table class="w-full text-[12.5px]">
                <thead><tr class="bg-stone-50">
                  <th class="text-left font-semibold px-3 py-2 text-[10.5px] uppercase tracking-wide text-stone-500">Vaccine</th>
                  <th class="text-left font-semibold px-3 py-2 text-[10.5px] uppercase tracking-wide text-stone-500">Dose</th>
                  <th class="text-left font-semibold px-3 py-2 text-[10.5px] uppercase tracking-wide text-stone-500">Date</th>
                  <th class="text-left font-semibold px-3 py-2 text-[10.5px] uppercase tracking-wide text-stone-500">Site</th>
                  <th class="text-left font-semibold px-3 py-2 text-[10.5px] uppercase tracking-wide text-stone-500">Status</th>
                </tr></thead>
                <tbody>
                  <tr v-for="v in selectedChildVaccinations" :key="v.vaccinationRecordID" class="border-t border-stone-200">
                    <td class="px-3 py-2">{{ v.vaccineName || vaccineNameById(v.vaccineID) }}</td>
                    <td class="px-3 py-2">{{ v.doseNumber }}</td>
                    <td class="px-3 py-2">{{ formatVaccDate(v.vaccinationDate) }}</td>
                    <td class="px-3 py-2 whitespace-nowrap">{{ v.injectionSite || '—' }}</td>
                    <td class="px-3 py-2"><span class="inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium bg-emerald-50 text-emerald-700">{{ v.status }}</span></td>
                  </tr>
                </tbody>
              </table>
            </div>
            <p v-else class="rounded-xl bg-stone-50 p-3.5 text-[12.5px] text-stone-500">No vaccination records yet.</p>
          </div>
          <!-- Parent / Guardian Accounts (the many-to-many bit) -->
          <div class="border-t border-stone-200 pt-5">
            <div class="flex items-center justify-between mb-1"><p class="text-[13px] font-semibold">Parent / Guardian Accounts</p><span class="text-[11px] text-stone-500">{{ linkedAccountsOf(selectedChild).length }} linked</span></div>
            <p class="text-[11.5px] text-stone-500 mb-3">Anyone linked here can view this child's vaccination record. Only one account can be the Primary Contact.</p>
            <div class="rounded-xl bg-stone-50 p-3 mb-3">
              <p class="text-[11px] font-medium text-stone-500 mb-2">Vaccination reminders notify</p>
              <div class="flex items-center rounded-lg border border-stone-200 p-1 bg-white w-fit">
                <button @click="setNotifyMode(selectedChild, 'primary')" class="rounded-md px-3 py-1.5 text-[12px] font-medium transition-colors" :class="notifyModeOf(selectedChild) === 'primary' ? 'bg-emerald-700 text-white' : 'text-stone-500'">Primary Contact Only</button>
                <button @click="setNotifyMode(selectedChild, 'all')" class="rounded-md px-3 py-1.5 text-[12px] font-medium transition-colors" :class="notifyModeOf(selectedChild) === 'all' ? 'bg-emerald-700 text-white' : 'text-stone-500'">All Linked Accounts</button>
              </div>
            </div>
            <div class="space-y-2">
              <div v-for="a in linkedAccountsOf(selectedChild)" :key="a.rel.id" class="rounded-xl border border-stone-200 p-3.5">
                <div class="flex items-start gap-3">
                  <Avatar :text="a.parent.initials" size="md" />
                  <div class="flex-1 min-w-0">
                    <p class="text-[13px] font-semibold flex items-center gap-1.5 flex-wrap">{{ a.parent.name }} <span v-if="a.rel.isPrimary" class="inline-flex items-center gap-1 text-[10.5px] font-medium text-amber-700 bg-amber-50 rounded-full px-2 py-0.5"><Star :size="10" class="fill-amber-600 text-amber-600" /> Primary Contact</span></p>
                    <p class="text-[12px] text-stone-500 mt-0.5">{{ a.rel.relationship }}</p>
                    <p class="text-[12px] text-stone-500 flex items-center gap-1.5 mt-1"><Phone :size="12" class="text-stone-400" /> {{ a.parent.contact }}</p>
                    <div class="mt-1.5"><StatusBadge :status="a.parent.status" /></div>
                  </div>
                </div>
                <div class="flex items-center gap-2 mt-3 pt-3 border-t border-stone-100">
                  <button @click="viewParent(a.parent)" class="flex items-center gap-1.5 rounded-lg border border-stone-200 px-2.5 py-1.5 text-[11.5px] font-medium hover:bg-stone-50"><Eye :size="13" /> View</button>
                  <button v-if="!a.rel.isPrimary" @click="setPrimary(selectedChild.id, a.rel.id)" class="flex items-center gap-1.5 rounded-lg border border-stone-200 px-2.5 py-1.5 text-[11.5px] font-medium hover:bg-stone-50"><ShieldCheck :size="13" /> Make Primary</button>
                  <button @click="openPicker('transfer', { rel: a.rel })" class="flex items-center gap-1.5 rounded-lg border border-stone-200 px-2.5 py-1.5 text-[11.5px] font-medium hover:bg-stone-50"><ArrowRightLeft :size="13" /> Transfer</button>
                  <div class="flex-1"></div>
                  <template v-if="removeConfirmId === a.rel.id">
                    <span class="text-[11px] text-rose-700 font-medium">Remove?</span>
                    <button @click="confirmRemoveAccount(a.rel)" class="rounded-lg bg-rose-600 text-white px-2.5 py-1.5 text-[11.5px] font-semibold hover:bg-rose-700">Yes</button>
                    <button @click="cancelRemoveAccount" class="rounded-lg border border-stone-200 px-2.5 py-1.5 text-[11.5px] font-medium hover:bg-stone-50">No</button>
                  </template>
                  <button v-else @click="askRemoveAccount(a.rel)" class="flex items-center gap-1.5 rounded-lg border border-stone-200 px-2.5 py-1.5 text-[11.5px] font-medium text-rose-700 hover:bg-rose-50"><Trash2 :size="13" /> Remove</button>
                </div>
              </div>
              <p v-if="linkedAccountsOf(selectedChild).length === 0" class="rounded-xl bg-rose-50 p-3.5 text-[12.5px] text-rose-700 flex items-center gap-2"><Link2 :size="14" /> No parent or guardian linked to this record yet.</p>
            </div>
            <button @click="openPicker('linkParent', { child: selectedChild })" class="mt-3 flex w-full items-center justify-center gap-1.5 rounded-xl border border-dashed border-stone-300 px-3 py-2.5 text-[12.5px] font-medium text-emerald-700 hover:bg-emerald-50"><Link2 :size="14" /> Link Another Parent / Guardian</button>
          </div>
        </div>
      </aside>
    </transition>

    <!-- GENERIC PICKER MODAL -->
    <div v-if="picker.open" class="fixed inset-0 z-50 flex items-center justify-center bg-stone-900/40 px-4">
      <div class="w-full max-w-lg rounded-2xl bg-white shadow-xl max-h-[85vh] flex flex-col">
        <div class="flex items-center justify-between px-6 py-4 border-b border-stone-200">
          <p class="text-[16px] font-semibold">{{ PICKER_CONFIG[picker.mode]?.title }}</p>
          <button @click="closePicker" class="p-1.5 rounded-lg hover:bg-stone-100"><X :size="18" class="text-stone-500" /></button>
        </div>
        <p v-if="picker.mode === 'transfer'" class="px-6 pt-4 text-[12.5px] text-stone-500">
          Replacing <span class="font-semibold text-stone-700">{{ parents.find(p => p.id === picker.rel?.parentId)?.name }}</span> ({{ picker.rel?.relationship }}) with a new guardian. The child's medical history is not affected.
        </p>
        <div class="p-6 pb-3">
          <div class="flex items-center gap-2 rounded-xl border border-stone-200 px-3 py-2.5">
            <Search :size="16" class="text-stone-400 shrink-0" />
            <input v-model="picker.query" :placeholder="PICKER_CONFIG[picker.mode]?.searchPlaceholder" class="flex-1 text-[13px] outline-none placeholder:text-stone-400" />
          </div>
        </div>
        <div class="flex-1 overflow-y-auto px-6 pb-2 space-y-2">
          <button v-for="item in pickerResults" :key="item.id" @click="picker.selected = item" class="flex w-full items-center gap-3 rounded-xl border p-3 text-left transition-colors" :class="picker.selected?.id === item.id ? 'border-emerald-500 bg-emerald-50' : 'border-stone-200 hover:bg-stone-50'">
            <div v-if="picker.mode === 'linkChild'" class="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-50 shrink-0"><Baby :size="17" class="text-emerald-700" /></div>
            <Avatar v-else :text="item.initials" size="md" />
            <div class="flex-1 min-w-0"><p class="text-[12.5px] font-semibold">{{ item.name }}</p><p class="text-[11px] text-stone-500"><template v-if="picker.mode === 'linkChild'">Family No. {{ item.familyNo || "—" }} · Born {{ item.birthDate }}</template><template v-else>Brgy {{ item.barangay || "—" }} · {{ item.contact }}</template></p></div>
            <Check v-if="picker.selected?.id === item.id" :size="17" class="text-emerald-700 shrink-0" />
          </button>
          <p v-if="pickerResults.length === 0" class="text-[12.5px] text-stone-500 py-4 text-center">No matches found.</p>
        </div>
        <div v-if="picker.selected" class="px-6 pb-4 space-y-3 border-t border-stone-200 pt-4">
          <div>
            <label class="text-[11.5px] font-medium text-stone-500">Relationship</label>
            <select v-model="picker.relationship" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500">
              <option value="" disabled>Choose relationship...</option>
              <option v-for="r in linkRelationshipOptions" :key="r">{{ r }}</option>
            </select>
          </div>
          <label v-if="PICKER_CONFIG[picker.mode]?.showPrimary" class="flex items-center gap-2 text-[12.5px] font-medium text-stone-700">
            <input type="checkbox" v-model="picker.isPrimary" class="rounded border-stone-300 text-emerald-600 focus:ring-emerald-500" /> Set as Primary Contact for this child
          </label>
        </div>
        <div v-if="picker.error" class="mx-6 mb-3 rounded-xl bg-rose-50 p-3 text-[12px] text-rose-700">
          {{ picker.error }}
        </div>
        <div class="flex items-center justify-end gap-3 px-6 py-4 border-t border-stone-200">
          <button :disabled="picker.submitting" @click="closePicker" class="rounded-xl px-4 py-2.5 text-[13px] font-medium text-stone-600 hover:bg-stone-50 disabled:opacity-40">Cancel</button>
          <button
            :disabled="!picker.selected || !picker.relationship || picker.submitting"
            @click="confirmPicker"
            class="rounded-xl px-5 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 shadow-sm disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {{ picker.submitting ? 'Saving...' : (picker.mode === 'transfer' ? 'Confirm Transfer' : 'Confirm Link') }}
          </button>
        </div>
      </div>
    </div>

    <!-- REGISTER MODAL — ported from StaffParentRecord.vue / StaffChildRecord.vue
         so Patient Records is the single place both parent and child records
         get created, with the same linking capability those pages had. -->
    <div v-if="registerModal.open" class="fixed inset-0 z-50 flex items-center justify-center bg-stone-900/40 px-4">
      <div class="relative bg-white w-full rounded-3xl shadow-2xl overflow-hidden flex flex-col max-h-[92vh]" :class="registerModal.kind === 'child' ? 'max-w-4xl' : 'max-w-2xl'">
        <div class="p-6 border-b border-stone-200 flex justify-between items-center bg-white sticky top-0 z-10">
          <div>
            <p class="text-[16px] font-semibold">Register {{ registerModal.kind === 'parent' ? 'Parent' : 'New Child' }}</p>
            <p v-if="registerModal.kind === 'child'" class="text-[11px] text-stone-400 uppercase font-bold tracking-wider">Health Intake Form</p>
          </div>
          <button @click="closeRegisterModal" class="p-1.5 rounded-lg hover:bg-stone-100"><X :size="18" class="text-stone-500" /></button>
        </div>

        <!-- ============== PARENT FORM ============== -->
        <div v-if="registerModal.kind === 'parent'" class="p-8 space-y-5 overflow-y-auto">
          <div class="grid grid-cols-3 gap-4">
            <div class="space-y-1">
              <label class="text-[10px] font-bold text-stone-400 uppercase ml-1">Given Name</label>
              <input :value="regParentForm.GivenName" @input="lettersOnlyInput('GivenName')($event)" type="text" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none" />
            </div>
            <div class="space-y-1">
              <label class="text-[10px] font-bold text-stone-400 uppercase ml-1">Middle Name</label>
              <input :value="regParentForm.MiddleName" @input="lettersOnlyInput('MiddleName')($event)" type="text" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none" />
            </div>
            <div class="space-y-1">
              <label class="text-[10px] font-bold text-stone-400 uppercase ml-1">Last Name</label>
              <input :value="regParentForm.LastName" @input="lettersOnlyInput('LastName')($event)" type="text" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none" />
            </div>
          </div>

          <div class="flex items-center justify-between rounded-xl bg-stone-50 border border-stone-200 px-4 py-3">
            <div>
              <p class="text-[13px] font-semibold text-stone-700">Give this guardian a portal login</p>
              <p class="text-[11px] text-stone-400 mt-0.5">Turn off to register a contact-only guardian — no email/password, no account.</p>
            </div>
            <button
              type="button"
              @click="regParentForm.CreateLogin = !regParentForm.CreateLogin"
              class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors shrink-0"
              :class="regParentForm.CreateLogin ? 'bg-emerald-600' : 'bg-stone-300'"
            >
              <span class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" :class="regParentForm.CreateLogin ? 'translate-x-6' : 'translate-x-1'" />
            </button>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div class="space-y-1">
              <label class="text-[10px] font-bold text-stone-400 uppercase ml-1">{{ regParentForm.CreateLogin ? 'Email Address' : 'Email Address (optional)' }}</label>
              <input v-model="regParentForm.Email" type="email" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none" />
            </div>
            <div class="space-y-1">
              <label class="text-[10px] font-bold text-stone-400 uppercase ml-1">Contact No</label>
              <input v-model="regParentForm.ContactNo" v-ph-mobile type="tel" placeholder="09xx xxx xxxx" maxlength="17" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none" />
            </div>
          </div>

          <div class="grid grid-cols-3 gap-4">
            <div class="col-span-1 space-y-1">
              <label class="text-[10px] font-bold text-stone-400 uppercase ml-1">Barangay No</label>
              <select v-model="regParentForm.BarangayNo" :title="BARANGAY_HINT" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none">
                <option value="">Select barangay</option>
                <option v-for="b in barangayChoices()" :key="b.value" :value="b.value">{{ b.label }}</option>
              </select>
            </div>
            <div class="col-span-2 space-y-1">
              <label class="text-[10px] font-bold text-stone-400 uppercase ml-1">Complete Address</label>
              <input v-model="regParentForm.Address" type="text" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none" />
            </div>
          </div>

          <PrivacyConsentCheckbox v-model="regParentForm.PrivacyConsent" />

          <div v-if="regParentForm.CreateLogin" class="space-y-1">
            <p class="text-[11px] text-stone-500 ml-1">A temporary password will be made automatically and shown after you register. Give it to the parent; they'll choose their own password the first time they sign in.</p>
          </div>

          <!-- LINK TO EXISTING CHILD (OPTIONAL) -->
          <div class="pt-2 border-t border-stone-100 space-y-3">
            <div>
              <p class="text-[11px] font-bold text-emerald-700 uppercase tracking-wide">Link to Existing Child <span class="text-stone-400 font-medium normal-case">(optional)</span></p>
              <p class="text-[11px] text-stone-400 mt-0.5">Already have a child registered? Link this new parent account to them now, or skip and link it later.</p>
            </div>
            <div class="grid grid-cols-3 gap-3">
              <div class="col-span-2 space-y-1 relative">
                <input v-model="regChildSearch" type="text" placeholder="Search by child name..." class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none" />
                <div v-if="regChildSearch && regFilteredChildrenForLink.length > 0" class="absolute left-0 right-0 z-10 bg-white border border-stone-200 shadow-xl rounded-xl mt-1 overflow-hidden max-h-48 overflow-y-auto">
                  <div v-for="c in regFilteredChildrenForLink" :key="c.id" @click="regAddLinkedChild(c)" class="px-4 py-2.5 hover:bg-emerald-50 cursor-pointer border-b border-stone-50 last:border-none">
                    <p class="text-sm font-bold text-stone-700">{{ c.name }}</p>
                    <p class="text-[10px] text-stone-400">Brgy {{ c.barangay ?? 'N/A' }}</p>
                  </div>
                </div>
              </div>
              <select v-model="regLinkRole" class="px-3 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm focus:border-emerald-500 outline-none">
                <option v-for="r in regLinkRoleOptions" :key="r" :value="r">{{ r }}</option>
              </select>
            </div>
            <div v-if="regLinkedChildren.length > 0" class="flex flex-wrap gap-2">
              <div v-for="lc in regLinkedChildren" :key="lc.childID" class="flex items-center gap-2 bg-emerald-50 border border-emerald-100 rounded-lg px-3 py-1.5">
                <span class="text-xs font-bold text-emerald-800">{{ lc.name }}</span>
                <span class="text-[10px] text-emerald-600 uppercase font-bold">{{ lc.role }}</span>
                <X @click="regRemoveLinkedChild(lc.childID)" class="w-3 h-3 text-emerald-400 hover:text-rose-500 cursor-pointer" />
              </div>
            </div>
          </div>

          <div v-if="error" class="rounded-xl bg-rose-50 p-3 text-[12px] text-rose-700">{{ error }}</div>
        </div>

        <!-- ============== CHILD FORM (Step 1/2/3) ============== -->
        <div v-else class="p-8 space-y-8 overflow-y-auto">
          <!-- STEP 1: PARENT INFORMATION (LINK OR MANUAL) -->
          <div class="p-6 bg-emerald-50 rounded-2xl border border-emerald-100 space-y-4">
            <div class="flex justify-between items-end">
              <label class="text-[10px] font-bold text-emerald-700 uppercase tracking-widest">Step 1: Parent/Guardian Information</label>
              <span class="text-[9px] text-emerald-600 bg-white px-2 py-1 rounded border border-emerald-100">Search to link existing account</span>
            </div>
            <div class="grid grid-cols-2 gap-4">
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-emerald-800 ml-1">Assign To Role</label>
                <select v-model="regSelectedRole" class="w-full px-4 py-2.5 bg-white border border-emerald-200 rounded-xl text-sm outline-none">
                  <option value="Mother">Mother</option>
                  <option value="Father">Father</option>
                  <option value="Guardian">Guardian</option>
                </select>
              </div>
              <div class="space-y-1 relative">
                <label class="text-[11px] font-bold text-emerald-800 ml-1">Type Name or Search Email</label>
                <input v-model="regParentSearch" @input="regHandleManualNameEntry" type="text" placeholder="Type name manually or search account..." class="w-full px-4 py-2.5 bg-white border border-emerald-200 rounded-xl text-sm outline-none" />
                <div v-if="regFilteredParents.length > 0" class="absolute left-0 right-0 z-[70] bg-white border border-stone-200 shadow-xl rounded-xl mt-1 overflow-hidden">
                  <div v-for="p in regFilteredParents" :key="p.id" @click="regLinkExistingParent(p)" class="px-4 py-3 hover:bg-emerald-50 cursor-pointer border-b border-stone-50 last:border-none">
                    <p class="text-sm font-bold text-stone-700">{{ p.name }}</p>
                    <p class="text-[10px] text-emerald-600 font-bold">LINK ACCOUNT: {{ p.email }}</p>
                  </div>
                </div>
              </div>
            </div>
            <div class="grid grid-cols-3 gap-3 pt-2">
              <div v-for="role in ['Mother', 'Father', 'Guardian']" :key="role" class="p-3 rounded-xl border transition-all" :class="regChildForm[role+'Name'] ? 'bg-white border-emerald-200 shadow-sm' : 'bg-emerald-50/50 border-dashed border-emerald-200'">
                <div class="flex justify-between items-start mb-1">
                  <span class="text-[9px] font-black uppercase text-emerald-800">{{ role }}</span>
                  <button v-if="regChildForm[role+'Name']" @click="regClearParentRole(role)" class="text-emerald-300 hover:text-red-400"><X class="w-3 h-3"/></button>
                </div>
                <p class="text-xs font-bold text-stone-700 truncate">{{ regChildForm[role+'Name'] || 'Empty' }}</p>
                <p v-if="regChildForm[role+'Email']" class="text-[8px] text-emerald-500 font-bold uppercase mt-1">Linked: {{ regChildForm[role+'Email'] }}</p>
                <p v-else-if="regChildForm[role+'Name']" class="text-[8px] text-stone-400 font-medium uppercase mt-1">Manual Entry</p>
              </div>
            </div>
          </div>

          <!-- STEP 2: CHILD IDENTITY -->
          <div class="space-y-4">
            <label class="text-[10px] font-bold text-stone-400 uppercase tracking-widest">Step 2: Child Identity</label>
            <div class="grid grid-cols-3 gap-4">
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">First Name</label>
                <input v-model="regChildForm.FirstName" type="text" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none focus:border-emerald-500" />
              </div>
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Middle Name</label>
                <input v-model="regChildForm.MiddleName" type="text" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none focus:border-emerald-500" />
              </div>
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Last Name</label>
                <input v-model="regChildForm.LastName" type="text" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none focus:border-emerald-500" />
              </div>
            </div>
            <div class="grid grid-cols-3 gap-4">
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Birth Date</label>
                <input v-model="regChildForm.BirthDate" type="date" :max="todayISO" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none" />
              </div>
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Sex</label>
                <select v-model="regChildForm.Sex" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none">
                  <option value="Male">Male</option>
                  <option value="Female">Female</option>
                </select>
              </div>
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Place of Birth</label>
                <input v-model="regChildForm.PlaceOfBirth" type="text" placeholder="City/Hospital" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none" />
              </div>
            </div>
          </div>

          <!-- STEP 3: PHYSICAL METRICS & ADDRESS -->
          <div class="space-y-4">
            <label class="text-[10px] font-bold text-stone-400 uppercase tracking-widest">Step 3: Medical & Location</label>
            <div class="grid grid-cols-4 gap-4">
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Birth Weight (kg)</label>
                <input v-model="regChildForm.BirthWeight" v-digits.decimal type="number" step="0.01" min="0.5" max="7" placeholder="e.g. 3.2" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none" />
              </div>
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Birth Height (cm)</label>
                <input v-model="regChildForm.BirthHeight" v-digits.decimal type="number" step="0.1" min="25" max="65" placeholder="e.g. 50" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none" />
              </div>
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Barangay</label>
                <select v-model="regChildForm.Barangay" :title="BARANGAY_HINT" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none">
                  <option value="">Select barangay</option>
                  <option v-for="b in barangayChoices()" :key="b.value" :value="b.value">{{ b.label }}</option>
                </select>
              </div>
              <div class="space-y-1">
                <label class="text-[11px] font-bold text-stone-500 ml-1">Family No.</label>
                <input v-model="regChildForm.FamilyNo" type="text" class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none" />
              </div>
            </div>
            <div class="space-y-1">
              <label class="text-[11px] font-bold text-stone-500 ml-1">Full Home Address</label>
              <input v-model="regChildForm.Address" type="text" placeholder="Street Name, Building, etc." class="w-full px-4 py-2.5 bg-stone-50 border border-stone-200 rounded-xl text-sm outline-none" />
            </div>
          </div>

          <!-- STEP 4: PRIOR VACCINATION HISTORY (OPTIONAL) -->
          <div class="space-y-4">
            <label class="text-[10px] font-bold text-stone-400 uppercase tracking-widest">Step 4: Prior Vaccination History</label>
            <div class="flex items-center justify-between rounded-xl bg-stone-50 border border-stone-200 px-4 py-3">
              <div>
                <p class="text-[13px] font-semibold text-stone-700">This child already has vaccination history</p>
                <p class="text-[11px] text-stone-400 mt-0.5">Based on the newborn screening or prior clinic papers the parent brought in — e.g. BCG given at birth. Turn on to record which vaccines/doses were already administered before this registration.</p>
              </div>
              <button
                type="button"
                @click="regHasPriorVaccinations = !regHasPriorVaccinations"
                class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors shrink-0"
                :class="regHasPriorVaccinations ? 'bg-emerald-600' : 'bg-stone-300'"
              >
                <span class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform" :class="regHasPriorVaccinations ? 'translate-x-6' : 'translate-x-1'" />
              </button>
            </div>

            <div v-if="regHasPriorVaccinations" class="space-y-3">
              <div v-for="(row, idx) in regPriorVaccinationRows" :key="idx" class="rounded-xl border border-stone-200 p-3.5 space-y-3">
                <div class="flex items-center justify-between">
                  <p class="text-[11.5px] font-semibold text-stone-500">Vaccination {{ idx + 1 }}</p>
                  <button v-if="regPriorVaccinationRows.length > 1" @click="removeRegPriorVaccinationRow(idx)" class="flex items-center gap-1 text-[11.5px] font-medium text-rose-700 hover:bg-rose-50 rounded-lg px-2 py-1"><Trash2 :size="12" /> Remove</button>
                </div>
                <div class="grid grid-cols-2 gap-3">
                  <select v-model="row.vaccineID" @change="row.doseNumber = ''" class="col-span-2 rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500">
                    <option value="" disabled>Select Vaccine</option>
                    <option v-for="v in vaccineCatalog" :key="v.vaccineID" :value="v.vaccineID">{{ v.vaccineName }}</option>
                  </select>
                  <div>
                    <label class="text-[11px] font-medium text-stone-500">Dose Number</label>
                    <select v-model.number="row.doseNumber" :disabled="!row.vaccineID" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500 disabled:bg-stone-100 disabled:text-stone-400">
                      <option value="" disabled>{{ row.vaccineID ? 'Select Dose' : 'Select vaccine first' }}</option>
                      <option v-for="d in doseOptionsForVaccine(row.vaccineID)" :key="d" :value="d">Dose {{ d }}</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-[11px] font-medium text-stone-500">Vaccination Date</label>
                    <input v-model="row.vaccinationDate" type="date" :max="todayISO" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500" />
                  </div>
                </div>
              </div>
              <button @click="addRegPriorVaccinationRow" class="flex w-full items-center justify-center gap-1.5 rounded-xl border border-dashed border-stone-300 px-3 py-2.5 text-[12.5px] font-medium text-emerald-700 hover:bg-emerald-50"><Syringe :size="14" /> Add Another Vaccination</button>
            </div>
          </div>

          <div v-if="error" class="rounded-xl bg-rose-50 p-3 text-[12px] text-rose-700">{{ error }}</div>
        </div>

        <div class="flex items-center justify-end gap-3 px-6 py-4 border-t border-stone-200 bg-stone-50 sticky bottom-0">
          <button @click="closeRegisterModal" class="rounded-xl px-4 py-2.5 text-[13px] font-medium text-stone-600 hover:bg-stone-50">Cancel</button>
          <button
            @click="registerModal.kind === 'parent' ? submitParentRegister() : submitChildRegister()"
            :disabled="registerSubmitting || (registerModal.kind === 'parent' && !regParentForm.PrivacyConsent)"
            class="rounded-xl px-5 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {{ registerSubmitting ? 'Saving...' : `Register ${registerModal.kind === 'parent' ? 'Parent' : 'Child'}` }}
          </button>
        </div>
      </div>
    </div>

    <!-- GENERIC EDIT MODAL -->
    <div v-if="editModal.open" class="fixed inset-0 z-50 flex items-center justify-center bg-stone-900/40 px-4">
      <div class="w-full max-w-lg rounded-2xl bg-white shadow-xl max-h-[88vh] overflow-y-auto">
        <div class="flex items-center justify-between px-6 py-4 border-b border-stone-200">
          <p class="text-[16px] font-semibold">Edit {{ editModal.kind === 'parent' ? 'Parent' : 'Child' }}</p>
          <button @click="editModal.open = false" class="p-1.5 rounded-lg hover:bg-stone-100"><X :size="18" class="text-stone-500" /></button>
        </div>
        <div class="p-6 space-y-4">
          <div v-for="row in fieldRows(EDIT_FIELDS[editModal.kind])" :key="row.map(f => f.key).join('-')" :class="row.length > 1 ? `grid grid-cols-${row.length} gap-3` : ''">
            <div v-for="f in row" :key="f.key">
              <label class="text-[11.5px] font-medium text-stone-500">{{ f.label }}</label>
              <select v-if="f.type === 'select'" v-model="editModal.item[f.key]" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500">
                <option v-for="o in f.options" :key="o">{{ o }}</option>
              </select>
              <select v-else-if="f.type === 'barangay'" v-model="editModal.item[f.key]" :title="BARANGAY_HINT" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500">
                <option value="">Select barangay</option>
                <option v-for="b in barangayChoices(editModal.item[f.key])" :key="b.value" :value="b.value">{{ b.label }}</option>
              </select>
              <input v-else-if="f.type === 'phone'" v-model="editModal.item[f.key]" v-ph-mobile type="tel" placeholder="+63 9XX XXX XXXX" maxlength="17" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500" />
              <input v-else v-model="editModal.item[f.key]" :type="f.type || 'text'" :step="f.type === 'number' ? '0.01' : undefined" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500" />
            </div>
          </div>
          <p v-if="editModal.kind === 'child'" class="text-[11px] text-stone-400">
            Changing the birth date moves the child's upcoming vaccine dates. The parent is notified of any change.
          </p>
          <div v-if="error" class="rounded-xl bg-rose-50 p-3 text-[12px] text-rose-700">{{ error }}</div>
        </div>
        <div class="flex items-center justify-end gap-3 px-6 py-4 border-t border-stone-200">
          <button @click="editModal.open = false" class="rounded-xl px-4 py-2.5 text-[13px] font-medium text-stone-600 hover:bg-stone-50">Cancel</button>
          <button @click="submitEdit" class="rounded-xl px-5 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 shadow-sm">Save Changes</button>
        </div>
      </div>
    </div>
    <!-- HISTORICAL VACCINATION RECORDS MODAL -->
    <div v-if="historicalModal.open" class="fixed inset-0 z-50 flex items-center justify-center bg-stone-900/40 px-4">
      <div class="w-full max-w-xl rounded-2xl bg-white shadow-xl max-h-[88vh] overflow-y-auto">
        <div class="flex items-center justify-between px-6 py-4 border-b border-stone-200">
          <div>
            <p class="text-[16px] font-semibold">Historical Vaccination Records</p>
            <p class="text-[11.5px] text-stone-500 mt-0.5" v-if="selectedChild">{{ selectedChild.name }} · {{ selectedChild.id }}</p>
          </div>
          <button @click="closeHistoricalModal" class="p-1.5 rounded-lg hover:bg-stone-100"><X :size="18" class="text-stone-500" /></button>
        </div>

        <div v-if="!historicalModal.success" class="p-6 space-y-4">
          <p class="text-[12px] text-stone-500 -mt-1">Record vaccinations this child received before being managed in Aruga.</p>

          <div v-for="(row, idx) in historicalModal.rows" :key="idx" class="rounded-xl border border-stone-200 p-3.5 space-y-3">
            <div class="flex items-center justify-between">
              <p class="text-[11.5px] font-semibold text-stone-500">Vaccination {{ idx + 1 }}</p>
              <button v-if="historicalModal.rows.length > 1" @click="removeHistoricalRow(idx)" class="flex items-center gap-1 text-[11.5px] font-medium text-rose-700 hover:bg-rose-50 rounded-lg px-2 py-1"><Trash2 :size="12" /> Remove</button>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <select v-model="row.vaccineID" @change="row.doseNumber = ''" class="col-span-2 rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500">
                <option value="" disabled>Select Vaccine</option>
                <option v-for="v in vaccineCatalog" :key="v.vaccineID" :value="v.vaccineID">{{ v.vaccineName }}</option>
              </select>
              <div>
                <label class="text-[11px] font-medium text-stone-500">Dose Number</label>
                <select v-model.number="row.doseNumber" :disabled="!row.vaccineID" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500 disabled:bg-stone-100 disabled:text-stone-400">
                  <option value="" disabled>{{ row.vaccineID ? 'Select Dose' : 'Select vaccine first' }}</option>
                  <option v-for="d in doseOptionsForVaccine(row.vaccineID)" :key="d" :value="d">Dose {{ d }}</option>
                </select>
              </div>
              <div>
                <label class="text-[11px] font-medium text-stone-500">Vaccination Date</label>
                <input v-model="row.vaccinationDate" type="date" :max="todayISO" class="mt-1 w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] outline-none focus:border-emerald-500" />
              </div>
            </div>
          </div>

          <button @click="addHistoricalRow" class="flex w-full items-center justify-center gap-1.5 rounded-xl border border-dashed border-stone-300 px-3 py-2.5 text-[12.5px] font-medium text-emerald-700 hover:bg-emerald-50"><Syringe :size="14" /> Add Another Vaccination</button>

          <div v-if="historicalModal.error" class="rounded-xl bg-rose-50 p-3 text-[12px] text-rose-700">{{ historicalModal.error }}</div>
        </div>

        <div v-else class="p-6 text-center py-10">
          <div class="flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-50 mx-auto mb-4"><Check :size="26" class="text-emerald-700" /></div>
          <p class="text-[15px] font-semibold mb-1">Historical vaccination records saved successfully.</p>
          <p class="text-[13px] text-stone-500 mb-5">The child's vaccination history has been updated.</p>
          <button @click="closeHistoricalModal" class="rounded-xl px-5 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800">Done</button>
        </div>

        <div v-if="!historicalModal.success" class="flex items-center justify-end gap-3 px-6 py-4 border-t border-stone-200">
          <button @click="closeHistoricalModal" class="rounded-xl px-4 py-2.5 text-[13px] font-medium text-stone-600 hover:bg-stone-50">Cancel</button>
          <button :disabled="historicalModal.submitting" @click="submitHistoricalVaccinations" class="rounded-xl px-5 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800 shadow-sm disabled:opacity-50">
            {{ historicalModal.submitting ? "Saving..." : "Save Historical Vaccinations" }}
          </button>
        </div>
      </div>
    </div>

    <!-- ======================= PARENT LOGIN: CONFIRM / RESULT ======================= -->
    <div v-if="accountDialog" class="fixed inset-0 z-[60] flex items-center justify-center bg-stone-900/40 px-4" @click.self="!accountDialog.busy && (accountDialog = null)">
      <div class="w-full max-w-md rounded-2xl bg-white shadow-xl">
        <div class="px-6 pt-6 pb-4">
          <p class="text-[15px] font-semibold">{{ accountDialog.result ? "Done" : accountDialog.title }}</p>
          <template v-if="!accountDialog.result">
            <p class="mt-2 text-[13px] text-stone-600">{{ accountDialog.message }}</p>
            <p v-if="accountDialog.error" class="mt-3 rounded-xl bg-rose-50 p-3 text-[12px] text-rose-700">{{ accountDialog.error }}</p>
          </template>
          <template v-else>
            <div v-if="accountDialog.result.password" class="mt-3 rounded-xl bg-stone-50 border border-stone-200 p-4 text-center">
              <p class="text-[11px] uppercase tracking-wide text-stone-500">Temporary password</p>
              <p class="mt-1 font-mono text-[18px] font-semibold tracking-wider select-all">{{ accountDialog.result.password }}</p>
            </div>
            <p class="mt-3 text-[13px] text-stone-600">{{ accountDialog.result.text }}</p>
          </template>
        </div>
        <div class="flex justify-end gap-3 px-6 py-4 border-t border-stone-200">
          <template v-if="!accountDialog.result">
            <button :disabled="accountDialog.busy" @click="accountDialog = null" class="rounded-xl px-4 py-2.5 text-[13px] font-medium text-stone-600 hover:bg-stone-50">Cancel</button>
            <button :disabled="accountDialog.busy" @click="runAccountDialog" class="rounded-xl px-5 py-2.5 text-[13px] font-semibold text-white disabled:opacity-50" :class="accountDialog.danger ? 'bg-amber-600 hover:bg-amber-700' : 'bg-emerald-700 hover:bg-emerald-800'">
              {{ accountDialog.busy ? "Please wait..." : accountDialog.confirmLabel }}
            </button>
          </template>
          <button v-else @click="accountDialog = null" class="rounded-xl px-5 py-2.5 text-[13px] font-semibold text-white bg-emerald-700 hover:bg-emerald-800">Close</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.slide-enter-active, .slide-leave-active { transition: transform 0.25s ease; }
.slide-enter-from, .slide-leave-to { transform: translateX(100%); }
</style>