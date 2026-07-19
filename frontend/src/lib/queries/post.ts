import { mutationOptions, queryOptions } from '@tanstack/react-query'
import type { ApiResponse, Post, PostFilter, PostPayload } from '@/@types'
import { queryClient } from '@/providers/query-client-provider'
import { httpClient } from '../repository/http-client'

const keys = {
    all: (opts: PostFilter) => ['posts', opts],
    one: (id: string) => ['posts', id],
    create: () => ['posts', 'create'],
    update: () => ['posts', 'update'],
    delete: () => ['posts', 'delete'],
}

export const posts = () => ({
    all: {
        queryKey: keys.all,
        queryOptions: (opts: PostFilter = {}) =>
            queryOptions({
                queryKey: keys.all(opts),
                queryFn: async () => {
                    const { data } = await httpClient.get<ApiResponse<Post[]>>('/posts', opts)
                    return data
                },
            }),
    },

    one: {
        queryKey: keys.one,
        queryOptions: (id: string) =>
            queryOptions({
                queryKey: keys.one(id),
                queryFn: async () => {
                    const { data } = await httpClient.get<ApiResponse<Post>>(`/posts/${id}`)
                    return data
                },
            }),
    },

    create: {
        mutationKey: keys.create,
        mutationOptions: () =>
            mutationOptions({
                mutationKey: keys.create(),
                mutationFn: async ({ ...input }: PostPayload) => {
                    return await httpClient.post<ApiResponse>('/posts', input)
                },
                onSuccess: () => {
                    queryClient().invalidateQueries({ queryKey: keys.all({}) })
                },
            }),
    },

    update: {
        mutationKey: keys.update,
        mutationOptions: () =>
            mutationOptions({
                mutationKey: keys.update(),
                mutationFn: async ({ id, ...input }: PostPayload & { id: string }) => {
                    return await httpClient.put<ApiResponse>(`/posts/${id}`, input)
                },
                onSuccess: () => {
                    queryClient().invalidateQueries({ queryKey: keys.all({}) })
                },
            }),
    },

    delete: {
        mutationKey: keys.delete,
        mutationOptions: () =>
            mutationOptions({
                mutationKey: keys.delete(),
                mutationFn: async (id: string) => {
                    return await httpClient.delete<ApiResponse>(`/posts/${id}`)
                },
                onSuccess: () => {
                    queryClient().invalidateQueries({ queryKey: keys.all({}) })
                },
            }),
    },
})
