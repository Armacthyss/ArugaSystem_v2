// Aruga's own pop-up dialogs, instead of the browser's confirm()/alert()
// (those say "localhost:5173 says" and look out of place). The window
// itself is AppDialog.vue, placed once in App.vue.
//
//   if (!(await askConfirm({ title: 'Q-002 did not come in?', message: '...', confirmText: 'Back to Waiting' }))) return
//   await showAlert({ title: 'Could not save', message: '...', tone: 'error' })
//
// Both also take a plain string as the message.
import { reactive } from 'vue'

export const dialogState = reactive({
  open: false,
  kind: 'confirm',        // 'confirm' | 'alert'
  tone: 'primary',        // 'primary' | 'danger' | 'error' | 'info'
  title: '',
  message: '',
  details: [],            // optional bullet list under the message
  confirmText: 'OK',
  cancelText: 'Cancel',
  resolve: null,
})

function open(kind, options, defaults) {
  const o = typeof options === 'string' ? { message: options } : (options || {})
  // A dialog already open is answered "no" before the new one shows
  if (dialogState.resolve) dialogState.resolve(false)
  return new Promise(resolve => {
    Object.assign(dialogState, defaults, {
      title: '', message: '', details: [], ...o,
      kind, open: true, resolve,
    })
  })
}

// Resolves true (confirm button) or false (cancel, Esc, click outside)
export function askConfirm(options) {
  return open('confirm', options, { tone: 'primary', confirmText: 'Confirm', cancelText: 'Cancel' })
}

// Resolves when the person closes it
export function showAlert(options) {
  return open('alert', options, { tone: 'info', confirmText: 'OK' })
}

export function closeDialog(answer) {
  const resolve = dialogState.resolve
  dialogState.open = false
  dialogState.resolve = null
  resolve?.(answer)
}
