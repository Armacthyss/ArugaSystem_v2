import { createRouter, createWebHistory } from 'vue-router'
import { isLoggedIn, getRole, logout } from '@/utils/auth'

// ── Login / Change Password ─────────────────────────────────

import ChangePassword from '@/components/Login/ChangePassword.vue'
import Login from '@/components/Login/Login.vue'
import ForgotPassword from '@/components/Login/ForgotPassword.vue'

// ── Parent ───────────────────────────────────────────────────

import ParentOverview from '@/components/ParentViews/Views/Parentoverview.vue'
import ParentCheckin from '@/components/ParentViews/Views/Checkin.vue'
import ParentSchedule from '@/components/ParentViews/Views/Scheduled.vue'
import ParentRecords from '@/components/ParentViews/Views/Record.vue'

// ── Staff pages (formerly split across Admin/ and Staff/) ────

import StaffCalendar from '@/components/Staff/StaffCalendar.vue'
import StaffReport from '@/components/Staff/StaffReport.vue'
import StaffVaccineInventory from '@/components/Staff/StaffVaccineInventory.vue'
import StaffQueueManagement from '@/components/Staff/StaffQueueManagement.vue'
import StaffCheckinQR from '@/components/Staff/StaffCheckinQR.vue'

// ── System Admin ────────────────────────────────────────────

import SystemAdminHomepage from '@/components/SystemAdmin/SystemAdmin.vue'
import SystemAdminUserManagement from '@/components/SystemAdmin/SysAd-UserManagement.vue'
import SystemAdminPatient from '@/components/SystemAdmin/SysAd-Patient.vue'
import SystemAdminVaccine from '@/components/SystemAdmin/SysAd-Vaccine.vue'
import SystemAdminInventory from '@/components/SystemAdmin/SysAd-Inventory.vue'
import SystemAdminNotification from '@/components/SystemAdmin/SysAd-Notification.vue'
import SystemAdminReports from '@/components/SystemAdmin/SysAd-Reports.vue'
import SystemAuditlogs from '@/components/SystemAdmin/SysAd-Auditlogs.vue'
import TestAPI from '@/components/SystemAdmin/TestAPI.vue'
import VaccineSchedule from '@/components/SystemAdmin/VaccineSchedule.vue'
import SystemOperatingHours from '@/components/SystemAdmin/SysAd-Operating_hours.vue'

// ── Super Admin (the development team) ─────────────────────
import SuperAdminHome from '@/components/SuperAdmin/SuperAdminHome.vue'
import SuperAdminAccounts from '@/components/SuperAdmin/SuperAdminAccounts.vue'

// ── Staff ────────────────────────────────────────────────────

import StaffDashboard from '@/components/Staff/StaffDashboard.vue'
import StaffAccountSettings from '@/components/Staff/StaffAccountSettings.vue'
import StaffPatientRecords from '@/components/Staff/StaffPatientRecords.vue'
import StaffVaccineSchedule from '@/components/Staff/StaffVaccineSchedule.vue'
import StaffVaccinationVisit from '@/components/Staff/StaffVaccinationVisit.vue'
import StaffVaccinationRecords from '@/components/Staff/StaffVaccinationRecords.vue'
import VaccinationCard from '@/components/Shared/VaccinationCard.vue'


const routes = [
  // ── Login ──────────────────────────────────────────────────

  {
    path: '/',
    name: 'Login',
    component: Login
  },

  {
    path: '/forgot-password',
    name: 'ForgotPassword',
    component: ForgotPassword
  },


  // ── Parent ─────────────────────────────────────────────────

  {
    path: '/ParentOverview',
    component: ParentOverview,
    meta: {
      requiresAuth: true,
      role: 'Parent'
    }
  },

  {
    path: '/ParentCheckin',
    component: ParentCheckin,
    meta: {
      requiresAuth: true,
      role: 'Parent'
    }
  },

  {
    path: '/ParentRecords',
    component: ParentRecords,
    meta: {
      requiresAuth: true,
      role: 'Parent'
    }
  },

  {
    path: '/ParentSchedule',
    component: ParentSchedule,
    meta: {
      requiresAuth: true,
      role: 'Parent'
    }
  },


  // ── Staff / Nurse ──────────────────────────────────────────
  // Leveriza has three user levels: Admin / Doctor, Staff / Nurse and
  // Parent. The Nurse checks families in, calls them into the one
  // vaccination room (Call Next) and records the vaccines.

  // Read-only list of the doses this Nurse gave. Declared BEFORE the
  // dynamic /staff/vaccination/:queueId route below.
  {
    path: '/staff/vaccination-records',
    component: StaffVaccinationRecords,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  // Recording flow — reached from the Dashboard's Vaccination Room card
  // ("Start Vaccinating"): /staff/vaccination/:queueId?child=<childID>
  {
    path: '/staff/vaccination/:queueId',
    component: StaffVaccinationVisit,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  // Old links from the separate Doctor/Nurse portal (removed)
  { path: '/healthcare/vaccination/:queueId', redirect: to => `/staff/vaccination/${to.params.queueId}` },
  { path: '/healthcare/vaccination-records', redirect: '/staff/vaccination-records' },
  { path: '/healthcare/:rest(.*)*', redirect: '/staff/dashboard' },
  { path: '/doctor/:rest(.*)*', redirect: '/staff/dashboard' },

  {
    path: '/staff/dashboard',
    component: StaffDashboard,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  {
    path: '/staff/patient-records',
    component: StaffPatientRecords,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  {
    path: '/staff/vaccine-schedule',
    component: StaffVaccineSchedule,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },


  // ── Formerly "Old Admin routes" ────────────────────────────
  // Renamed from /admin/* to /staff/* — these were always Staff*.vue
  // components gated by role: 'Staff' (not SystemAdmin), so the /admin/
  // prefix was just leftover naming from before AdminHomepage.vue was
  // retired (see note above), not an actual permission boundary.
  // /AdminHome removed: AdminHomepage.vue is retired — its real
  // functionality (GetRooms/AssignRoom/CompleteSession) now lives in
  // StaffDashboard.vue at /staff/dashboard.

  {
    path: '/staff/calendar',
    component: StaffCalendar,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  // Children and Parents are tabs on Patient Records now
  { path: '/staff/children', redirect: '/staff/patient-records?tab=children' },

  { path: '/staff/doctor-staff', redirect: '/staff/dashboard' },

  { path: '/staff/parents', redirect: '/staff/patient-records?tab=parents' },

  {
    path: '/staff/reports',
    component: StaffReport,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  {
    path: '/staff/vaccine-inventory',
    component: StaffVaccineInventory,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  {
    path: '/staff/checkin-qr',
    component: StaffCheckinQR,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  {
    path: '/staff/queue-management',
    component: StaffQueueManagement,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },

  {
      path: '/staff/settings',
    component: StaffAccountSettings,
    meta: {
      requiresAuth: true,
      role: 'Staff'
    }
  },


  // ── Admin / Doctor ─────────────────────────────────────────
  // At /admin/* (was /system-admin/*). The role is still 'SystemAdmin'.

  {
    path: '/admin/home',
    component: SystemAdminHomepage,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  {
    path: '/admin/user-management',
    component: SystemAdminUserManagement,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  {
    path: '/admin/patients',
    component: SystemAdminPatient,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  {
    path: '/admin/vaccines',
    component: SystemAdminVaccine,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  {
    path: '/admin/inventory',
    component: SystemAdminInventory,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  {
    path: '/admin/notifications',
    component: SystemAdminNotification,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  {
    path: '/admin/reports',
    component: SystemAdminReports,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  // The audit logs moved to the Super Admin (technical adviser, Oct 2026)
  { path: '/admin/audit-logs', redirect: '/admin/home' },

  {
    path: '/admin/test-api',
    component: TestAPI,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  {
    path: '/admin/vaccine-schedule',
    component: VaccineSchedule,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },

  // One vaccination room only (beneficiary revision), so the room list is gone
  { path: '/admin/rooms', redirect: '/admin/home' },

  {
    path: '/admin/operating-hours',
    component: SystemOperatingHours,
    meta: {
      requiresAuth: true,
      role: 'SystemAdmin'
    }
  },


  // Old /system-admin/* links and bookmarks
  { path: '/system-admin/:rest(.*)*', redirect: to => `/admin/${[].concat(to.params.rest || []).join('/') || 'home'}` },


  // ── Super Admin ────────────────────────────────────────────
  // The development team: system status, Admin / Doctor accounts and the
  // audit logs. No patient pages.

  {
    path: '/super-admin/home',
    component: SuperAdminHome,
    meta: { requiresAuth: true, role: 'SuperAdmin' }
  },
  {
    path: '/super-admin/accounts',
    component: SuperAdminAccounts,
    meta: { requiresAuth: true, role: 'SuperAdmin' }
  },
  {
    path: '/super-admin/audit-logs',
    component: SystemAuditlogs,
    meta: { requiresAuth: true, role: 'SuperAdmin' }
  },


  // ── Printable immunization record ─────────────────────────
  // Any signed-in role: admin, healthcare worker, staff, or the parent.

  {
    path: '/print/vaccination-card/:childId',
    component: VaccinationCard,
    meta: {
      requiresAuth: true
    }
  },


  // ── Change Password ────────────────────────────────────────
  // Any authenticated account can access this.

  {
    path: '/ChangePassword',
    component: ChangePassword,
    meta: {
      requiresAuth: true
    }
  }
]


const router = createRouter({
  history: createWebHistory(),
  routes
})

function homeFor(role) {
  switch (role) {
    case 'Parent':
      return '/ParentOverview'
    case 'Staff':
      return '/staff/dashboard'
    case 'SystemAdmin':
      return '/admin/home'
    case 'SuperAdmin':
      return '/super-admin/home'
    default:
      return '/'
  }
}


// ============================================================
// AUTHENTICATION / ROLE GUARD
// ============================================================

router.beforeEach((to) => {

  let loggedIn = isLoggedIn()
  const role = getRole()

  // A sign-in from before the three user levels (e.g. the old
  // "Healthcare" portal) has no home any more: sign in again.
  if (loggedIn && homeFor(role) === '/') {
    logout()
    loggedIn = false
  }


  // ----------------------------------------------------------
  // 1. Protected page + NOT logged in
  // ----------------------------------------------------------

  if (to.meta.requiresAuth && !loggedIn) {
    // Come back here after signing in (e.g. a parent who scanned the
    // clinic's check-in QR before logging in)
    return { path: '/', query: { redirect: to.fullPath } }
  }


  // ----------------------------------------------------------
  // 2. Already logged in + tries to open Login
  // ----------------------------------------------------------

  if (to.path === '/' && loggedIn) {
    return homeFor(role)
  }


  // ----------------------------------------------------------
  // 3. Logged in but wrong role
  // ----------------------------------------------------------

  if (to.meta.role && to.meta.role !== role) {
    return homeFor(role)
  }

  return true
})


export default router