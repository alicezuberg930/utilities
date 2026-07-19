import type { ApiQueryParams, ApiResponse, DeleteParams, EntityId } from './api'

export type AppEvent = {
  _id: EntityId
  image: string
  isActive: boolean
  createdAt?: string
  updatedAt?: string
}

export type EventPayload = {
  image: string
  isActive: boolean
}

export type EventFilter = ApiQueryParams & {
  isActive?: boolean
  page?: number
  limit?: number
  search?: string
}

export type GetEventsParams = {
  filter?: EventFilter
}

export type CreateEventParams = {
  event: EventPayload
}

export type UpdateEventParams = {
  id: EntityId
  event: EventPayload
}

export type DeleteEventParams = DeleteParams

export type EventListResponse = ApiResponse<AppEvent[]>

export type EventMutationResponse = ApiResponse