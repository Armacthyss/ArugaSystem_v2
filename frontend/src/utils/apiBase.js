// Where the frontend reaches the API.
//
// Empty (the default) means "the same address this page was opened from":
// the Vite dev server passes every /api/... request on to the ASP.NET API
// (see vite.config.js). That way Aruga works the same on this computer
// (http://localhost:5173) and on a phone on the same Wi-Fi
// (http://192.168.x.x:5173) — a hard-coded "localhost" would point a phone
// at itself. Set VITE_API_URL to use an API hosted somewhere else.
export const API_ORIGIN = (import.meta.env.VITE_API_URL || '').replace(/\/$/, '')
export const API_BASE = `${API_ORIGIN}/api`
