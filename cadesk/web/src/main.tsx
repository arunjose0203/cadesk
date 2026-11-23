import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import PortalUpload from './PortalUpload.tsx'

// /upload/<token>: a client sending files with an upload link (no sign-in)
const upload = location.pathname.match(/^\/upload\/([^/]+)$/)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {upload ? <PortalUpload token={decodeURIComponent(upload[1])} /> : <App />}
  </StrictMode>,
)

// Register PWA service worker in supported browsers
if ('serviceWorker' in navigator && (import.meta.env.PROD || window.location.protocol === 'https:')) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch((err) => {
      console.warn('CA Desk PWA service worker registration skipped/failed:', err)
    })
  })
}
