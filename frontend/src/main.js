import './assets/main.css'

import { createApp } from 'vue'
import axios from 'axios'
import App from './App.vue'
import router from '@/router'
import { getToken, logout } from '@/utils/auth'
import { API_ORIGIN } from '@/utils/apiBase'
import { digits } from '@/utils/digits'
import { phMobile } from '@/utils/phone'

// Is this URL a call to our API? (API_ORIGIN is empty when the API is reached
// through this site's own /api path — see utils/apiBase.js.)
function isApiUrl(url) {
  try {
    const u = new URL(url, window.location.href)
    return API_ORIGIN
      ? u.href.startsWith(`${API_ORIGIN}/`)
      : u.origin === window.location.origin && u.pathname.startsWith('/api/')
  } catch {
    return false
  }
}

// A sign-in lasts 8 hours. After that the API answers 401 to every call, so
// instead of pages silently showing nothing, go back to the login page and
// say why (then come back to this page after signing in).
function sessionExpired() {
  logout()
  const here = router.currentRoute.value
  if (here.path !== '/') {
    router.push({ path: '/', query: { expired: '1', redirect: here.fullPath } })
  }
}

// Send the signed-in user's token with every API call made through axios.
// The backend requires it (everything except login / Forgot Password), and
// uses it for role checks and for the audit log.
// Calls that already set their own Authorization header are left alone.
axios.interceptors.request.use(config => {
  const token = getToken()
  if (token && !config.headers?.Authorization) {
    config.headers = config.headers || {}
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// (A wrong password on the login page is also a 401, but no token is sent
// there, so it isn't treated as an expired session.)
axios.interceptors.response.use(
  response => response,
  error => {
    const sentToken = !!error?.config?.headers?.Authorization
    if (error?.response?.status === 401 && sentToken && getToken()) sessionExpired()
    return Promise.reject(error)
  }
)

// Some pages call the API with fetch() instead of axios; give those the same
// token and the same expired-session handling.
const nativeFetch = window.fetch.bind(window)
window.fetch = async (input, init = {}) => {
  const url = typeof input === 'string' ? input : input?.url || ''
  const token = getToken()
  const toApi = isApiUrl(url)

  if (token && toApi) {
    const headers = new Headers(init.headers || (typeof input !== 'string' ? input.headers : undefined) || {})
    if (!headers.has('Authorization')) headers.set('Authorization', `Bearer ${token}`)
    init = { ...init, headers }
  }

  const response = await nativeFetch(input, init)
  if (response.status === 401 && token && toApi) sessionExpired()
  return response
}

const app = createApp(App)

app.use(router)
app.directive('digits', digits) // numbers-only inputs, see utils/digits.js
app.directive('ph-mobile', phMobile) // +63 9XX XXX XXXX inputs, see utils/phone.js
app.mount('#app')
