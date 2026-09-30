<!--
  PrivacyConsentCheckbox.vue — the required "the parent/guardian agreed to
  the Data Privacy Notice" tick box on every form that registers a parent.
  The API refuses the registration without it (CreateParentDto.PrivacyConsent).

  Usage:  <PrivacyConsentCheckbox v-model="form.privacyConsent" />
-->
<template>
  <div class="rounded-xl border px-4 py-3" :class="modelValue ? 'border-emerald-200 bg-emerald-50/50' : 'border-amber-200 bg-amber-50/60'">
    <label class="flex items-start gap-2.5 cursor-pointer">
      <input
        type="checkbox"
        :checked="modelValue"
        @change="$emit('update:modelValue', $event.target.checked)"
        class="mt-0.5 h-4 w-4 shrink-0 rounded border-slate-300 accent-emerald-600"
      />
      <span class="text-[12.5px] text-slate-700">
        <strong>Data Privacy consent — Data Privacy Act of 2012 (Republic Act No. 10173).</strong>
        The parent/guardian has read (or was read) the Data Privacy Notice and agrees that their and
        their child's information may be shared with and kept in Aruga.
        <button type="button" @click.prevent="open = !open" class="ml-1 font-semibold text-emerald-700 hover:underline">
          {{ open ? 'Hide notice' : 'Read the notice' }}
        </button>
      </span>
    </label>
    <div v-if="open" class="mt-3 max-h-64 overflow-y-auto rounded-lg border border-slate-200 bg-white p-3">
      <PrivacyNotice />
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import PrivacyNotice from './PrivacyNotice.vue'

defineProps({ modelValue: { type: Boolean, default: false } })
defineEmits(['update:modelValue'])

const open = ref(false)
</script>
