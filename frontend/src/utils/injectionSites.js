// Where a dose was given (asked for by City Hall, so a swelling can be
// matched to the vaccine given there). Same list as the backend's
// Services/InjectionSites.cs.
export const INJECTION_SITES = ['Left thigh', 'Right thigh', 'Left upper arm', 'Right upper arm', 'Mouth (oral)']

const INJECTED = INJECTION_SITES.filter(s => s !== 'Mouth (oral)')

// Sites that fit the vaccine's route (Vaccines.AdministrationRoute), in the
// order the Nurse most likely wants them: thighs first for intramuscular,
// arms first for intradermal / subcutaneous.
export function sitesForRoute(route) {
  const r = String(route || '').toLowerCase()
  if (r.includes('oral')) return ['Mouth (oral)']
  if (r.includes('intradermal') || r.includes('subcutaneous')) {
    return ['Left upper arm', 'Right upper arm', 'Left thigh', 'Right thigh']
  }
  if (r) return INJECTED
  return INJECTION_SITES
}

// Filled in only when there is one usual answer: by mouth for an oral
// vaccine, the right upper arm for BCG (intradermal). Sides for the other
// injections are the Nurse's pick.
export function defaultSite(route) {
  const r = String(route || '').toLowerCase()
  if (r.includes('oral')) return 'Mouth (oral)'
  if (r.includes('intradermal')) return 'Right upper arm'
  return ''
}

// "Left thigh" -> "L thigh", for tight table cells
export const shortSite = s => (s || '').replace('Left ', 'L ').replace('Right ', 'R ').replace(' (oral)', '')
