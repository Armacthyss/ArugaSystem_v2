// Shared vaccination schedule for the parent portal (Overview, Check-in,
// Schedule and Records pages).
//
// The schedule comes from the backend's VaccinationTimeline — the same
// data the health workers see — so dates already include the 28-day
// recalculation done when a dose is recorded late. This replaces the
// per-page copies of a hardcoded vaccine list, whose vaccine IDs had
// drifted from the database and never matched any recorded dose.
import api from './api'

// Normalizes a VaccinationRecords row to the field names the parent pages use.
function normalizeRecord(r) {
  return {
    ...r,
    recordId: r.recordId ?? r.vaccinationRecordID ?? null,
    dateAdministered: r.dateAdministered ?? r.vaccinationDate ?? null,
  }
}

export async function fetchChildSchedule(childId) {
  if (!childId) return { timeline: [], records: [] }
  const [t, r] = await Promise.all([
    api.get(`/VaccinationTimeline/child/${childId}`),
    api.get(`/VaccinationRecords/child/${childId}`),
  ])
  return {
    timeline: t.data ?? [],
    records: (r.data ?? []).map(normalizeRecord),
  }
}

const dayOnly = d => { const x = new Date(d); x.setHours(0, 0, 0, 0); return x }

// One row per scheduled dose:
//   { doseId, vaccineId, name, doseNumber, scheduledDate, originalDueDate,
//     isCompleted, administeredDate, wasLate, daysLate }
// scheduledDate is the administration date for doses already given,
// otherwise the (possibly recalculated) date the child is due.
export function buildSchedule(timeline, records) {
  return timeline
    .map(t => {
      const record = records.find(r =>
        Number(r.vaccineID ?? r.vaccineId) === Number(t.vaccineID) &&
        Number(r.doseNumber) === Number(t.doseNumber) &&
        (r.status ?? 'Completed') === 'Completed' &&
        r.dateAdministered)

      const originalDueDate = dayOnly(t.scheduledDate)
      const administeredDate = record ? new Date(record.dateAdministered) : null
      const isCompleted = !!record || t.status === 'Completed'
      const wasLate = !!administeredDate && dayOnly(administeredDate) > originalDueDate
      const daysLate = wasLate ? Math.round((dayOnly(administeredDate) - originalDueDate) / 86400000) : 0

      return {
        doseId: `${t.vaccineID}-${t.doseNumber}`,
        vaccineId: t.vaccineID,
        name: t.vaccineName,
        doseNumber: t.doseNumber,
        scheduledDate: administeredDate ?? originalDueDate,
        originalDueDate,
        isCompleted,
        administeredDate,
        wasLate,
        daysLate,
      }
    })
    .sort((a, b) => a.scheduledDate - b.scheduledDate || a.name.localeCompare(b.name))
}
