// Philippine mobile numbers, always written as "+63 9XX XXX XXXX".
//
// v-ph-mobile — formats the box as the number is typed or pasted
// (09171234567, 639171234567, +63 917-123-4567 ... all become
// "+63 917 123 4567"), and shows an error under the box when it is left
// with a number that isn't a complete +63 9XX XXX XXXX.
//
//   <input v-model="form.contactNo" v-ph-mobile placeholder="+63 9XX XXX XXXX" />
//
// The box can't stop a form from being saved, so the save function also
// checks it:
//
//   const err = phMobileError(form.contactNo, { required: true })
//   if (err) { form.error = err; return }

export const PH_MOBILE_PLACEHOLDER = '+63 9XX XXX XXXX'
export const PH_MOBILE_ERROR = 'Enter the mobile number as +63 9XX XXX XXXX (e.g. +63 917 123 4567).'

const PH_MOBILE = /^\+63 9\d{2} \d{3} \d{4}$/
// The start of "+63" being typed by hand: left as it is until more follows
const COUNTRY_CODE_START = /^(\+|\+?63?)$/

// Any way of writing the number -> "+63 9XX XXX XXXX" (or as much of it as
// has been typed so far). Numbers saved before this format (09171234567)
// are converted too.
export function formatPhMobile(value) {
  const text = String(value ?? '').trim()
  if (COUNTRY_CODE_START.test(text)) return text
  let d = text.replace(/\D/g, '')
  // Country code. A mobile number itself starts with 9 (or 09), never 63.
  if (d.startsWith('63')) d = d.slice(2)
  if (d.startsWith('0')) d = d.slice(1) // 09XX... -> 9XX...
  d = d.slice(0, 10)
  if (!d) return ''
  return '+63 ' + [d.slice(0, 3), d.slice(3, 6), d.slice(6)].filter(Boolean).join(' ')
}

export const isPhMobile = (value) => PH_MOBILE.test(String(value ?? '').trim())

// The error to show for this value, or '' when it's fine.
export function phMobileError(value, { required = false } = {}) {
  if (!String(value ?? '').trim()) return required ? 'Enter a mobile number (+63 9XX XXX XXXX).' : ''
  return isPhMobile(value) ? '' : PH_MOBILE_ERROR
}

// Put the caret back after the same number of digits it was after before
// the box was reformatted (so editing the middle of the number works).
function caretAfterDigits(text, digitCount) {
  if (digitCount <= 0) return 0
  let seen = 0
  for (let i = 0; i < text.length; i++) {
    if (/\d/.test(text[i]) && ++seen === digitCount) return i + 1
  }
  return text.length
}

export const phMobile = {
  mounted(el) {
    el.setAttribute('inputmode', 'tel')
    el.setAttribute('autocomplete', 'tel')
    if (!el.getAttribute('placeholder')) el.setAttribute('placeholder', PH_MOBILE_PLACEHOLDER)

    const message = document.createElement('p')
    message.className = 'mt-1 text-xs text-rose-600'
    message.textContent = PH_MOBILE_ERROR
    message.hidden = true
    el.after(message)
    // value: the last value this directive saw, to spot ones set by code
    el._phMobile = { message, value: null }

    const showError = (show) => {
      message.hidden = !show
      el.style.borderColor = show ? '#e11d48' : ''
      el.setAttribute('aria-invalid', show ? 'true' : 'false')
    }
    el._phMobile.showError = showError

    el.addEventListener('input', () => {
      const raw = el.value
      const formatted = formatPhMobile(raw)
      if (formatted !== raw) {
        const atEnd = el.selectionStart == null || el.selectionStart >= raw.length
        const digitsBefore = atEnd ? 0 : raw.slice(0, el.selectionStart).replace(/\D/g, '').length
        el.value = formatted
        if (!atEnd && document.activeElement === el) {
          const pos = caretAfterDigits(formatted, digitsBefore)
          el.setSelectionRange(pos, pos)
        }
        el.dispatchEvent(new Event('input')) // tell v-model
        return
      }
      el._phMobile.value = formatted
      // Once an error is showing, clear it as soon as the number is right
      if (!message.hidden && (!formatted || isPhMobile(formatted))) showError(false)
    })

    el.addEventListener('blur', () => {
      // Only "+63" (or part of it) typed: treat the box as empty
      if (el.value && COUNTRY_CODE_START.test(el.value.trim())) {
        el.value = ''
        el.dispatchEvent(new Event('input'))
      }
      showError(!!el.value && !isPhMobile(el.value))
    })

    // v-model fills the box after this hook runs; old numbers (09XXXXXXXXX)
    // are converted once it has.
    queueMicrotask(() => normalize(el))
  },
  updated(el) {
    normalize(el)
  },
  unmounted(el) {
    el._phMobile?.message.remove()
  },
}

// A value set by code (opening an edit form, resetting a form) that isn't
// in the +63 format yet is converted, and the form's data updated to match.
// It starts without an error message.
function normalize(el) {
  const state = el._phMobile
  if (!state || el.value === state.value) return
  state.value = el.value
  state.showError(false)
  const formatted = formatPhMobile(el.value)
  if (formatted !== el.value) {
    el.value = formatted
    el.dispatchEvent(new Event('input'))
  }
}
