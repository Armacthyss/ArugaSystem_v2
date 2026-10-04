import { API_BASE } from './apiBase'

export function getAccount() {
  const savedAccount = localStorage.getItem('account')

  if (!savedAccount) {
    return null
  }

  try {
    return JSON.parse(savedAccount)
  } catch (error) {
    console.error('Invalid account session:', error)
    return null
  }
}

export function getToken() {
  return localStorage.getItem('authToken')
}

export function getRole() {
  return getAccount()?.role || null
}

export function getUser() {
  return getAccount()?.user || null
}

// UserType is the job title inside `user` — 'Doctor' | 'Nurse' (older
// accounts may say 'Administrator' or 'Staff'). `role` (above) is the coarse
// value the route guard checks against `meta.role`: 'SystemAdmin' (Admin /
// Doctor), 'Staff' (Staff / Nurse) or 'Parent'. UserType is only for display.
export function getUserType() {
  return getUser()?.UserType || null
}

export function isLoggedIn() {
  return !!getToken() && !!getAccount()
}

export function logout() {
  // Tell the API this browser has left (only used by survey mode, where the
  // test data resets once every tester has logged out). Fire-and-forget:
  // logging out never waits for it.
  const token = getToken()
  if (token) {
    fetch(`${API_BASE}/auth/logout`, { method: 'POST', headers: { Authorization: `Bearer ${token}` }, keepalive: true }).catch(() => {})
  }

  localStorage.removeItem('account')
  localStorage.removeItem('authToken')

  // Parent-specific session cleanup
  localStorage.removeItem('selectedParentChild')
  localStorage.removeItem('parentUser')
  localStorage.removeItem('selectedChild')

  // Legacy keys from the old Doctor/Healthcare auth path. Safe to clear
  // unconditionally now that Login.vue no longer writes them and every
  // page reads through this file instead of localStorage directly.
  localStorage.removeItem('aruga_token')
  localStorage.removeItem('aruga_user')
}