import { httpClient } from '@/lib/repository/http-client'
import type {
    LoginParams,
    LoginResponse,
    ProfileResponse,
} from '@/@types/auth'
import type {
    BannerDetailsResponse,
    BannerListResponse,
    BannerMutationResponse,
    CreateBannerParams,
    DeleteBannerParams,
    GetBannerDetailsParams,
    GetBannersParams,
    UpdateBannerParams,
} from '@/@types/banner'
import type {
    CreateEventParams,
    DeleteEventParams,
    EventListResponse,
    EventMutationResponse,
    GetEventsParams,
    UpdateEventParams,
} from '@/@types/event'
import type { UploadFileParams, UploadFileResponse } from '@/@types/file'
import type { FileConversionFormat, FileConversionJobResponse } from '@/@types/file-conversion'
import type {
    CreateYoutubeJobRequest,
    YoutubeJobResponse,
    YoutubeMetadataResponse,
} from '@/@types/youtube'
import { API } from '../lib/api'
import { getApiUrl } from '@/lib/repository/http-client'

// auth
export const login = async (params: LoginParams) => {
    return httpClient.post<LoginResponse>(API.AUTH.LOGIN, params)
}

export const getProfile = async () => {
    return httpClient.get<ProfileResponse>(API.USERS.PROFILE)
}

// common
export const uploadFile = async ({ file }: UploadFileParams) => {
    return httpClient.post<UploadFileResponse>(API.UPLOAD_FILE, file)
}

// banner
export const createBanner = async ({ banner }: CreateBannerParams) => {
    return httpClient.post<BannerMutationResponse>(API.BANNERS, banner)
}

export const updateBanner = async ({ id, banner }: UpdateBannerParams) => {
    return httpClient.patch<BannerMutationResponse>(`${API.BANNERS}/${id}`, banner)
}

export const deleteBanner = async ({ id }: DeleteBannerParams) => {
    return httpClient.delete<BannerMutationResponse>(`${API.BANNERS}/${id}`)
}

export const getBanners = async (params?: GetBannersParams) => {
    return httpClient.get<BannerListResponse>(API.BANNERS, params)
}

export const getBannerDetails = async ({ id }: GetBannerDetailsParams) => {
    return httpClient.get<BannerDetailsResponse>(`${API.BANNERS}/${id}`)
}

// event
export const createEvent = async ({ event }: CreateEventParams) => {
    return httpClient.post<EventMutationResponse>(API.EVENTS, event)
}

export const updateEvent = async ({ id, event }: UpdateEventParams) => {
    return httpClient.patch<EventMutationResponse>(`${API.EVENTS}/${id}`, event)
}

export const deleteEvent = async ({ id }: DeleteEventParams) => {
    return httpClient.delete<EventMutationResponse>(`${API.EVENTS}/${id}`)
}

export const getEvents = async ({ filter }: GetEventsParams = {}) => {
    return httpClient.get<EventListResponse>(API.EVENTS, filter)
}

// youtube
export const getYoutubeInfo = async (url: string) => {
    return httpClient.get<YoutubeMetadataResponse>(API.YOUTUBE.INFO, { url })
}

export const createYoutubeJob = async (request: CreateYoutubeJobRequest) => {
    return httpClient.post<YoutubeJobResponse>(API.YOUTUBE.JOB, request)
}

export const getYoutubeProgressUrl = (jobId: string) =>
    getApiUrl(API.YOUTUBE.PROGRESS(jobId))

export const getYoutubeFileUrl = (jobId: string) =>
    getApiUrl(API.YOUTUBE.FILE(jobId))

// file conversion
export const createFileConversionJob = async (
    file: File,
    convertedFormat: FileConversionFormat
) => {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('convertedFormat', convertedFormat)

    return httpClient.post<FileConversionJobResponse>(API.CONVERT.JOB, formData)
}

export const getFileConversionProgressUrl = (jobId: string) =>
    getApiUrl(API.CONVERT.PROGRESS(jobId))

export const getConvertedFileUrl = (jobId: string) =>
    getApiUrl(API.CONVERT.FILE(jobId))
