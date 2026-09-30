import { API_ORIGIN } from '@/utils/apiBase'
import axios from "axios"
import router from "@/router"
import { logout } from "@/utils/auth"

const api = axios.create({
  baseURL: `${API_ORIGIN}/api`
})

api.interceptors.request.use(config => {
  const token = localStorage.getItem("authToken")

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

// Expired sign-in (8 hours): back to the login page, then back here after
api.interceptors.response.use(
  response => response,
  error => {
    if (error?.response?.status === 401 && localStorage.getItem("authToken")) {
      logout()
      const here = router.currentRoute.value
      if (here.path !== "/") router.push({ path: "/", query: { expired: "1", redirect: here.fullPath } })
    }
    return Promise.reject(error)
  }
)

export default api
