import type {
  ApiQueryParams,
  EntityId,
} from './api'

export type RequestLog = {
  _id: EntityId
  ipAddress: string
  userAgent: string
  path: string
  method: string
  referrer: string
  createdAt?: string
  updatedAt?: string
}

export type LogFilter = ApiQueryParams & {
  page?: number
  limit?: number
  search?: string
  method?: string
}

export type GetLogsParams = {
  filter?: LogFilter
}