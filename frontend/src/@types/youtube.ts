export type YoutubePlaylistItem = {
  kind?: string
  etag?: string
  id?: string
  snippet?: {
    title?: string
    description?: string
    publishedAt?: string
    thumbnails?: Record<string, unknown>
    resourceId?: {
      kind?: string
      videoId?: string
    }
  }
  contentDetails: {
    videoId: string
    videoPublishedAt?: string
  }
}

export type YoutubePlaylistVideosResponse = {
  kind?: string
  etag?: string
  nextPageToken?: string
  prevPageToken?: string
  pageInfo?: {
    totalResults: number
    resultsPerPage: number
  }
  items: YoutubePlaylistItem[]
}
