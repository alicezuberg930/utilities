import PlayListSlider from '@/pages/public/video/components/playlist-slider'
import Section from '@/layout/public/section'
import { playlistIds as ids } from '@/lib/constants'
import { useQueries } from '@tanstack/react-query'
import { playlists } from '@/lib/queries/playlist'

function VideoPage() {
  const results = useQueries({
    queries: ids.map((id) =>
      playlists().one.queryOptions(id)
    ),
  })

  const queriedPlaylistIds = results.map((playlist) => [
    ...new Set(
      playlist.data?.items.map((item) => item.contentDetails.videoId) ?? []
    ),
  ])

  console.log(results[0].data)

  const titles = [
    'VIDEO CHÁO TÌNH THƯƠNG',
    'VIDEO TIẾP SỨC TRI THỨC',
    'VIDEO CHƯƠNG TRÌNH THƯỜNG NIÊN',
    'VIDEO HỖ TRỢ HOÀN CẢNH KHÓ KHĂN',
  ]

  return (
    <>
      {titles.map((title, index) => (
        <div key={title}>
          <Section title={title} />
          <PlayListSlider
            videoIds={queriedPlaylistIds[index]}
            isLoading={results[index].isLoading}
          />
        </div>
      ))}
    </>
  )
}

export default VideoPage
