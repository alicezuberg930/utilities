import type { ApiResponse } from './api'

export type UploadFileParams = {
  file: FormData
}

export type UploadFileResponse = ApiResponse<string[]>
