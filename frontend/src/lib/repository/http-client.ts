import { type ApiResponse } from '@/@types'
import { HttpError } from './http-error'
import { InterceptorManager } from './interceptor'

const getBaseUrl = () => {
  const environment = import.meta.env.VITE_ENVIRONMENT
  const productionApi = import.meta.env.VITE_PRODUCTION_API
  const developmentApi = import.meta.env.VITE_DEVELOPMENT_API

  if (environment === 'development') {
    return developmentApi
  }
  if (environment === 'production') {
    return productionApi
  }
  return productionApi ?? developmentApi
}

const BASE_URL = getBaseUrl()

const isAbsoluteUrl = (url: string) => /^https?:\/\//i.test(url)

const getRequestUrl = (endpoint: string) => {
  return isAbsoluteUrl(endpoint) ? endpoint : `${BASE_URL}${endpoint}`
}

const getErrorMessage = (data: unknown) => {
  if (typeof data === 'string') return data || 'Request failed'

  if (data && typeof data === 'object') {
    if ('message' in data) {
      const message = (data as { message?: unknown }).message
      if (Array.isArray(message)) return message.join(', ')
      if (typeof message === 'string') return message
    }

    if ('error' in data) {
      return getErrorMessage((data as { error?: unknown }).error)
    }
  }

  return 'Request failed'
}

const parseResponseBody = async <T>(response: globalThis.Response) => {
  const text = await response.text()
  if (!text) return null as T

  try {
    return JSON.parse(text) as T
  } catch {
    return text as T
  }
}

export type ResponseWithHeaders<T> = {
  data: T
  headers: Headers
}

export class HttpClient {
  interceptors = {
    request: new InterceptorManager<RequestInit>(),
    response: new InterceptorManager<
      Error | HttpError | ResponseWithHeaders<unknown>
    >(),
  }

  private async fetchJson<T = any>(
    url: string,
    options: RequestInit = {}
  ): Promise<T> {
    const headers = new Headers(options.headers)
    headers.set('Accept', headers.get('Accept') ?? 'application/json')

    if (options.body !== undefined && !(options.body instanceof FormData)) {
      headers.set(
        'Content-Type',
        headers.get('Content-Type') ?? 'application/json'
      )
    }

    let config: RequestInit = {
      ...options,
      headers,
    }

    for (const { onFulfilled } of this.interceptors.request.getHandlers()) {
      if (onFulfilled) config = await onFulfilled(config)
    }

    try {
      const response = await fetch(url, config)
      if (!response.ok) {
        const data = await parseResponseBody<ApiResponse | string>(
          response
        )
        throw new HttpError(response.status, getErrorMessage(data), data)
      }

      const data = await parseResponseBody<T>(response)

      for (const { onFulfilled } of this.interceptors.response.getHandlers()) {
        if (onFulfilled) {
          await onFulfilled({ data: data as T, headers: response.headers })
        }
      }

      return data as T
    } catch (error: unknown) {
      const handledError =
        error instanceof HttpError
          ? error
          : new HttpError(
            0,
            error instanceof Error ? error.message : 'Network Error'
          )

      for (const { onRejected } of this.interceptors.response.getHandlers()) {
        if (onRejected) onRejected(handledError)
      }

      throw handledError
    }
  }

  get<T = any>(
    endpoint: string,
    params: Record<string, unknown> = {},
    options?: RequestInit
  ) {
    const queryParams = new URLSearchParams()
    for (const key in params) {
      const value = params[key]
      if (value !== undefined && value !== null) {
        if (Array.isArray(value)) {
          queryParams.append(key, JSON.stringify(value))
        } else {
          queryParams.append(key, String(value))
        }
      }
    }

    const requestUrl = getRequestUrl(endpoint)
    const queryString = queryParams.toString()
    const separator = requestUrl.includes('?') ? '&' : '?'

    return this.fetchJson<T>(
      queryString ? `${requestUrl}${separator}${queryString}` : requestUrl,
      {
        method: 'GET',
        credentials: 'include',
        ...options,
      }
    )
  }

  post<T = any>(endpoint: string, body?: unknown, options?: RequestInit) {
    return this.fetchJson<T>(
      getRequestUrl(endpoint),
      {
        method: 'POST',
        credentials: 'include',
        body:
          body !== undefined
            ? body instanceof FormData
              ? body
              : JSON.stringify(body)
            : undefined,
        ...options,
      }
    )
  }

  put<T = any>(endpoint: string, body?: unknown, options?: RequestInit) {
    return this.fetchJson<T>(
      getRequestUrl(endpoint),
      {
        method: 'PUT',
        credentials: 'include',
        body:
          body !== undefined
            ? body instanceof FormData
              ? body
              : JSON.stringify(body)
            : undefined,
        ...options,
      }
    )
  }

  patch<T = any>(endpoint: string, body?: unknown, options?: RequestInit) {
    return this.fetchJson<T>(
      getRequestUrl(endpoint),
      {
        method: 'PATCH',
        credentials: 'include',
        body:
          body !== undefined
            ? body instanceof FormData
              ? body
              : JSON.stringify(body)
            : undefined,
        ...options,
      }
    )
  }

  delete<T = any>(endpoint: string, options?: RequestInit) {
    return this.fetchJson<T>(
      getRequestUrl(endpoint),
      {
        method: 'DELETE',
        credentials: 'include',
        ...options,
      }
    )
  }
}

export const httpClient = new HttpClient()
