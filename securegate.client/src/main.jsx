import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './theme'   // mavzuni darhol qo'llash (flash bo'lmasligi uchun)
import App from './App.jsx'
import { AuthProvider } from './auth/AuthProvider.jsx'
import ErrorBoundary from './components/ErrorBoundary.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    {/* Eng tashqi chegara — render xatosi oq ekran bermasin */}
    <ErrorBoundary>
      <AuthProvider>
        <App />
      </AuthProvider>
    </ErrorBoundary>
  </StrictMode>,
)
