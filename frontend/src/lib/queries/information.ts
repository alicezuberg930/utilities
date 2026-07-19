import { mutationOptions, queryOptions } from '@tanstack/react-query'
import type { ApiResponse } from '@/@types'
import { queryClient } from '@/providers/query-client-provider'
import { httpClient } from '../repository/http-client'
import type { WebsiteInformation } from '@/@types/information'

const keys = {
    one: () => ['information'],
    update: () => ['information', 'update'],
}

export const information = () => ({
    one: {
        queryKey: keys.one,
        queryOptions: () =>
            queryOptions({
                queryKey: keys.one(),
                queryFn: async () => {
                    const { data } = await httpClient.get<ApiResponse<WebsiteInformation>>('/information')
                    return data
                },
            }),
    },

    update: {
        mutationKey: keys.update,
        mutationOptions: () =>
            mutationOptions({
                mutationKey: keys.update(),
                mutationFn: async (input: WebsiteInformation) => {
                    return await httpClient.post<ApiResponse>(`/information`, { ...input })
                },
                onSuccess: () => {
                    queryClient().invalidateQueries({ queryKey: keys.one() })
                },
            }),
    },
})
