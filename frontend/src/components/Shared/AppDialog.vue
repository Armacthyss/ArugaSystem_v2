<!--
  AppDialog.vue — the pop-up behind askConfirm() / showAlert()
  (utils/dialog.js). Placed once in App.vue so every page can use it.
-->
<template>
  <Teleport to="body">
    <Transition name="app-dialog">
      <div
        v-if="dialogState.open"
        class="fixed inset-0 z-[1000] flex items-center justify-center bg-slate-900/50 p-4"
        @click.self="cancel"
      >
        <div
          role="alertdialog"
          aria-modal="true"
          :aria-labelledby="dialogState.title ? 'app-dialog-title' : undefined"
          aria-describedby="app-dialog-message"
          class="w-full max-w-md rounded-2xl bg-white shadow-2xl"
        >
          <div class="flex items-start gap-4 px-6 pt-6">
            <div class="flex h-11 w-11 shrink-0 items-center justify-center rounded-full" :class="look.iconBg">
              <component :is="look.icon" class="h-5 w-5" :class="look.iconText" />
            </div>
            <div class="min-w-0 flex-1 pt-0.5">
              <h3 v-if="dialogState.title" id="app-dialog-title" class="text-base font-bold text-slate-800">{{ dialogState.title }}</h3>
              <p id="app-dialog-message" class="mt-1 whitespace-pre-line text-sm leading-relaxed text-slate-600">{{ dialogState.message }}</p>
              <ul v-if="dialogState.details?.length" class="mt-3 space-y-1 rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-700">
                <li v-for="(d, i) in dialogState.details" :key="i" class="flex gap-2"><span class="text-slate-400">•</span><span>{{ d }}</span></li>
              </ul>
            </div>
          </div>

          <div class="mt-6 flex flex-col-reverse gap-2 border-t border-slate-100 px-6 py-4 sm:flex-row sm:justify-end">
            <button
              v-if="dialogState.kind === 'confirm'"
              type="button"
              class="rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-semibold text-slate-600 hover:bg-slate-50"
              @click="cancel"
            >
              {{ dialogState.cancelText }}
            </button>
            <button
              ref="okButton"
              type="button"
              class="rounded-xl px-5 py-2.5 text-sm font-bold text-white"
              :class="look.button"
              @click="closeDialog(true)"
            >
              {{ dialogState.confirmText }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { AlertTriangle, CircleHelp, Info, XCircle } from 'lucide-vue-next'
import { dialogState, closeDialog } from '@/utils/dialog'

const okButton = ref(null)

const looks = {
  primary: { icon: CircleHelp, iconBg: 'bg-emerald-50', iconText: 'text-emerald-600', button: 'bg-emerald-600 hover:bg-emerald-700' },
  danger: { icon: AlertTriangle, iconBg: 'bg-red-50', iconText: 'text-red-600', button: 'bg-red-600 hover:bg-red-700' },
  error: { icon: XCircle, iconBg: 'bg-red-50', iconText: 'text-red-600', button: 'bg-slate-800 hover:bg-slate-900' },
  info: { icon: Info, iconBg: 'bg-sky-50', iconText: 'text-sky-600', button: 'bg-emerald-600 hover:bg-emerald-700' },
}
const look = computed(() => looks[dialogState.tone] || looks.primary)

// Esc / click outside = Cancel (or just close, for a message)
const cancel = () => closeDialog(false)
const onKey = e => { if (dialogState.open && e.key === 'Escape') cancel() }
onMounted(() => window.addEventListener('keydown', onKey))
onBeforeUnmount(() => window.removeEventListener('keydown', onKey))

// Enter presses the focused main button
watch(() => dialogState.open, async open => {
  if (open) { await nextTick(); okButton.value?.focus() }
})
</script>

<style scoped>
.app-dialog-enter-active, .app-dialog-leave-active { transition: opacity .15s ease; }
.app-dialog-enter-from, .app-dialog-leave-to { opacity: 0; }
</style>
