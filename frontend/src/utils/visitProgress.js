// Where each child of a family visit stands today, so the Nurse finishes
// every child before pressing Complete Visit (a parent can bring several).
//   due        doses due today or overdue that haven't been given
//   givenToday doses recorded today
//   state      'done' (nothing left due) | 'started' | 'todo'
import axios from 'axios'
import { API_BASE, toISODate } from './format'

const day = (value) => (value ? String(value).slice(0, 10) : '')

export async function childProgress(childId) {
  const today = toISODate()
  const [tl, recs] = await Promise.all([
    axios.get(`${API_BASE}/VaccinationTimeline/child/${childId}`),
    axios.get(`${API_BASE}/VaccinationRecords/child/${childId}`),
  ])
  const due = tl.data.filter(t => (t.status === 'Pending' || t.status === 'Missed') && day(t.scheduledDate) <= today).length
  const givenToday = recs.data.filter(r => (r.status ?? 'Completed') === 'Completed' && day(r.vaccinationDate) === today).length
  return { due, givenToday, state: due === 0 ? 'done' : givenToday > 0 ? 'started' : 'todo' }
}

// { [childID]: progress } for a visit's children; a child whose data
// can't be loaded is left out rather than blocking the page.
export async function visitProgress(children = []) {
  const entries = await Promise.all(children.map(async c => {
    try { return [c.childID, await childProgress(c.childID)] } catch { return null }
  }))
  return Object.fromEntries(entries.filter(Boolean))
}

// "Ethan (2 vaccines)" for children who still have doses due today
export function unfinishedChildren(children, progress, exceptId = null) {
  return children
    .filter(c => c.childID !== exceptId && progress[c.childID]?.due > 0)
    .map(c => `${c.name} (${progress[c.childID].due} vaccine${progress[c.childID].due === 1 ? '' : 's'})`)
}
