import { queryOptions } from '@tanstack/react-query'
import type { YoutubePlaylistVideosResponse } from '@/@types'
import { httpClient } from '../repository/http-client'
import { googleApiKey as key } from '../constants'

const keys = {
    one: (id: string) => ['playlists', id],
}

export const playlists = () => ({
    one: {
        queryKey: keys.one,
        queryOptions: (id: string) =>
            queryOptions({
                queryKey: keys.one(id),
                queryFn: async () => {
                    return httpClient.get<YoutubePlaylistVideosResponse>(
                        'https://youtube.googleapis.com/youtube/v3/playlistItems',
                        {
                            part: 'snippet,contentDetails',
                            maxResults: 30,
                            playlistId: id,
                            key,
                        },
                        { credentials: 'omit' }
                    )
                },
            }),
    },
})
