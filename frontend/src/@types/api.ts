export type EntityId = string

export type ApiQueryPrimitive = string | number | boolean

export type ApiQueryValue =
  | ApiQueryPrimitive
  | ApiQueryPrimitive[]
  | null
  | undefined

export type ApiQueryParams = Record<string, ApiQueryValue>

export type DetailParams = {
  id?: EntityId
}

export type DeleteParams = {
  id: EntityId
}

export type ApiResponse<T = unknown> = {
  message: string
  data?: T
  statusCode?: number
  path?: string
  method?: string
  timestamp?: string
  paginate?: {
    limit: number
    currentPage: number
    totalPages: number
  }
}