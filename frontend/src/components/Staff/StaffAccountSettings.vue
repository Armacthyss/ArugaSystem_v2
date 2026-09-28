<!--
  StaffAccountSettings.vue  —  /staff/settings (gear icon in StaffTopbar)
  - Profile: name, username and role are read-only (only the System
    Administrator can change those, in User Management). Contact details
    (email, mobile number, address) can be edited here: PUT /api/Users/{id}.
  - Change password: PATCH /api/Users/{userId}/change-password, with the
    same rule as everywhere else in the app (8+ chars, upper, lower, digit,
    special char).
-->
<template>
  <div class="flex min-h-screen w-full bg-stone-50 text-stone-900" style="font-family: 'Inter','Segoe UI',sans-serif;">
    <StaffSidebar />

    <main class="flex-1 min-w-0">
      <StaffTopbar title="Account Settings" breadcrumb="Aruga / Account Settings" />

      <div class="px-8 py-6 max-w-2xl space-y-6">
        <!-- Profile -->
        <div class="rounded-2xl border border-stone-200 bg-white p-6 shadow-sm">
          <p class="text-[14px] font-semibold mb-4">Profile</p>
          <div class="flex items-center gap-4 mb-5">
            <div class="flex h-14 w-14 items-center justify-center rounded-full text-white text-base font-semibold bg-emerald-700 shrink-0">
              {{ staffInitials }}
            </div>
            <div class="min-w-0">
              <p class="text-[15px] font-semibold truncate">{{ staffName }}</p>
              <p class="text-[12.5px] text-stone-500 truncate">{{ staffRole }}</p>
            </div>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <p class="text-[11px] font-semibold uppercase tracking-wide text-stone-400 mb-1">Username</p>
              <p class="text-[13px] text-stone-700">{{ profile.username || '—' }}</p>
            </div>
            <div>
              <p class="text-[11px] font-semibold uppercase tracking-wide text-stone-400 mb-1">Role</p>
              <p class="text-[13px] text-stone-700">{{ staffRole }}</p>
            </div>
          </div>

          <form @submit.prevent="saveContact" class="mt-6 space-y-4 border-t border-stone-100 pt-5">
            <p class="text-[12.5px] font-semibold text-stone-700">Contact Information</p>
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-[12px] font-medium text-stone-600 mb-1">Email</label>
                <input v-model="contact.email" type="email" class="w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] focus:outline-none focus:ring-2 focus:ring-emerald-600/30 focus:border-emerald-600" />
              </div>
              <div>
                <label class="block text-[12px] font-medium text-stone-600 mb-1">Mobile Number</label>
                <input v-model="contact.contactNo" v-digits type="tel" placeholder="09XXXXXXXXX" class="w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] focus:outline-none focus:ring-2 focus:ring-emerald-600/30 focus:border-emerald-600" />
              </div>
              <div class="col-span-2">
                <label class="block text-[12px] font-medium text-stone-600 mb-1">Address</label>
                <input v-model="contact.address" type="text" class="w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] focus:outline-none focus:ring-2 focus:ring-emerald-600/30 focus:border-emerald-600" />
              </div>
            </div>
            <p class="text-[11.5px] text-stone-400">Your name and role can only be changed by the System Administrator.</p>
            <p v-if="contactError" class="text-[12.5px] text-rose-700">{{ contactError }}</p>
            <p v-if="contactSaved" class="text-[12.5px] text-emerald-700">{{ contactSaved }}</p>
            <button
              type="submit"
              :disabled="savingContact"
              class="rounded-xl bg-emerald-700 px-4 py-2.5 text-[13px] font-medium text-white hover:bg-emerald-800 disabled:opacity-40"
            >
              {{ savingContact ? 'Saving…' : 'Save Contact Info' }}
            </button>
          </form>
        </div>

        <!-- Change password -->
        <div class="rounded-2xl border border-stone-200 bg-white p-6 shadow-sm">
          <p class="text-[14px] font-semibold mb-4">Change Password</p>

          <form @submit.prevent="submitPasswordChange" class="space-y-4">
            <div>
              <label class="block text-[12px] font-medium text-stone-600 mb-1">Current Password</label>
              <input
                v-model="form.currentPassword"
                type="password"
                required
                autocomplete="current-password"
                class="w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] focus:outline-none focus:ring-2 focus:ring-emerald-600/30 focus:border-emerald-600"
              />
            </div>
            <div>
              <label class="block text-[12px] font-medium text-stone-600 mb-1">New Password</label>
              <input
                v-model="form.newPassword"
                type="password"
                required
                autocomplete="new-password"
                class="w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] focus:outline-none focus:ring-2 focus:ring-emerald-600/30 focus:border-emerald-600"
              />
            </div>
            <div>
              <label class="block text-[12px] font-medium text-stone-600 mb-1">Confirm New Password</label>
              <input
                v-model="form.confirmPassword"
                type="password"
                required
                autocomplete="new-password"
                class="w-full rounded-xl border border-stone-200 px-3 py-2.5 text-[13px] focus:outline-none focus:ring-2 focus:ring-emerald-600/30 focus:border-emerald-600"
              />
            </div>

            <ul class="grid grid-cols-2 gap-x-4 gap-y-1 text-[12px]">
              <li :class="checklist.length ? 'text-emerald-700' : 'text-stone-400'">✓ 8+ characters</li>
              <li :class="checklist.upper ? 'text-emerald-700' : 'text-stone-400'">✓ Uppercase letter</li>
              <li :class="checklist.lower ? 'text-emerald-700' : 'text-stone-400'">✓ Lowercase letter</li>
              <li :class="checklist.digit ? 'text-emerald-700' : 'text-stone-400'">✓ Number</li>
              <li :class="checklist.special ? 'text-emerald-700' : 'text-stone-400'">✓ Special character</li>
              <li :class="checklist.match ? 'text-emerald-700' : 'text-stone-400'">✓ Passwords match</li>
            </ul>

            <p v-if="errorMessage" class="text-[12.5px] text-rose-700">{{ errorMessage }}</p>
            <p v-if="successMessage" class="text-[12.5px] text-emerald-700">{{ successMessage }}</p>

            <button
              type="submit"
              :disabled="submitting || !allValid"
              class="rounded-xl bg-emerald-700 px-4 py-2.5 text-[13px] font-medium text-white hover:bg-emerald-800 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {{ submitting ? "Saving…" : "Update Password" }}
            </button>
          </form>
        </div>
      </div>
    </main>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import axios from "axios";
import StaffSidebar from "./StaffSidebar.vue";
import StaffTopbar from "./StaffTopbar.vue";
import { getAccount } from "@/utils/auth";
import { API_BASE } from "@/utils/format";

const account = getAccount() || {};
const user = account.user || {};
const staffUserId = user.UserID || user.userId || user.id || user.Id;

// Filled from GET /api/Users/{id}; the session copy is only a fallback
const profile = ref({});

const staffName = computed(() => {
  const p = profile.value;
  return [p.firstName || user.FirstName, p.lastName || user.LastName].filter(Boolean).join(" ") || "Staff";
});
const staffRole = computed(() => {
  const position = profile.value.position || user.UserType;
  return position === "Nurse" || position === "Staff" ? "Staff / Nurse" : position || "Staff / Nurse";
});
const staffInitials = computed(() =>
  staffName.value.split(" ").filter(Boolean).slice(0, 2).map(w => w[0]?.toUpperCase()).join("") || "S"
);

// ── Contact info ─────────────────────────────────────────────
const contact = reactive({ email: "", contactNo: "", address: "" });
const savingContact = ref(false);
const contactError = ref("");
const contactSaved = ref("");

async function loadProfile() {
  if (!staffUserId) return;
  try {
    const res = await axios.get(`${API_BASE}/Users/${staffUserId}`);
    profile.value = res.data;
    Object.assign(contact, {
      email: res.data.email || "",
      contactNo: res.data.contactNo || "",
      address: res.data.address || "",
    });
  } catch (error) {
    console.error("Load profile error:", error);
  }
}

async function saveContact() {
  contactError.value = "";
  contactSaved.value = "";
  if (contact.contactNo && !/^(09|\+639)\d{9}$/.test(contact.contactNo.replace(/[\s-]/g, ""))) {
    contactError.value = "Enter a mobile number like 09171234567.";
    return;
  }
  savingContact.value = true;
  try {
    const res = await axios.put(`${API_BASE}/Users/${staffUserId}`, { ...contact });
    profile.value = { ...profile.value, ...res.data };
    contactSaved.value = "Contact information saved.";
  } catch (error) {
    contactError.value = error.response?.data?.message || "Could not save your contact information.";
  } finally {
    savingContact.value = false;
  }
}

// ── Password ─────────────────────────────────────────────────
const form = reactive({
  currentPassword: "",
  newPassword: "",
  confirmPassword: "",
});

const checklist = computed(() => ({
  length: form.newPassword.length >= 8,
  upper: /[A-Z]/.test(form.newPassword),
  lower: /[a-z]/.test(form.newPassword),
  digit: /\d/.test(form.newPassword),
  special: /[^A-Za-z0-9]/.test(form.newPassword),
  match: form.newPassword.length > 0 && form.newPassword === form.confirmPassword,
}));

const allValid = computed(() => Object.values(checklist.value).every(Boolean) && form.currentPassword.length > 0);

const submitting = ref(false);
const errorMessage = ref("");
const successMessage = ref("");

const submitPasswordChange = async () => {
  errorMessage.value = "";
  successMessage.value = "";

  if (!staffUserId) {
    errorMessage.value = "Couldn't determine your account ID — please log out and back in, then try again.";
    return;
  }
  if (!allValid.value) {
    errorMessage.value = "Please meet all password requirements before submitting.";
    return;
  }

  submitting.value = true;
  try {
    await axios.patch(`${API_BASE}/Users/${staffUserId}/change-password`, {
      currentPassword: form.currentPassword,
      newPassword: form.newPassword,
    });

    successMessage.value = "Password updated successfully.";
    form.currentPassword = "";
    form.newPassword = "";
    form.confirmPassword = "";
  } catch (error) {
    console.error("Change password error:", error);
    errorMessage.value = error.response?.data?.message || "Something went wrong. Please try again.";
  } finally {
    submitting.value = false;
  }
};

onMounted(loadProfile);
</script>
