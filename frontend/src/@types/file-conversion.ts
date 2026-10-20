import type { ApiResponse } from './api'

export type FileConversionFormat =
  | 'aac'
  | 'apng'
  | 'avi'
  | 'avif'
  | 'bmp'
  | 'dpx'
  | 'exr'
  | 'fits'
  | 'flac'
  | 'gif'
  | 'glb'
  | 'hdr'
  | 'ico'
  | 'im1'
  | 'im8'
  | 'im24'
  | 'j2c'
  | 'j2k'
  | 'jls'
  | 'jp2'
  | 'jpeg'
  | 'jpg'
  | 'jxl'
  | 'ljpg'
  | 'm4a'
  | 'mkv'
  | 'mov'
  | 'mp3'
  | 'mp4'
  | 'ogg'
  | 'opus'
  | 'pam'
  | 'pbm'
  | 'pcx'
  | 'pfm'
  | 'pgm'
  | 'pgmyuv'
  | 'phm'
  | 'pix'
  | 'png'
  | 'ppm'
  | 'qoi'
  | 'ras'
  | 'rs'
  | 'sgi'
  | 'sun'
  | 'sunras'
  | 'tga'
  | 'tif'
  | 'tiff'
  | 'vbn'
  | 'wav'
  | 'wbmp'
  | 'webm'
  | 'webp'
  | 'xbm'
  | 'xface'
  | 'xwd'

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
