import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { createEvent, deleteEvent, getEvents, updateEvent } from "../services/api.service"
import { toast } from "sonner"
import { API } from "../lib/api"
import { showResponseError } from "../lib/utils"
import type {
    CreateEventParams,
    DeleteEventParams,
    EventListResponse,
    EventMutationResponse,
    GetEventsParams,
    UpdateEventParams,
} from "@/@types/event"

export const useGetEventsHook = ({ filter }: GetEventsParams = {}) => {
    return useQuery<EventListResponse>({
        queryKey: [API.EVENTS, filter],
        queryFn: () => getEvents({ filter }),
        placeholderData: (previousData, _) => previousData,
    })
}

export const useDeleteEventHook = () => {
    const queryClient = useQueryClient()
    return useMutation<EventMutationResponse, unknown, DeleteEventParams>({
        mutationFn: ({ id }) => deleteEvent({ id }),
        onSuccess(data) {
            toast.success(data.message)
            queryClient.invalidateQueries({ queryKey: [API.EVENTS] })
        },
        onError(error) {
            showResponseError(error)
        },
    })
}

export const useCreateEventHook = () => {
    const queryClient = useQueryClient()
    return useMutation<EventMutationResponse, unknown, CreateEventParams>({
        mutationFn: ({ event }) => createEvent({ event }),
        onSuccess(data) {
            toast.success(data.message)
            queryClient.invalidateQueries({ queryKey: [API.EVENTS] })
        },
        onError(error) {
            showResponseError(error)
        },
    })
}

export const useUpdateEventHook = () => {
    const queryClient = useQueryClient()
    return useMutation<EventMutationResponse, unknown, UpdateEventParams>({
        mutationFn: ({ id, event }) => updateEvent({ id, event }),
        onSuccess(data) {
            toast.success(data.message)
            queryClient.invalidateQueries({ queryKey: [API.EVENTS] })
        },
        onError(error) {
            showResponseError(error)
        },
    })
}
