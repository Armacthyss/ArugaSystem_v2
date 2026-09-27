import { ref, onMounted, onBeforeUnmount } from 'vue'

// Row "⋮" menus inside tables. The menu is teleported to <body> and placed
// with position: fixed next to its button, so the table's overflow-x-auto /
// the card's overflow-hidden can't cut it off. It opens upward when there
// isn't room below, and closes when the page scrolls or resizes.
//
//   const { openMenuId, menuStyle, toggleMenu, closeMenu } = useFloatingMenu()
//   <button @click.stop="toggleMenu(row.id, $event)">⋮</button>
//   <Teleport to="body"><div v-if="openMenuId === row.id" :style="menuStyle" class="fixed z-50 ...">
export function useFloatingMenu(menuHeight = 220) {
  const openMenuId = ref(null)
  const menuStyle = ref({})

  const closeMenu = () => (openMenuId.value = null)

  const toggleMenu = (id, event) => {
    if (openMenuId.value === id || !event) {
      openMenuId.value = openMenuId.value === id ? null : id
      return
    }
    const rect = event.currentTarget.getBoundingClientRect()
    const right = `${Math.max(8, window.innerWidth - rect.right)}px`
    const fitsBelow = rect.bottom + 6 + menuHeight <= window.innerHeight
    menuStyle.value = fitsBelow
      ? { top: `${rect.bottom + 6}px`, right }
      : { bottom: `${window.innerHeight - rect.top + 6}px`, right }
    openMenuId.value = id
  }

  onMounted(() => {
    window.addEventListener('scroll', closeMenu, true)
    window.addEventListener('resize', closeMenu)
  })
  onBeforeUnmount(() => {
    window.removeEventListener('scroll', closeMenu, true)
    window.removeEventListener('resize', closeMenu)
  })

  return { openMenuId, menuStyle, toggleMenu, closeMenu }
}
