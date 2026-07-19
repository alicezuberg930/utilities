import type { ApiQueryParams, ApiResponse, DeleteParams, DetailParams, EntityId } from './api'

export type PostCategory =
  | 'chao-tinh-thuong'
  | 'chuong-trinh-thuong-nien'
  | 'ho-tro-hoan-canh'
  | 'tiep-suc-tri-thuc'

export const postCategoryTitles: Record<PostCategory, string> = {
  'chao-tinh-thuong': 'Cháo tình thương',
  'chuong-trinh-thuong-nien': 'Chương trình thường niên',
  'ho-tro-hoan-canh': 'Hỗ trợ hoàn cảnh',
  'tiep-suc-tri-thuc': 'Tiếp sức tri thức'
}

export type Post = {
  _id: EntityId
  title: string
  description: string
  date: string
  category: PostCategory
  cover: string
  images: string[]
  createdAt?: string
  updatedAt?: string
}

export type PostPayload = {
  title?: string
  date?: string
  category?: PostCategory
  description?: string | null
  cover?: string
  images?: string[]
}

export type PostFilter = ApiQueryParams & {
  page?: number
  limit?: number
  search?: string
  category?: PostCategory
}

export type GetPostsParams = {
  filter?: PostFilter
}

export type CreatePostParams = {
  post: PostPayload
}

export type UpdatePostParams = {
  id: EntityId
  post: PostPayload
}