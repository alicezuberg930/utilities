import { mutationOptions, queryOptions } from '@tanstack/react-query'
import type { ApiResponse, RequestLog } from '@/@types'
import { queryClient } from '@/providers/query-client-provider'
import { httpClient } from '../repository/http-client'

const keys = {
    all: () => ['logs'],
    delete: () => ['logs', 'delete'],
}

export const logs = () => ({
    all: {
        queryKey: keys.all,
        queryOptions: () =>
            queryOptions({
                queryKey: keys.all(),
                queryFn: async () => {
                    const { data } = await httpClient.get<ApiResponse<RequestLog[]>>('/logs')
                    return data
                },
            }),
    },

    delete: {
        mutationKey: keys.delete,
        mutationOptions: () =>
            mutationOptions({
                mutationKey: keys.delete(),
                mutationFn: async (id: string) => {
                    return await httpClient.delete<ApiResponse>(`/logs/${id}`)
                },
                onSuccess: () => {
                    queryClient().invalidateQueries({ queryKey: keys.all() })
                },
            }),
    },
})
