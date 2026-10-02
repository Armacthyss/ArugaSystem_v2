// Phones and small tablets: the sidebar is hidden and opens as a slide-in
// menu from the ☰ button in the top bar. Shared by the Staff / Nurse and the
// Admin / Doctor / Super Admin layouts; the sidebars close it on page change.
import { ref } from 'vue'

export const mobileNavOpen = ref(false)
