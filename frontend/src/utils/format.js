// Small shared formatting helpers for dates, ages and CSV export.

export { API_BASE } from './apiBase'

// "2026-09-25" in local time (not UTC — toISOString() would shift the day
// for Philippine time before 8 AM).
export function toISODate(d = new Date()) {
  const date = d instanceof Date ? d : new Date(d)
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

export function formatDate(value, withYear = true) {
  if (!value) return '—'
  const d = new Date(value)
  if (isNaN(d)) return '—'
  return d.toLocaleDateString('en-PH', withYear
    ? { month: 'short', day: '2-digit', year: 'numeric' }
    : { month: 'short', day: '2-digit' })
}

export function formatDateTime(value) {
  if (!value) return '—'
  const d = new Date(value)
  if (isNaN(d)) return '—'
  return d.toLocaleString('en-PH', { month: 'short', day: '2-digit', year: 'numeric', hour: 'numeric', minute: '2-digit' })
}

export function formatTime(value) {
  if (!value) return '—'
  return new Date(value).toLocaleTimeString('en-PH', { hour: 'numeric', minute: '2-digit' })
}

export function relativeTime(value) {
  if (!value) return ''
  const diff = Date.now() - new Date(value).getTime()
  const mins = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days = Math.floor(diff / 86400000)
  if (mins < 1) return 'Just now'
  if (mins < 60) return `${mins} minute${mins === 1 ? '' : 's'} ago`
  if (hours < 24) return `${hours} hour${hours === 1 ? '' : 's'} ago`
  if (days < 7) return `${days} day${days === 1 ? '' : 's'} ago`
  return formatDate(value)
}

export function isSameDay(a, b = new Date()) {
  const x = new Date(a), y = new Date(b)
  return x.getFullYear() === y.getFullYear() && x.getMonth() === y.getMonth() && x.getDate() === y.getDate()
}

// "3 days", "5 mos", "1 yr 4 mos"
// Whole months (and leftover days) since birth: a month only counts once its
// day is reached (born Sep 26 -> on Oct 2 that's 0 months 6 days, not 1 month)
export function ageParts(birthDate, on = new Date()) {
  if (!birthDate) return null
  // "2026-09-26" is a calendar day; new Date() alone would read it as UTC midnight
  const ymd = typeof birthDate === 'string' && birthDate.match(/^(\d{4})-(\d{2})-(\d{2})/)
  const b = ymd ? new Date(+ymd[1], ymd[2] - 1, +ymd[3]) : new Date(birthDate)
  if (Number.isNaN(b.getTime())) return null
  let months = (on.getFullYear() - b.getFullYear()) * 12 + (on.getMonth() - b.getMonth())
  if (on.getDate() < b.getDate()) months--
  months = Math.max(months, 0)
  const day = d => Date.UTC(d.getFullYear(), d.getMonth(), d.getDate())
  const days = Math.max(Math.round((day(on) - day(b)) / 86400000), 0)
  return { years: Math.floor(months / 12), months: months % 12, totalMonths: months, days }
}

export function ageLabel(birthDate) {
  if (!birthDate) return '—'
  const b = new Date(birthDate), now = new Date()
  const days = Math.floor((now - b) / 86400000)
  if (days < 31) return `${Math.max(days, 0)} day${days === 1 ? '' : 's'}`
  let months = (now.getFullYear() - b.getFullYear()) * 12 + (now.getMonth() - b.getMonth())
  if (now.getDate() < b.getDate()) months--
  if (months < 12) return `${months} mo${months === 1 ? '' : 's'}`
  const years = Math.floor(months / 12), rem = months % 12
  return `${years} yr${years === 1 ? '' : 's'}${rem ? ` ${rem} mo${rem === 1 ? '' : 's'}` : ''}`
}

// "Rosario Santos (Grandmother)": who a person is to the child.
export function withRelationship(name, relationship) {
  if (!name) return '—'
  return relationship ? `${name} (${relationship})` : name
}

// The user levels, from an account's position/role. Older accounts
// may still say "Administrator" (Admin level) or "Staff" (Staff level).
export function userLevel(role) {
  if (role === 'SuperAdmin') return 'Super Admin'
  if (role === 'Parent') return 'Parent'
  if (role === 'Doctor' || role === 'Administrator' || role === 'SystemAdmin') return 'Admin / Doctor'
  if (role === 'Nurse' || role === 'Staff' || role === 'Admission' || role === 'Healthcare') return 'Staff / Nurse'
  return role || '—'
}

// A child's linked parents/guardians, primary contact first. Takes the
// `parents` array from /api/Children/overview, /api/Children/{id} or /all.
export function guardiansOf(parents = []) {
  return (parents || [])
    .map(p => ({
      name: p.name ?? p.parentName ?? '',
      relationship: p.relationshipType ?? '',
      isPrimary: !!p.isPrimaryContact,
      contact: p.contactNo ?? null,
    }))
    .filter(g => g.name)
    .sort((a, b) => (b.isPrimary - a.isPrimary) || a.name.localeCompare(b.name))
}

// Downloads rows (array of arrays, first row = header) as an Excel-friendly CSV.
export function downloadCSV(filename, rows) {
  const cell = v => {
    const s = String(v ?? '')
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
  }
  const csv = rows.map(r => r.map(cell).join(',')).join('\r\n')
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

// Per-vaccine stock summary from /api/VaccineInventory + /api/Vaccines.
// Only active, unexpired batches count as usable stock.
export function summarizeStock(inventory, vaccines) {
  const today = toISODate()
  return vaccines.map(v => {
    const batches = inventory.filter(i => i.vaccineID === v.vaccineID)
    const usable = batches.filter(i => i.status && String(i.expirationDate).slice(0, 10) >= today)
    const stock = usable.reduce((s, i) => s + (i.currentQuantity || 0), 0)
    const minimum = usable.reduce((s, i) => s + (i.minimumStock || 0), 0) || 20
    const expiringSoon = usable.filter(i => {
      const days = (new Date(i.expirationDate) - new Date()) / 86400000
      return days <= 30 && i.currentQuantity > 0
    }).length
    const status = stock === 0 ? 'Critical' : stock < minimum ? 'Low Stock' : 'Good'
    return { vaccineID: v.vaccineID, name: v.vaccineName, abbreviation: v.abbreviation, stock, minimum, status, expiringSoon, batches: batches.length }
  })
}
