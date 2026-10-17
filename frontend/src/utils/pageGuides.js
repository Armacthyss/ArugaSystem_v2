import { ref } from 'vue'

// What each Staff / Nurse and Admin / Doctor page is for, in plain words.
// The top bar shows `subtitle` under the page title, and the "How to use
// this page" card (Shared/PageGuide.vue) lists the `steps`. Survey feedback
// (Oct 2026) asked for clearer pages with instructions, so every page says
// what it's for and what to press. Edit the wording here: one place for all.
//
// Keys are route paths; a key ending in "/" matches every path under it.
const GUIDES = {
  // ── Staff / Nurse ──────────────────────────────────────────
  '/staff/dashboard': {
    subtitle: "Run today's clinic: check families in, call them into the room, record their vaccines.",
    steps: [
      { title: 'Check in', text: 'Families scan the Check-in QR on their phone. No phone? Use Check-In Patient (registered) or Add Patient (new).' },
      { title: 'Call Next', text: "In the Vaccination Room, press Call Next. The parent's phone gets an alert and a text." },
      { title: 'Record vaccines', text: 'Press Start Vaccinating for each child and record every dose you give.' },
      { title: 'Complete Visit', text: 'When every child is done, press Complete Visit, then Call Next for the next family.' },
    ],
    tip: "Family didn't come in? Press Not Here: they keep their number. To take a family off the list, use ⋯ on their row.",
  },
  '/staff/vaccination/': {
    subtitle: 'Record the vaccines given to this child during the visit.',
    steps: [
      { title: 'Check the child', text: 'Look at allergies and existing conditions (shown in red) before vaccinating.' },
      { title: 'Record Dose', text: 'Press Record Dose on each vaccine you give, pick the batch and injection site, then save.' },
      { title: 'Next child', text: 'Brought more than one child? Switch between them using the names at the top.' },
      { title: 'Complete Visit', text: 'When everyone is done, press Complete Visit to free the room.' },
    ],
  },
  '/staff/patient-records': {
    subtitle: 'Find, register and update parents and their children.',
    steps: [
      { title: 'Find', text: 'Pick Parents or Children, then search by name, Family No. or barangay.' },
      { title: 'View or edit', text: 'Use the eye to see details and the pencil to edit.' },
      { title: 'More actions', text: 'Use ⋯ to register or link a child, add old vaccination records, or reset a login.' },
      { title: 'New family', text: 'Press Register Child. Parent not on file yet? Use Register a new parent inside the same form.' },
    ],
  },
  '/staff/vaccination-records': {
    subtitle: 'Every dose you have given, with its batch number and remarks.',
    steps: [
      { title: 'Search', text: 'Type a child’s name, a vaccine or a batch number.' },
      { title: 'Filter', text: 'Choose a vaccine or a date range to narrow the list.' },
      { title: 'Details', text: 'Use the eye on a row to read the full remarks.' },
    ],
  },
  '/staff/vaccine-schedule': {
    subtitle: 'Who is due today, who is overdue, and what is coming up.',
    steps: [
      { title: 'Pick a list', text: 'Click a box at the top: Due today, Overdue, Coming up or Done today.' },
      { title: 'See the vaccines', text: 'Click a child’s row to see every vaccine for that visit.' },
      { title: 'Check in at the desk', text: 'Parent is here but can’t check in on their phone? Press Check In at Desk.' },
    ],
  },
  '/staff/vaccine-inventory': {
    subtitle: 'Vaccine stock on hand, batches received and the weekly stock check.',
    steps: [
      { title: 'Check the summary', text: 'Low Stock or Expiring numbers in red need attention first.' },
      { title: 'New stock arrived', text: 'Press Add New Batch and type the lot number, quantity and dates from the box.' },
      { title: 'Broken or spoiled doses?', text: 'Press Adjust on that batch, say how many and why. A recount works the same way.' },
      { title: 'Weekly stock check', text: 'Every Wednesday the system suggests how much to order. Call the pharmacy the same day.' },
    ],
  },
  '/staff/calendar': {
    subtitle: 'Vaccination days at a glance: who is due and who was vaccinated.',
    steps: [
      { title: 'Read the colors', text: 'Yellow = children due, green = vaccinated, gray = no vaccinations that day.' },
      { title: 'Open a day', text: 'Click a day to list its children on the right.' },
      { title: 'Change month', text: 'Use the arrows at the top. Today brings you back.' },
    ],
  },
  '/staff/queue-management': {
    subtitle: "A read-only record of each day's queue. Today's queue is run from the Dashboard.",
    steps: [
      { title: 'Pick a day', text: 'Use the arrows or the date box to look at another day.' },
      { title: 'Search', text: 'Find a child, parent or the person who brought them.' },
      { title: 'Details', text: 'Use the eye on a row to see the children and times.' },
    ],
  },
  '/staff/checkin-qr': {
    subtitle: "Print today's code for the entrance. Parents scan it to get a queue number.",
  },
  '/staff/reports': {
    subtitle: 'Vaccination numbers for the year, ready to download as a PDF.',
    steps: [
      { title: 'Pick the year', text: 'Use the year box at the top.' },
      { title: 'Download', text: 'Press Download PDF to save or print the report.' },
    ],
  },
  '/staff/settings': {
    subtitle: 'Your contact details and password.',
  },

  // ── Admin / Doctor ─────────────────────────────────────────
  '/admin/home': {
    subtitle: "A summary of the whole clinic and what needs attention.",
    steps: [
      { title: 'Read the cards', text: 'Totals for users, patients, this month’s vaccinations and low stock.' },
      { title: 'Needs attention', text: 'The Notifications box lists low stock, expiring batches and overdue children.' },
      { title: 'Quick Actions', text: 'Shortcuts on the right to add a user, patient or vaccine.' },
    ],
  },
  '/admin/user-management': {
    subtitle: 'Add Staff / Nurse accounts and manage everyone’s sign-in.',
    steps: [
      { title: 'Add a Nurse', text: 'Press Add User. They set their own password the first time they sign in.' },
      { title: 'Find someone', text: 'Search by name, username or email, or filter by role and status.' },
      { title: 'Manage', text: 'Use ⋮ on a row to view, edit, reset a password or deactivate.' },
    ],
  },
  '/admin/patients': {
    subtitle: "Every registered child and how far along their vaccinations are.",
    steps: [
      { title: 'Find a child', text: 'Search by name, Family No., barangay or parent.' },
      { title: 'See what’s missing', text: 'Hover over (or tap) the progress bar to list the vaccines not yet given.' },
      { title: 'Manage', text: 'Use ⋮ on a row to view, edit, print the vaccination card or link a guardian.' },
    ],
  },
  '/admin/vaccines': {
    subtitle: 'The vaccines the clinic gives and the days between doses.',
    steps: [
      { title: 'Read the schedule', text: '“Dose 2 (+28d)” means dose 2 is due 28 days after dose 1.' },
      { title: 'Add or edit', text: 'Press Add Vaccine, or use ⋮ on a row to edit it.' },
      { title: 'Stop using one', text: 'Deactivate a vaccine instead of deleting it, so past records stay.' },
    ],
  },
  '/admin/inventory': {
    subtitle: 'Vaccine stock by batch: what is on hand, low or expiring.',
    steps: [
      { title: 'Check the summary', text: 'Red numbers (low, expiring, expired) need attention first.' },
      { title: 'New stock arrived', text: 'Press Receive New Batch and type the lot number, quantity and dates.' },
      { title: 'See batches', text: 'Click a vaccine to open its batches. Use ⋮ to edit one, adjust its stock (broken, spoiled, recount) or deactivate it.' },
      { title: 'Tell the Nurses', text: 'Press Alert Staff to tell every Nurse what to order.' },
    ],
  },
  '/admin/notifications': {
    subtitle: 'Reminders and announcements sent to parents.',
    steps: [
      { title: 'Check what was sent', text: 'Each row is one message, with who got it and whether it was delivered.' },
      { title: 'Announce', text: 'Press Create Announcement to message every parent.' },
      { title: 'Reminders', text: 'They go out by themselves every morning at 8:00 AM. Reminder Schedule shows when each one is sent.' },
    ],
  },
  '/admin/operating-hours': {
    subtitle: 'Vaccination days and hours, plus closures and extra days.',
    steps: [
      { title: 'Weekly hours', text: 'Use Edit on a day to change its hours or close it.' },
      { title: 'Closed for a day?', text: 'Press Add Exception for a holiday or typhoon. Parents are told automatically.' },
      { title: 'Extra vaccination day', text: 'Add an Exception set to Open for that date.' },
    ],
  },
  '/admin/reports': {
    subtitle: 'Printable reports on patients, vaccinations, stock and the queue.',
    steps: [
      { title: 'Pick a report', text: 'Choose a category on the left, then the report you need.' },
      { title: 'Set filters', text: 'Choose the period, barangay or vaccine, then press Refresh Report.' },
      { title: 'Save it', text: 'Use Print, Export PDF or Export Excel at the top of the report.' },
    ],
  },
}

export function guideFor(path) {
  if (GUIDES[path]) return GUIDES[path]
  const key = Object.keys(GUIDES).find(k => k.endsWith('/') && path.startsWith(k))
  return key ? GUIDES[key] : null
}

// Pages whose "How to use this page" card the user has hidden. Kept in this
// browser only; the Help (?) button in the top bar shows it again.
const STORAGE_KEY = 'aruga.hiddenGuides'
function loadHidden() {
  try { return JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]') } catch { return [] }
}
const hidden = ref(loadHidden())

function guideKey(path) {
  return Object.keys(GUIDES).find(k => k === path || (k.endsWith('/') && path.startsWith(k))) || path
}

export const isGuideHidden = (path) => hidden.value.includes(guideKey(path))

export function setGuideHidden(path, value) {
  const key = guideKey(path)
  hidden.value = value ? [...new Set([...hidden.value, key])] : hidden.value.filter(k => k !== key)
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(hidden.value)) } catch { /* only for this visit */ }
}
