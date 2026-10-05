// When should the family come next, and for which vaccines?
// Shared by the parent Overview and Schedule pages.
import { format, differenceInCalendarDays } from 'date-fns'

export const dayOnly = d => { const x = new Date(d); x.setHours(0, 0, 0, 0); return x }

// A vaccination day per the admin's Operating Hours: the open weekdays,
// with holidays / special openings (exceptions) taking priority.
export function isClinicDay(clinic, date) {
  if (!clinic) return false
  const key = format(date, 'yyyy-MM-dd')
  const exception = (clinic.exceptions || []).find(e => e.date === key)
  if (exception) return exception.isOpen
  return (clinic.openDays || []).includes(date.getDay())
}

export function isClosedException(clinic, date) {
  const key = format(date, 'yyyy-MM-dd')
  return !!(clinic?.exceptions || []).find(e => e.date === key && !e.isOpen)
}

// The first vaccination day on or after `from`. Today only counts while
// check-in is still possible.
export function nextVaccinationDay(clinic, from = new Date()) {
  const start = dayOnly(from)
  if (!clinic) return start
  const today = dayOnly(new Date())
  const d = new Date(start)
  for (let i = 0; i < 400; i++) {
    if (isClinicDay(clinic, d)) {
      const checkInOver = d.getTime() === today.getTime() &&
        clinic.openToday && !clinic.checkInOpenNow && !clinic.checkInNotYetOpen
      if (!checkInOver) return d
    }
    d.setDate(d.getDate() + 1)
  }
  return start
}

// The child's next visit from the schedule built by childSchedule.js:
//   { date, doses (all due by that day, overdue first), overdue, firstDue, later }
export function planNextVisit(schedule, clinic) {
  const pending = schedule.filter(v => !v.isCompleted).sort((a, b) => a.scheduledDate - b.scheduledDate)
  if (!pending.length) return null
  const today = dayOnly(new Date())
  const first = pending[0]
  const overdue = first.scheduledDate < today
  const date = nextVaccinationDay(clinic, overdue ? today : first.scheduledDate)
  const doses = pending.filter(v => v.scheduledDate <= date)
  return {
    date,
    doses: doses.length ? doses : [first],
    overdue,
    firstDue: first.scheduledDate,
    later: pending.filter(v => v.scheduledDate > date),
  }
}

// "8:00 AM – 12:00 PM" for a vaccination day
export function visitHours(clinic, date) {
  if (!clinic) return ''
  if (date && dayOnly(date).getTime() === dayOnly(new Date()).getTime() && clinic.opensAt && clinic.closesAt) {
    return `${clinic.opensAt} – ${clinic.closesAt}`
  }
  const parts = String(clinic.hoursText || '').split('·')
  return parts.length > 1 ? parts.slice(1).join('·').trim() : ''
}

// "today", "tomorrow", "in 3 days"
export function relativeDay(date) {
  const n = differenceInCalendarDays(dayOnly(date), dayOnly(new Date()))
  if (n === 0) return 'today'
  if (n === 1) return 'tomorrow'
  if (n > 1) return `in ${n} days`
  return `${-n} day${n === -1 ? '' : 's'} ago`
}

export const ordinal = n => {
  const s = ['th', 'st', 'nd', 'rd'], v = n % 100
  return n + (s[(v - 20) % 10] || s[v] || s[0])
}

// "8:00 AM" -> "0800"
function to24h(text, fallback) {
  const m = String(text || '').match(/(\d{1,2}):(\d{2})\s*(AM|PM)/i)
  if (!m) return fallback
  let h = Number(m[1]) % 12
  if (/pm/i.test(m[3])) h += 12
  return `${String(h).padStart(2, '0')}${m[2]}`
}

// "Add reminder": a calendar file (.ics) the phone opens in its calendar app,
// with an alert the day before. Aruga's own reminders still go out as usual.
export function downloadVisitReminder({ child, visit, hours }) {
  const [from, to] = String(hours || '').split(/\s*[–-]\s*/)
  const day = format(visit.date, 'yyyyMMdd')
  const start = to24h(from, '0800')
  const end = to24h(to, '1200')
  const vaccines = visit.doses.map(d => `${d.name} (Dose ${d.doseNumber})`).join(', ')
  const stamp = new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '')
  const esc = s => String(s).replace(/([,;\\])/g, '\\$1')
  const ics = [
    'BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Aruga//Leveriza Health Center//EN', 'CALSCALE:GREGORIAN',
    'BEGIN:VEVENT',
    `UID:aruga-${child.childID}-${day}@aruga`,
    `DTSTAMP:${stamp}`,
    `DTSTART:${day}T${start}00`,
    `DTEND:${day}T${end}00`,
    `SUMMARY:${esc(`${child.firstName}'s vaccination`)}`,
    'LOCATION:Leveriza Health Center',
    `DESCRIPTION:${esc(`Vaccines: ${vaccines}. Bring the Yellow Book and check in with the clinic QR code.`)}`,
    'BEGIN:VALARM', 'TRIGGER:-P1D', 'ACTION:DISPLAY', `DESCRIPTION:${esc(`${child.firstName}'s vaccination is tomorrow`)}`, 'END:VALARM',
    'END:VEVENT', 'END:VCALENDAR',
  ].join('\r\n')

  const url = URL.createObjectURL(new Blob([ics], { type: 'text/calendar;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = `${child.firstName}-vaccination-${format(visit.date, 'yyyy-MM-dd')}.ics`
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
