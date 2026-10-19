import type { ApiResponse } from './api'

export type FileConversionFormat =
  | 'aac'
  | 'avi'
  | 'flac'
  | 'glb'
  | 'm4a'
  | 'mkv'
  | 'mov'
  | 'mp3'
  | 'mp4'
  | 'ogg'
  | 'opus'
  | 'wav'
  | 'webm'

export type FileConversionJob = {
  jobId: string
  status: 'pending' | 'running' | 'completed' | 'failed'
  originalFormat: string
  convertedFormat: FileConversionFormat
  downloadUrl: string
  progress: number
  createdAt: string
  updatedAt: string
}

export type FileConversionJobResponse = Omit<ApiResponse<FileConversionJob>, 'data'> & {
  data: FileConversionJob
}

export type FileConversionProgressEvent = {
  progress: number
}

export type FileConversionFailureEvent = {
  message: string
}
