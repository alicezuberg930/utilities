import { Navigate } from '@tanstack/react-router'
import type { ReactNode } from 'react'
import { useAuth } from '@/providers/auth-provider'

type AuthGuardProps = {
    children: ReactNode
}

export const AuthGuard = ({ children }: AuthGuardProps) => {
    const { isAuthenticated, isLoading } = useAuth()

    if (isLoading) return

    if (!isAuthenticated) {
        return <Navigate to="/login" replace />
    }

    return <>{children}</>
}
