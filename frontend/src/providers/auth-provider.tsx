import {
    createContext,
    useCallback,
    useContext,
    useEffect,
    useMemo,
    useState,
    type ReactNode,
} from 'react'
import type { AuthUser, LoginParams } from '@/@types/auth'
import { getProfile, login as loginRequest } from '@/services/api.service'

type AuthContextValue = {
    user: AuthUser | null
    isAuthenticated: boolean
    isLoading: boolean
    isLoggingIn: boolean
    login: (params: LoginParams) => Promise<AuthUser>
    refreshProfile: () => Promise<AuthUser | null>
    clearUser: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const [user, setUser] = useState<AuthUser | null>(null)
    const [isLoading, setIsLoading] = useState(true)
    const [isLoggingIn, setIsLoggingIn] = useState(false)

    const refreshProfile = useCallback(async () => {
        const response = await getProfile()
        const profile = response.data ?? null
        setUser(profile)
        return profile
    }, [])

    const login = useCallback(async (params: LoginParams) => {
        setIsLoggingIn(true)
        try {
            const response = await loginRequest(params)
            const loggedInUser = response.data?.user
            if (!loggedInUser) throw new Error('Không lấy được thông tin người dùng')
            setUser(loggedInUser)
            return loggedInUser
        } finally {
            setIsLoggingIn(false)
        }
    }, [])

    const clearUser = useCallback(() => {
        setUser(null)
    }, [])

    useEffect(() => {
        let isMounted = true

        const loadProfile = async () => {
            try {
                const response = await getProfile()
                if (isMounted) setUser(response.data ?? null)
            } catch {
                if (isMounted) setUser(null)
            } finally {
                if (isMounted) setIsLoading(false)
            }
        }

        void loadProfile()

        return () => {
            isMounted = false
        }
    }, [])

    const value = useMemo<AuthContextValue>(() => ({
        user,
        isAuthenticated: !!user,
        isLoading,
        isLoggingIn,
        login,
        refreshProfile,
        clearUser,
    }), [clearUser, isLoading, isLoggingIn, login, refreshProfile, user])

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export const useAuth = () => {
    const context = useContext(AuthContext)
    if (!context) throw new Error('useAuth must be used within AuthProvider')
    return context
}