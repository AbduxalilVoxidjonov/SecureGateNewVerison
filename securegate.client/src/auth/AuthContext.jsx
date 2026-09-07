// Autentifikatsiya konteksti va `useAuth` hook'i.
// Provider alohida faylda: ./AuthProvider.jsx
import { createContext, useContext } from "react";

export const AuthContext = createContext(null);

/**
 * @returns {{ user: object|null, loading: boolean,
 *             login: (email:string, password:string, rememberMe?:boolean)=>Promise<object>,
 *             logout: ()=>Promise<void>,
 *             hasPermission: (perm?:string)=>boolean }}
 */
export const useAuth = () => useContext(AuthContext);
