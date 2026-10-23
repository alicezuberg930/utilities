import type { ApiResponse } from './api'

export type ImageCompressionJob = {
  jobId: string
  status: 'pending' | 'running' | 'completed' | 'failed'
  quality: number
  originalFileName: string
  originalFormat: 'jpeg' | 'jpg' | 'png' | 'webp'
  outputFormat: 'jpg' | 'png' | 'webp'
  originalSizeBytes: number
  compressedSizeBytes: number | null
  downloadUrl: string
  progress: number
  createdAt: string
  updatedAt: string
}

export type ImageCompressionJobResponse = Omit<ApiResponse<ImageCompressionJob>, 'data'> & {
  data: ImageCompressionJob
}

export type ImageCompressionProgressEvent = {
  progress: number
}

export type ImageCompressionFailureEvent = {
  message: string
}
