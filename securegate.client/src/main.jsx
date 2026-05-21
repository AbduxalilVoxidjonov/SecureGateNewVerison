import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './theme'   // mavzuni darhol qo'llash (flash bo'lmasligi uchun)
import App from './App.jsx'
import { AuthProvider } from './auth/AuthContext.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <AuthProvider>
      <App />
    </AuthProvider>
  </StrictMode>,
)
