import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { createBanner, deleteBanner, getBannerDetails, getBanners, updateBanner } from "../services/api.service"
import { toast } from "sonner"
import { API } from "../lib/api"
import { showResponseError } from "../lib/utils"
import type {
    BannerFilter,
    BannerListResponse,
    BannerDetailsResponse,
    BannerMutationResponse,
    CreateBannerParams,
    DeleteBannerParams,
    GetBannerDetailsParams,
    UpdateBannerParams,
} from "@/@types/banner"

export const useGetBannersHook = (params?: BannerFilter) => {
    return useQuery<BannerListResponse>({
        queryKey: [API.BANNERS, params],
        queryFn: () => getBanners(params),
        placeholderData: (previousData, _) => previousData,
    })
}

export const useDeleteBannerHook = () => {
    const queryClient = useQueryClient()
    return useMutation<BannerMutationResponse, unknown, DeleteBannerParams>({
        mutationFn: ({ id }) => deleteBanner({ id }),
        onSuccess(data) {
            toast.success(data.message)
            queryClient.invalidateQueries({ queryKey: [API.BANNERS] })
        },
        onError(error) {
            showResponseError(error)
        },
    })
}

export const useCreateBannerHook = () => {
    const queryClient = useQueryClient()
    return useMutation<BannerMutationResponse, unknown, CreateBannerParams>({
        mutationFn: ({ banner }) => createBanner({ banner }),
        onSuccess(data) {
            toast.success(data.message)
            queryClient.invalidateQueries({ queryKey: [API.BANNERS] })
        },
        onError(error) {
            showResponseError(error)
        },
    })
}

export const useUpdateBannerHook = () => {
    const queryClient = useQueryClient()
    return useMutation<BannerMutationResponse, unknown, UpdateBannerParams>({
        mutationFn: ({ id, banner }) => updateBanner({ id, banner }),
        onSuccess(data) {
            toast.success(data.message)
            queryClient.invalidateQueries({ queryKey: [API.BANNERS] })
        },
        onError(error) {
            showResponseError(error)
        },
    })
}

export const useGetBannerDetailsHook = ({ id }: GetBannerDetailsParams) => {
    return useQuery<BannerDetailsResponse>({
        queryKey: [API.BANNERS, id],
        queryFn: () => getBannerDetails({ id }),
        enabled: Boolean(id),
        placeholderData: (previousData, _) => previousData,
    })
}
