// v-digits — only the numbers 0-9 can be typed or pasted into this box
// (license no., quantities, ...). Letters, spaces and symbols never appear.
// v-digits.decimal also allows one "." (weights). Mobile numbers use
// v-ph-mobile instead (utils/phone.js).
//
//   <input v-model="form.licenseNumber" v-digits />
//   <input v-model="form.birthWeight" type="number" v-digits.decimal />
export const digits = {
  mounted(el, binding) {
    const decimal = !!binding.modifiers.decimal
    const badChar = decimal ? /[^0-9.]/ : /[^0-9]/
    const badChars = decimal ? /[^0-9.]/g : /[^0-9]/g
    el.setAttribute('inputmode', decimal ? 'decimal' : 'numeric')

    // Stop a bad key before it lands (also stops "e", "+", "-" in number boxes).
    el.addEventListener('beforeinput', (e) => {
      if (e.data && badChar.test(e.data)) e.preventDefault()
    })

    // Clean up anything that still got in (paste, autofill) and tell v-model.
    // Number boxes can't be cleaned this way (their value reads as "" when
    // invalid), but the check above already covers them.
    if (el.type === 'number') return
    el.addEventListener('input', () => {
      let v = el.value.replace(badChars, '')
      if (decimal) {
        const dot = v.indexOf('.')
        if (dot >= 0) v = v.slice(0, dot + 1) + v.slice(dot + 1).replace(/\./g, '')
      }
      if (v !== el.value) {
        el.value = v
        el.dispatchEvent(new Event('input'))
      }
    })
  },
}
