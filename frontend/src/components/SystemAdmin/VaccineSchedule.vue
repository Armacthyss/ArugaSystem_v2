<template>
  <div class="min-h-screen bg-gray-50 p-6">

    <!-- Header -->
    <div class="mb-6">
      <h1 class="text-3xl font-bold text-gray-800">
        Child Vaccination Timeline & Record
      </h1>

      <p class="mt-1 text-gray-500">
        Search for a child to view their vaccination timeline and vaccination records.
      </p>
    </div>

    <!-- Search -->
    <div class="mb-6 rounded-xl bg-white p-5 shadow">

      <div class="flex flex-col gap-3 md:flex-row md:items-end">

        <div class="flex-1">
          <label class="mb-2 block text-sm font-semibold text-gray-700">
            Child ID
          </label>

          <input
            v-model="childIdInput"
            type="text"
            placeholder="Enter Child ID"
            class="w-full rounded-lg border border-gray-300 px-4 py-3 outline-none focus:border-green-500 focus:ring-2 focus:ring-green-100"
            @keyup.enter="searchChild"
          />
        </div>

        <button
          @click="searchChild"
          :disabled="loading"
          class="rounded-lg bg-green-600 px-6 py-3 font-semibold text-white hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {{ loading ? 'Searching...' : 'Search Child' }}
        </button>

      </div>

      <p
        v-if="searchError"
        class="mt-3 text-sm font-medium text-red-600"
      >
        {{ searchError }}
      </p>

    </div>

    <!-- Empty state -->
    <div
      v-if="!child && !loading && !searchError"
      class="rounded-xl bg-white p-12 text-center shadow"
    >
      <div class="mb-3 text-4xl">
        👶
      </div>

      <h2 class="text-lg font-semibold text-gray-700">
        Search for a child
      </h2>

      <p class="mt-1 text-sm text-gray-500">
        Enter a Child ID above to view the vaccination timeline and records.
      </p>
    </div>

    <!-- Loading -->
    <div
      v-if="loading"
      class="rounded-xl bg-white p-10 text-center shadow"
    >
      <p class="text-gray-500">
        Loading child vaccination data...
      </p>
    </div>

    <template v-if="child && !loading">

      <!-- Child information -->
      <div class="mb-6 rounded-xl bg-white p-5 shadow">

        <div class="mb-4 flex flex-col gap-2 md:flex-row md:items-center md:justify-between">

          <div>
            <h2 class="text-xl font-bold text-gray-800">
              {{ childFullName }}
            </h2>

            <p class="text-sm text-gray-500">
              Child ID: {{ child.childID }}
            </p>
          </div>

          <div class="flex gap-2">

            <button
              @click="generateTimeline"
              :disabled="actionLoading"
              class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white hover:bg-green-700 disabled:opacity-50"
            >
              Generate Timeline
            </button>

            <button
              @click="regenerateTimeline"
              :disabled="actionLoading"
              class="rounded-lg border border-orange-500 px-4 py-2 text-sm font-semibold text-orange-600 hover:bg-orange-50 disabled:opacity-50"
            >
              Regenerate Timeline
            </button>

          </div>

        </div>

        <div class="grid grid-cols-1 gap-4 md:grid-cols-4">

          <div class="rounded-lg bg-gray-50 p-4">
            <p class="text-xs font-semibold uppercase text-gray-500">
              Birth Date
            </p>

            <p class="mt-1 font-semibold text-gray-800">
              {{ formatDate(child.birthDate) }}
            </p>
          </div>

          <div class="rounded-lg bg-gray-50 p-4">
            <p class="text-xs font-semibold uppercase text-gray-500">
              Age
            </p>

            <p class="mt-1 font-semibold text-gray-800">
              {{ childAge }}
            </p>
          </div>

          <div class="rounded-lg bg-gray-50 p-4">
            <p class="text-xs font-semibold uppercase text-gray-500">
              Timeline Entries
            </p>

            <p class="mt-1 font-semibold text-gray-800">
              {{ timeline.length }}
            </p>
          </div>

          <div class="rounded-lg bg-gray-50 p-4">
            <p class="text-xs font-semibold uppercase text-gray-500">
              Completed
            </p>

            <p class="mt-1 font-semibold text-green-600">
              {{ completedCount }}
            </p>
          </div>

        </div>

      </div>

      <!-- Action message -->
      <div
        v-if="actionMessage"
        class="mb-5 rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm font-medium text-green-700"
      >
        {{ actionMessage }}
      </div>

      <!-- Error -->
      <div
        v-if="dataError"
        class="mb-5 rounded-lg border border-red-200 bg-red-50 p-4"
      >
        <p class="font-semibold text-red-700">
          Unable to load vaccination data
        </p>

        <p class="mt-1 text-sm text-red-600">
          {{ dataError }}
        </p>
      </div>

      <!-- Combined Timeline -->
      <div class="overflow-hidden rounded-xl bg-white shadow">

        <div class="border-b border-gray-200 px-5 py-4">
          <h2 class="text-xl font-bold text-gray-800">
            Vaccination Timeline
          </h2>

          <p class="mt-1 text-sm text-gray-500">
            Vaccination visits are grouped by Sequence Order.
            Vaccination records are shown together with the expected timeline.
          </p>
        </div>

        <div class="overflow-x-auto">

          <table class="min-w-max w-full border-collapse">

            <thead>
              <tr>

                <th
                  class="sticky left-0 z-20 min-w-57.5 border border-gray-200 bg-orange-400 px-5 py-4 text-left font-bold text-gray-900"
                >
                  Vaccine
                </th>

                <th
                  class="min-w-[200px] border border-gray-200 bg-orange-400 px-5 py-4 text-left font-bold text-gray-900"
                >
                  Disease
                </th>

                <th
                  v-for="visit in visits"
                  :key="visit.sequence"
                  class="min-w-[220px] border border-gray-200 bg-orange-400 px-4 py-4 text-center font-bold text-gray-900"
                >
                  <div>
                    {{ visit.label }}
                  </div>

                  <div
                    v-if="visit.ageLabels.length"
                    class="mt-1 text-xs font-medium text-gray-700"
                  >
                    {{ visit.ageLabels.join(' / ') }}
                  </div>
                </th>

              </tr>
            </thead>

            <tbody>

              <tr
                v-for="row in scheduleRows"
                :key="row.vaccineID"
                class="hover:bg-gray-50"
              >

                <!-- Vaccine -->
                <td
                  class="sticky left-0 z-10 border border-gray-200 bg-white px-5 py-4"
                >
                  <div class="font-semibold text-gray-800">
                    {{ row.vaccineName }}
                  </div>

                  <div class="mt-1 text-xs text-gray-400">
                    Vaccine ID: {{ row.vaccineID }}
                  </div>
                </td>

                <!-- Disease -->
                <td
                  class="border border-gray-200 px-5 py-4 text-gray-700"
                >
                  {{ row.targetDisease || '—' }}
                </td>

                <!-- Visits -->
                <td
                  v-for="visit in visits"
                  :key="`${row.vaccineID}-${visit.sequence}`"
                  class="border border-gray-200 px-3 py-3 align-top"
                >

                  <div
                    v-if="getEntries(row.vaccineID, visit.sequence).length"
                    class="space-y-2"
                  >

                    <div
                      v-for="entry in getEntries(
                        row.vaccineID,
                        visit.sequence
                      )"
                      :key="entry.timelineID"
                      class="rounded-lg border p-3"
                      :class="statusClasses(entry.status)"
                    >

                      <!-- Dose -->
                      <div class="flex items-center justify-between">

                        <span class="text-sm font-bold">
                          Dose {{ entry.doseNumber }}
                        </span>

                        <span
                          class="rounded-full px-2 py-1 text-xs font-semibold"
                        >
                          {{ entry.status }}
                        </span>

                      </div>

                      <!-- Expected -->
                      <div class="mt-2 text-xs">
                        <span class="font-semibold">
                          Expected:
                        </span>

                        {{ formatDate(entry.expectedDate) }}
                      </div>

                      <!-- Scheduled -->
                      <div class="mt-1 text-xs">
                        <span class="font-semibold">
                          Scheduled:
                        </span>

                        {{ formatDate(entry.scheduledDate) }}
                      </div>

                      <!-- Recommended age -->
                      <div
                        v-if="entry.recommendedAgeDays !== null"
                        class="mt-1 text-xs"
                      >
                        <span class="font-semibold">
                          Recommended:
                        </span>

                        {{ formatRecommendedAge(entry.recommendedAgeDays) }}
                      </div>

                      <!-- Record -->
                      <div class="mt-2 border-t border-current/10 pt-2 text-xs">

                        <template v-if="entry.vaccinationRecordID">

                          <div class="font-semibold text-green-700">
                            ✓ Vaccination Record Found
                          </div>

                          <div class="mt-1">
                            Record ID:
                            {{ entry.vaccinationRecordID }}
                          </div>

                        </template>

                        <template v-else>

                          <div class="text-gray-500">
                            No vaccination record linked
                          </div>

                        </template>

                      </div>

                    </div>

                  </div>

                  <div
                    v-else
                    class="py-5 text-center text-gray-300"
                  >
                    —
                  </div>

                </td>

              </tr>

              <!-- No timeline -->
              <tr v-if="scheduleRows.length === 0">

                <td
                  :colspan="2 + visits.length"
                  class="px-6 py-12 text-center text-gray-500"
                >
                  No vaccination timeline entries were found for this child.
                </td>

              </tr>

            </tbody>

          </table>

        </div>

        <!-- Footer -->
        <div
          class="border-t border-gray-200 bg-gray-50 px-5 py-4 text-sm text-gray-500"
        >
          Visit columns are determined by the backend
          <strong>SequenceOrder</strong>.
          Age labels are informational only.
        </div>

      </div>

      <!-- Raw records -->
      <div class="mt-6 overflow-hidden rounded-xl bg-white shadow">

        <div class="border-b border-gray-200 px-5 py-4">

          <h2 class="text-xl font-bold text-gray-800">
            Vaccination Records
          </h2>

          <p class="mt-1 text-sm text-gray-500">
            Actual vaccination records associated with this child.
          </p>

        </div>

        <div class="overflow-x-auto">

          <table class="min-w-full border-collapse">

            <thead>
              <tr>

                <th class="border border-gray-200 bg-gray-100 px-4 py-3 text-left text-sm">
                  Vaccine ID
                </th>

                <th class="border border-gray-200 bg-gray-100 px-4 py-3 text-left text-sm">
                  Dose
                </th>

                <th class="border border-gray-200 bg-gray-100 px-4 py-3 text-left text-sm">
                  Vaccination Date
                </th>

                <th class="border border-gray-200 bg-gray-100 px-4 py-3 text-left text-sm">
                  Status
                </th>

                <th class="border border-gray-200 bg-gray-100 px-4 py-3 text-left text-sm">
                  Record ID
                </th>

              </tr>
            </thead>

            <tbody>

              <tr
                v-for="record in vaccinationRecords"
                :key="record.vaccinationRecordID"
                class="hover:bg-gray-50"
              >

                <td class="border border-gray-200 px-4 py-3 text-sm">
                  {{ record.vaccineID }}
                </td>

                <td class="border border-gray-200 px-4 py-3 text-sm">
                  Dose {{ record.doseNumber }}
                </td>

                <td class="border border-gray-200 px-4 py-3 text-sm">
                  {{
                    formatDate(
                      record.vaccinationDate ||
                      record.dateAdministered ||
                      record.createdAt
                    )
                  }}
                </td>

                <td class="border border-gray-200 px-4 py-3 text-sm">
                  <span
                    class="rounded-full bg-green-100 px-3 py-1 text-xs font-semibold text-green-700"
                  >
                    {{ record.status || 'Completed' }}
                  </span>
                </td>

                <td class="border border-gray-200 px-4 py-3 text-xs text-gray-500">
                  {{ record.vaccinationRecordID }}
                </td>

              </tr>

              <tr v-if="vaccinationRecords.length === 0">

                <td
                  colspan="5"
                  class="px-6 py-10 text-center text-gray-500"
                >
                  No vaccination records found for this child.
                </td>

              </tr>

            </tbody>

          </table>

        </div>

      </div>

    </template>

  </div>
</template>


<script setup>

import { API_ORIGIN } from '@/utils/apiBase'
import { ref, computed } from 'vue'
import axios from 'axios'

const API_BASE = `${API_ORIGIN}/api`

/*
|--------------------------------------------------------------------------
| API endpoints
|--------------------------------------------------------------------------
|
| If your existing controller uses different GET routes,
| change ONLY these endpoint functions.
|
*/

const childEndpoint = (childId) =>
  `${API_BASE}/Children/${childId}`

const timelineEndpoint = (childId) =>
  `${API_BASE}/VaccinationTimeline/child/${childId}`

const recordsEndpoint = (childId) =>
  `${API_BASE}/VaccinationRecords/child/${childId}`

const generateEndpoint = (childId) =>
  `${API_BASE}/VaccinationTimeline/generate/${childId}`

const regenerateEndpoint = (childId) =>
  `${API_BASE}/VaccinationTimeline/regenerate/${childId}`


/*
|--------------------------------------------------------------------------
| State
|--------------------------------------------------------------------------
*/

const childIdInput = ref('')

const child = ref(null)

const timeline = ref([])

const vaccinationRecords = ref([])

const loading = ref(false)

const actionLoading = ref(false)

const searchError = ref(null)

const dataError = ref(null)

const actionMessage = ref(null)


/*
|--------------------------------------------------------------------------
| Child name
|--------------------------------------------------------------------------
*/

const childFullName = computed(() => {

  if (!child.value) {
    return 'Unknown Child'
  }

  const parts = [
    child.value.firstName,
    child.value.middleName,
    child.value.lastName
  ].filter(Boolean)

  return parts.join(' ') || 'Unknown Child'

})


/*
|--------------------------------------------------------------------------
| Child age
|--------------------------------------------------------------------------
*/

const childAge = computed(() => {

  if (!child.value?.birthDate) {
    return '—'
  }

  const birth = new Date(child.value.birthDate)

  const today = new Date()

  let years =
    today.getFullYear() -
    birth.getFullYear()

  const birthdayThisYear =
    new Date(
      today.getFullYear(),
      birth.getMonth(),
      birth.getDate()
    )

  if (today < birthdayThisYear) {
    years--
  }

  if (years > 0) {
    return `${years} year${years === 1 ? '' : 's'}`
  }

  const diffDays = Math.floor(
    (today - birth) /
    (1000 * 60 * 60 * 24)
  )

  if (diffDays < 7) {
    return `${diffDays} day${diffDays === 1 ? '' : 's'}`
  }

  const weeks = Math.floor(diffDays / 7)

  return `${weeks} week${weeks === 1 ? '' : 's'}`

})


/*
|--------------------------------------------------------------------------
| Search child
|--------------------------------------------------------------------------
*/

const searchChild = async () => {

  const childId = childIdInput.value.trim()

  if (!childId) {

    searchError.value =
      'Please enter a Child ID.'

    return

  }

  loading.value = true

  searchError.value = null

  dataError.value = null

  actionMessage.value = null

  child.value = null

  timeline.value = []

  vaccinationRecords.value = []

  try {

    const childResponse =
      await axios.get(
        childEndpoint(childId)
      )

    child.value =
      childResponse.data

    /*
    |--------------------------------------------------------------------------
    | Load timeline + records
    |--------------------------------------------------------------------------
    */

    const results =
      await Promise.all([
        axios.get(
          timelineEndpoint(childId)
        ),
        axios.get(
          recordsEndpoint(childId)
        )
      ])

    timeline.value =
      Array.isArray(results[0].data)
        ? results[0].data
        : []

    vaccinationRecords.value =
      Array.isArray(results[1].data)
        ? results[1].data
        : []

  } catch (err) {

    console.error(
      'Failed to load child vaccination data:',
      err
    )

    searchError.value =
      err.response?.data?.message ||
      err.message ||
      'Child could not be found.'

  } finally {

    loading.value = false

  }

}


/*
|--------------------------------------------------------------------------
| Visit columns
|--------------------------------------------------------------------------
|
| IMPORTANT:
|
| We use SequenceOrder.
|
| We DO NOT use age to determine the column.
|
*/

const visits = computed(() => {

  const visitMap = new Map()

  timeline.value.forEach(entry => {

    const sequence =
      entry.sequenceOrder ??
      entry.sequence ??
      entry.visitSequence

    if (sequence === null || sequence === undefined) {
      return
    }

    if (!visitMap.has(sequence)) {

      visitMap.set(sequence, {
        sequence,
        ageLabels: new Set()
      })

    }

    if (
      entry.recommendedAgeDays !== null &&
      entry.recommendedAgeDays !== undefined
    ) {

      visitMap
        .get(sequence)
        .ageLabels
        .add(
          formatRecommendedAge(
            entry.recommendedAgeDays
          )
        )

    }

  })

  return Array.from(
    visitMap.values()
  )
    .sort(
      (a, b) =>
        a.sequence -
        b.sequence
    )
    .map(visit => ({

      ...visit,

      label:
        getVisitLabel(
          visit.sequence
        ),

      ageLabels:
        Array.from(
          visit.ageLabels
        )

    }))

})


/*
|--------------------------------------------------------------------------
| Visit label
|--------------------------------------------------------------------------
*/

const getVisitLabel = (sequence) => {

  const labels = [
    'First Visit',
    'Second Visit',
    'Third Visit',
    'Fourth Visit',
    'Fifth Visit',
    'Sixth Visit',
    'Seventh Visit',
    'Eighth Visit',
    'Ninth Visit',
    'Tenth Visit'
  ]

  return (
    labels[sequence - 1] ||
    `Visit ${sequence}`
  )

}


/*
|--------------------------------------------------------------------------
| Vaccine rows
|--------------------------------------------------------------------------
|
| Each vaccine appears ONCE.
|
| This prevents duplicate vaccine rows.
|
*/

const scheduleRows = computed(() => {

  const vaccineMap = new Map()

  timeline.value.forEach(entry => {

    const vaccineID =
      entry.vaccineID

    if (!vaccineMap.has(vaccineID)) {

      vaccineMap.set(vaccineID, {

        vaccineID,

        vaccineName:
          entry.vaccine?.vaccineName ||
          entry.vaccineName ||
          `Vaccine ${vaccineID}`,

        targetDisease:
          entry.vaccine?.targetDisease ||
          entry.targetDisease ||
          '—'

      })

    }

  })

  return Array.from(
    vaccineMap.values()
  )

})


/*
|--------------------------------------------------------------------------
| Get timeline entries for vaccine + visit
|--------------------------------------------------------------------------
*/

const getEntries = (
  vaccineID,
  sequence
) => {

  return timeline.value
    .filter(entry => {

      const entrySequence =
        entry.sequenceOrder ??
        entry.sequence ??
        entry.visitSequence

      return (
        entry.vaccineID === vaccineID &&
        entrySequence === sequence
      )

    })
    .sort(
      (a, b) =>
        a.doseNumber -
        b.doseNumber
    )

}


/*
|--------------------------------------------------------------------------
| Status styling
|--------------------------------------------------------------------------
*/

const statusClasses = (status) => {

  switch (
    String(status || '').toLowerCase()
  ) {

    case 'completed':

      return 'border-green-200 bg-green-50 text-green-800'

    case 'missed':

      return 'border-red-200 bg-red-50 text-red-800'

    case 'pending':

      return 'border-blue-200 bg-blue-50 text-blue-800'

    case 'cancelled':

      return 'border-gray-300 bg-gray-100 text-gray-600'

    default:

      return 'border-gray-200 bg-gray-50 text-gray-700'

  }

}


/*
|--------------------------------------------------------------------------
| Completed count
|--------------------------------------------------------------------------
*/

const completedCount = computed(() => {

  return timeline.value.filter(
    entry =>
      String(entry.status || '')
        .toLowerCase() === 'completed'
  ).length

})


/*
|--------------------------------------------------------------------------
| Recommended age
|--------------------------------------------------------------------------
|
| This is DISPLAY ONLY.
|
| It cannot affect visit grouping.
|
*/

const formatRecommendedAge = (days) => {

  if (
    days === null ||
    days === undefined
  ) {
    return '—'
  }

  const totalDays =
    Number(days)

  if (!Number.isFinite(totalDays)) {
    return '—'
  }

  if (totalDays === 0) {
    return 'At Birth'
  }

  /*
  |--------------------------------------------------------------------------
  | Exact weeks
  |--------------------------------------------------------------------------
  |
  | Only call something a week when it is
  | actually divisible by 7.
  |
  */

  if (
    totalDays % 7 === 0 &&
    totalDays < 365
  ) {

    const weeks =
      totalDays / 7

    return `${weeks} Week${weeks === 1 ? '' : 's'}`

  }

  /*
  |--------------------------------------------------------------------------
  | Days
  |--------------------------------------------------------------------------
  |
  | Do NOT round small values into incorrect
  | month/week values.
  |
  */

  if (totalDays < 30) {

    return `${totalDays} Day${totalDays === 1 ? '' : 's'}`

  }

  /*
  |--------------------------------------------------------------------------
  | Months
  |--------------------------------------------------------------------------
  */

  if (totalDays < 365) {

    const months =
      Math.round(
        totalDays / 30.4375
      )

    return `${months} Month${months === 1 ? '' : 's'}`

  }

  /*
  |--------------------------------------------------------------------------
  | Years
  |--------------------------------------------------------------------------
  */

  const years =
    Math.floor(
      totalDays / 365
    )

  const remainingDays =
    totalDays % 365

  if (remainingDays === 0) {

    return `${years} Year${years === 1 ? '' : 's'}`

  }

  return `${years} Year${years === 1 ? '' : 's'} + ${remainingDays} Days`

}


/*
|--------------------------------------------------------------------------
| Date formatting
|--------------------------------------------------------------------------
*/

const formatDate = (date) => {

  if (!date) {
    return '—'
  }

  const parsed =
    new Date(date)

  if (
    Number.isNaN(
      parsed.getTime()
    )
  ) {
    return '—'
  }

  return parsed.toLocaleDateString(
    'en-US',
    {
      month: 'short',
      day: 'numeric',
      year: 'numeric'
    }
  )

}


/*
|--------------------------------------------------------------------------
| Generate timeline
|--------------------------------------------------------------------------
*/

const generateTimeline = async () => {

  if (!child.value?.childID) {
    return
  }

  actionLoading.value = true

  actionMessage.value = null

  try {

    const response =
      await axios.post(
        generateEndpoint(
          child.value.childID
        )
      )

    actionMessage.value =
      response.data?.message ||
      'Vaccination timeline generated successfully.'

    await searchChild()

  } catch (err) {

    console.error(
      'Failed to generate timeline:',
      err
    )

    actionMessage.value =
      err.response?.data?.message ||
      err.message ||
      'Failed to generate timeline.'

  } finally {

    actionLoading.value = false

  }

}


/*
|--------------------------------------------------------------------------
| Regenerate timeline
|--------------------------------------------------------------------------
*/

const regenerateTimeline = async () => {

  if (!child.value?.childID) {
    return
  }

  actionLoading.value = true

  actionMessage.value = null

  try {

    const response =
      await axios.post(
        regenerateEndpoint(
          child.value.childID
        )
      )

    actionMessage.value =
      response.data?.message ||
      'Vaccination timeline regenerated.'

    await searchChild()

  } catch (err) {

    console.error(
      'Failed to regenerate timeline:',
      err
    )

    actionMessage.value =
      err.response?.data?.message ||
      err.message ||
      'Failed to regenerate timeline.'

  } finally {

    actionLoading.value = false

  }

}

</script> 