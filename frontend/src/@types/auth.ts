import type { ApiResponse } from "./api"

export type AuthUser = {
  _id: string
  email: string
  name: string
  phone?: string | null
  address?: string | null
  avatar?: string | null
}

export type LoginParams = {
  username: string
  password: string
}

export type LoginPayload = {
  access_token: string
  user: AuthUser
}

export type LoginResponse = ApiResponse<LoginPayload>
export type ProfileResponse = ApiResponse<AuthUser>