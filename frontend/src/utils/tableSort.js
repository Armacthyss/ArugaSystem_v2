import { ref, computed, unref } from 'vue'

// Click a column heading to sort a table. The first click goes A–Z (or
// earliest / smallest first), the second reverses it, and the third puts the
// page's own order back. Blank cells stay at the bottom either way.
//
//   const sort = useTableSort(filteredRows, { age: r => r.birthDate }, () => { page.value = 1 })
//   <SortableTh :sort="sort" column="name">Name</SortableTh>
//   sort.sortedRows.value.slice(...)
//
// A column sorts by row[column] unless `values` says otherwise: give the raw
// date or number there, not the text on screen ("Nov 03, 2026", "1 yr 2 mos").
const collator = new Intl.Collator('en', { numeric: true, sensitivity: 'base' })

const isBlank = (v) => v === null || v === undefined || v === '' || v === '—' || Number.isNaN(v)

function compareValues(a, b) {
  const x = a instanceof Date ? a.getTime() : a
  const y = b instanceof Date ? b.getTime() : b
  if (typeof x === 'number' && typeof y === 'number') return x - y
  return collator.compare(String(x), String(y))
}

export function useTableSort(rows, values = {}, onChange) {
  const sortKey = ref('')
  const sortDir = ref('asc')

  const valueOf = (row, key) => {
    if (typeof values === 'function') return values(row, key)
    return values[key] ? values[key](row) : row[key]
  }

  function sortList(list) {
    const key = sortKey.value
    if (!key) return list
    const sign = sortDir.value === 'asc' ? 1 : -1
    return [...list].sort((a, b) => {
      const x = valueOf(a, key), y = valueOf(b, key)
      if (isBlank(x) || isBlank(y)) return isBlank(x) - isBlank(y)
      return sign * compareValues(x, y)
    })
  }

  function setSort(key, dir = 'asc') {
    sortKey.value = key
    sortDir.value = dir
    onChange?.()
  }

  function toggleSort(key) {
    if (sortKey.value !== key) setSort(key)
    else if (sortDir.value === 'asc') setSort(key, 'desc')
    else setSort('')
  }

  const sortedRows = computed(() => sortList(unref(rows) ?? []))

  return { sortKey, sortDir, toggleSort, setSort, sortList, sortedRows }
}

// The page numbers under a list: the first three, the last five and the
// pages around the current one, e.g. 1 2 3 ... 6 7 8 9 10.
export function pageList(current, last) {
  const show = new Set([1, 2, 3, current - 1, current, current + 1])
  for (let n = last - 4; n <= last; n++) show.add(n)
  const pages = [...show].filter(n => n >= 1 && n <= last).sort((a, b) => a - b)
  const list = []
  let previous = 0
  for (const n of pages) {
    if (n - previous === 2) list.push(n - 1)   // no "..." for one hidden page
    else if (n - previous > 2) list.push('...')
    list.push(n)
    previous = n
  }
  return list
}

// Report cells are already text: read dates and ages back so they sort as
// such ("Nov 03, 2026", "Nov 2026", "1 yr 2 mos", "5 mos", "3 days").
export function cellValue(cell) {
  if (typeof cell !== 'string') return cell
  if (/^[A-Z][a-z]{2,8} (\d{1,2}, )?\d{4}\b/.test(cell)) {
    const time = Date.parse(cell.replace(/,? (\d{1,2}:\d{2}) ?([AP]M)/i, ' $1 $2'))
    if (!Number.isNaN(time)) return time
  }
  const age = cell.match(/^(?:(\d+) yrs?)? ?(?:(\d+) mos?)?(?:(\d+) days?)?$/)
  if (age && (age[1] || age[2] || age[3])) return (+age[1] || 0) * 365 + (+age[2] || 0) * 30 + (+age[3] || 0)
  return cell
}

// The number cards above a list work as filters: click one to list only what
// it counts, click it again (or a Total card) for everything.
//
//   const { activeCard, pickCard, applyCard } = useCardFilter({ active: p => p.status === 'Active' }, () => { page.value = 1 })
//   <button :class="cardClass(activeCard === 'active')" @click="pickCard('active')">
//   const filtered = computed(() => applyCard(rows.value))
export function useCardFilter(filters, onChange) {
  const activeCard = ref('')
  function pickCard(key) {
    activeCard.value = activeCard.value === key || !filters[key] ? '' : key
    onChange?.()
  }
  function applyCard(list) {
    const matches = filters[activeCard.value]
    return matches ? list.filter(matches) : list
  }
  return { activeCard, pickCard, applyCard }
}

export const cardClass = (selected) => [
  'w-full text-left transition hover:border-emerald-300 hover:shadow-md focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500',
  selected ? 'border-emerald-600! ring-2 ring-emerald-600/20' : '',
]
