// Leveriza Health Center only serves Barangays 19 and 21–40. Families from
// any other barangay are outside its jurisdiction and can't be registered
// (the API checks the same list: backend/Service/Barangays.cs).
export const BARANGAYS = [19, ...Array.from({ length: 20 }, (_, i) => 21 + i)]

export const BARANGAY_HINT = 'Leveriza serves Barangays 19 and 21–40 only.'

export function isServedBarangay(value) {
  if (value === '' || value == null) return true
  return BARANGAYS.includes(Number(value))
}

// Dropdown choices. An older record may still have a barangay from before
// the jurisdiction check; it stays in the list (marked) so editing the
// record doesn't silently change it.
export function barangayChoices(current) {
  const list = BARANGAYS.map(n => ({ value: String(n), label: `Barangay ${n}` }))
  if (current !== '' && current != null && !isServedBarangay(current)) {
    list.unshift({ value: String(current), label: `${current} (outside Leveriza's area)`, outside: true })
  }
  return list
}
