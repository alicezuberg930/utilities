import { memo } from 'react'
import { Skeleton } from '@/components/ui/skeleton'
import { CustomSlider, type SliderProps } from '@/components/custom-slider'

const settings = {
  slidesToShow: 3,
  autoplay: false,
  loop: true,
  showDot: false,
  responsive: [
    {
      breakpoint: 1024,
      slidesToShow: 2
    },
    {
      breakpoint: 640,
      slidesToShow: 1
    }
  ]
} as SliderProps

type PlayListSliderProps = {
  videoIds: string[]
  isLoading?: boolean
}

const PlayListSlider = ({ videoIds, isLoading = false }: PlayListSliderProps) => {
  return (
    <div className='my-4 -mx-2'>
      {isLoading ? (
        <div
          className='grid grid-cols-1 gap-4 px-2 sm:grid-cols-2 lg:grid-cols-3'
          aria-label='Loading videos'
          aria-busy='true'
        >
          {Array.from({ length: 3 }).map((_, index) => (
            <Skeleton key={index} className='h-60 w-full rounded-none' />
          ))}
        </div>
      ) : (
        <CustomSlider {...settings}>
          {videoIds.map((videoId) => (
            <div key={videoId} className='px-2'>
              <iframe
                title={videoId}
                src={`https://www.youtube-nocookie.com/embed/${videoId}`}
                allow='accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share'
                allowFullScreen
                loading='lazy'
                className='w-full h-60 border-0'
              />
            </div>
          ))}
        </CustomSlider>
      )}
    </div>
  )
}

export default memo(PlayListSlider)