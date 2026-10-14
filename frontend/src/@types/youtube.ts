import type { ApiResponse } from './api'

export type YoutubeDownloadFormat = 'audio' | 'video'
export type YoutubeVideoQuality = '360p' | '480p' | '720p' | '1080p'

export type YtDlpFormat = {
  format_id: string
  format: string
  video_ext: string
  audio_ext: string
  vcodec: string
  acodec: string
  url: string
  ext: string
  resolution?: string | null
  filesize?: number | null
  abr?: number | null
}

export type FilteredMetadata = {
  title: string | null
  thumbnail: string | null
  videoFormats: YtDlpFormat[]
  audioFormats: YtDlpFormat[]
  combinedFormat: string | null
}

export type CreateYoutubeJobRequest = {
  url: string
  format: YoutubeDownloadFormat
  audioQuality?: number
  videoQuality?: YoutubeVideoQuality
}

export type YoutubeJob = {
  jobId: string
  status: 'pending' | 'running' | 'completed' | 'failed'
  url: string
  audioQuality: number | null
  videoQuality: YoutubeVideoQuality | null
  format: YoutubeDownloadFormat
  progress: number
  createdAt: string
  updatedAt: string
}

export type YoutubeApiResponse<T> = Omit<ApiResponse<T>, 'data' | 'message'> & {
  data: T
  message?: string
}

export type YoutubeMetadataResponse = YoutubeApiResponse<FilteredMetadata>
export type YoutubeJobResponse = YoutubeApiResponse<YoutubeJob>

export type YoutubeProgressEvent = {
  progress: number
}

export type YoutubeFailureEvent = {
  message: string
}

export type YoutubePlaylistItem = {
  kind?: string
  etag?: string
  id?: string
  snippet?: {
    title?: string
    description?: string
    publishedAt?: string
    thumbnails?: Record<string, unknown>
    resourceId?: {
      kind?: string
      videoId?: string
    }
  }
  contentDetails: {
    videoId: string
    videoPublishedAt?: string
  }
}

export type YoutubePlaylistVideosResponse = {
  kind?: string
  etag?: string
  nextPageToken?: string
  prevPageToken?: string
  pageInfo?: {
    totalResults: number
    resultsPerPage: number
  }
  items: YoutubePlaylistItem[]
}
