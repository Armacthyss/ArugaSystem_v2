<!--
  SysAd-Rooms.vue  —  /system-admin/rooms
  The clinic's vaccination rooms (Room 1, Room 2, ...). The admin keeps the
  list matching the real rooms and can put a Doctor or Nurse in each room
  (the Admission Staff can also change this from their dashboard), then the
  staff send patients there.
-->
<script setup>
import { ref, onMounted } from 'vue'
import axios from 'axios'
import { DoorOpen, Pencil, Trash2, Plus, Check, X } from 'lucide-vue-next'
import AppSidebar from './Components/AppSidebar.vue'
import AppHeader from './Components/AppHeader.vue'
import { API_BASE } from '@/utils/format'

const rooms = ref([])
const loading = ref(true)
const message = ref('')
const error = ref('')
const newName = ref('')
const editingId = ref(null)
const editName = ref('')
const healthWorkers = ref([])

function flash(text) {
  message.value = text
  error.value = ''
  setTimeout(() => { if (message.value === text) message.value = '' }, 3500)
}

async function load() {
  loading.value = true
  try {
    rooms.value = (await axios.get(`${API_BASE}/Vaccination/GetRooms`)).data
  } catch (e) {
    error.value = 'Could not load the rooms.'
  } finally {
    loading.value = false
  }
}

// Suggest the next "Room N"
function suggestName() {
  const numbers = rooms.value.map(r => parseInt(String(r.roomName).replace(/\D/g, ''), 10)).filter(n => !isNaN(n))
  return `Room ${numbers.length ? Math.max(...numbers) + 1 : 1}`
}

async function addRoom() {
  error.value = ''
  const name = newName.value.trim() || suggestName()
  try {
    const res = await axios.post(`${API_BASE}/Vaccination/rooms`, { name })
    newName.value = ''
    flash(res.data.message)
    await load()
  } catch (e) {
    error.value = e.response?.data?.message || 'Could not add the room.'
  }
}

function startEdit(room) {
  editingId.value = room.roomId
  editName.value = room.roomName
}

async function saveEdit(room) {
  error.value = ''
  try {
    await axios.put(`${API_BASE}/Vaccination/rooms/${room.roomId}`, { name: editName.value })
    editingId.value = null
    flash('Room renamed.')
    await load()
  } catch (e) {
    error.value = e.response?.data?.message || 'Could not rename the room.'
  }
}

async function removeRoom(room) {
  if (!confirm(`Remove ${room.roomName}? Past visits keep their records.`)) return
  error.value = ''
  try {
    await axios.delete(`${API_BASE}/Vaccination/rooms/${room.roomId}`)
    flash(`${room.roomName} removed.`)
    await load()
  } catch (e) {
    error.value = e.response?.data?.message || 'Could not remove the room.'
  }
}

// Active Doctors and Nurses for the "who's in this room" dropdown.
async function loadHealthWorkers() {
  try {
    const res = await axios.get(`${API_BASE}/Users`, { params: { position: 'Doctor,Nurse' } })
    healthWorkers.value = res.data.filter(u => u.status === 'Active')
  } catch (e) {
    console.error('loadHealthWorkers:', e)
  }
}

// Same endpoint the Admission Staff use; a worker is moved off any other room.
async function setWorker(room, userId) {
  error.value = ''
  try {
    await axios.put(`${API_BASE}/Vaccination/rooms/${room.roomId}/worker`, { userId: userId || null })
    const w = healthWorkers.value.find(x => x.userID === userId)
    flash(w ? `${w.firstName} ${w.lastName} assigned to ${room.roomName}.` : `${room.roomName} has no health worker now.`)
  } catch (e) {
    error.value = e.response?.data?.message || 'Could not update this room.'
  } finally {
    await load()
  }
}

onMounted(() => { load(); loadHealthWorkers() })
</script>

<template>
  <div class="min-h-screen bg-slate-50 flex text-slate-900">
    <AppSidebar />

    <div class="flex-1 min-w-0 flex flex-col">
      <AppHeader title="Vaccination Rooms" breadcrumb="System Administration / Vaccination Rooms" />

      <main class="p-6">
        <div class="max-w-3xl space-y-5">
          <p class="text-sm text-slate-600">
            Keep this list the same as the rooms at Leveriza Health Center and choose which Doctor or Nurse works in
            each room. The Admission Staff can also change this from their dashboard, and send checked-in patients to a room.
          </p>

          <div class="bg-white border border-slate-200 rounded-xl shadow-sm">
            <div class="px-5 py-4 border-b border-slate-200 flex items-center justify-between">
              <h2 class="text-sm font-bold text-slate-900">Rooms ({{ rooms.length }})</h2>
              <form class="flex items-center gap-2" @submit.prevent="addRoom">
                <input
                  v-model="newName"
                  :placeholder="suggestName()"
                  maxlength="50"
                  class="w-40 text-sm rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:bg-white"
                />
                <button type="submit" class="flex items-center gap-1.5 text-sm font-semibold px-3.5 py-2 rounded-lg bg-emerald-600 text-white hover:bg-emerald-700">
                  <Plus class="w-4 h-4" /> Add Room
                </button>
              </form>
            </div>

            <p v-if="loading" class="px-5 py-6 text-sm text-slate-400">Loading…</p>

            <ul v-else class="divide-y divide-slate-100">
              <li v-for="room in rooms" :key="room.roomId" class="px-5 py-3.5 flex flex-wrap sm:flex-nowrap items-center gap-4">
                <div class="w-9 h-9 rounded-lg bg-emerald-50 text-emerald-700 flex items-center justify-center shrink-0">
                  <DoorOpen class="w-4 h-4" />
                </div>

                <div class="flex-1 min-w-0">
                  <form v-if="editingId === room.roomId" class="flex items-center gap-2" @submit.prevent="saveEdit(room)">
                    <input v-model="editName" maxlength="50" class="w-48 text-sm rounded-lg border border-slate-300 px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-emerald-500" />
                    <button type="submit" class="p-1.5 rounded-lg text-emerald-700 hover:bg-emerald-50" aria-label="Save"><Check class="w-4 h-4" /></button>
                    <button type="button" class="p-1.5 rounded-lg text-slate-400 hover:bg-slate-100" aria-label="Cancel" @click="editingId = null"><X class="w-4 h-4" /></button>
                  </form>
                  <template v-else>
                    <p class="text-sm font-semibold text-slate-900">{{ room.roomName }}</p>
                    <p class="text-xs text-slate-500">
                      <span v-if="room.doctorName">Today: {{ room.workerPosition === 'Doctor' ? 'Dr.' : 'Nurse' }} {{ room.doctorName }}</span>
                      <span v-else>No health worker assigned today</span>
                      <span v-if="room.currentPatient"> · with {{ room.currentPatient }}</span>
                    </p>
                  </template>
                </div>

                <select
                  v-if="editingId !== room.roomId"
                  :value="room.workerId || ''"
                  :disabled="!!room.currentQueueId"
                  :title="room.currentQueueId ? 'Finish the current patient before changing the health worker' : 'Health worker in this room'"
                  class="w-52 shrink-0 text-sm rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-slate-700 focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:bg-slate-50 disabled:text-slate-400"
                  @change="setWorker(room, $event.target.value)"
                >
                  <option value="">— No health worker —</option>
                  <option v-for="w in healthWorkers" :key="w.userID" :value="w.userID">
                    {{ w.position === 'Doctor' ? 'Dr.' : 'Nurse' }} {{ w.firstName }} {{ w.lastName }}
                  </option>
                </select>

                <span v-if="room.currentQueueId" class="text-[11px] font-semibold px-2 py-1 rounded-full bg-amber-50 text-amber-700">Busy</span>

                <div v-if="editingId !== room.roomId" class="flex items-center gap-1">
                  <button class="p-1.5 rounded-lg text-slate-500 hover:bg-slate-100" title="Rename" @click="startEdit(room)"><Pencil class="w-4 h-4" /></button>
                  <button class="p-1.5 rounded-lg text-rose-600 hover:bg-rose-50 disabled:opacity-30" title="Remove" :disabled="!!room.currentQueueId" @click="removeRoom(room)"><Trash2 class="w-4 h-4" /></button>
                </div>
              </li>
            </ul>
          </div>

          <p v-if="message" class="text-sm text-emerald-700">{{ message }}</p>
          <p v-if="error" class="text-sm text-rose-600">{{ error }}</p>
        </div>
      </main>
    </div>
  </div>
</template>
