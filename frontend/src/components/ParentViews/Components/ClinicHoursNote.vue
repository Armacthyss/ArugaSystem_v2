<template>
  <!-- The health center's general hours, and the (shorter) vaccination days
       and hours from the admin's Operating Hours.
       Data: GET /ClinicOperatingSchedule/today → clinicHoursText, hoursText -->
  <div v-if="clinic?.hoursText" class="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 flex items-start gap-2">
    <span class="text-base leading-6">💡</span>
    <div class="text-sm text-amber-700 space-y-1">
      <p v-if="clinic.clinicHoursText"><span class="font-bold">Clinic hours:</span> {{ clinic.clinicHoursText }}</p>
      <p>
        <span class="font-bold">Vaccination hours:</span> {{ clinic.hoursText }}
        <span v-if="showCheckIn && clinic.openToday && clinic.checkInUntil"> · Check-in until {{ clinic.checkInUntil }} </span>
      </p>
    </div>
  </div>
</template>

<script setup>
defineProps({
  clinic: { type: Object, default: null },
  showCheckIn: { type: Boolean, default: false },
})
</script>
