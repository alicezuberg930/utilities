import { mutationOptions } from '@tanstack/react-query'
import { type ApiResponse, } from '@/@types'
import { loginFormSchema } from '@/lib/validators/auth-validator'
import { httpClient } from '../repository/http-client'
import type z from 'zod'
import { toast } from 'sonner'

const keys = {
    login: () => ['auth', 'login'],
}

export const auth = () => ({
    login: {
        mutationKey: keys.login(),
        mutationOptions: () =>
            mutationOptions({
                mutationKey: keys.login(),
                mutationFn: async (input: z.infer<typeof loginFormSchema>) => {
                    return await httpClient.post<ApiResponse>(
                        '/auth/login',
                        input
                    )
                },
            }),
    },
})