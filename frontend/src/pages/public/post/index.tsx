import { useParams } from '@tanstack/react-router'
import { SEO } from '@/lib/seo'
import LazyLoadImage from '@/components/lazy-load-image/lazy-load-image'
import { Button } from '@/components/ui/button'
import { LucideVolume2 } from 'lucide-react'
import { stripHtml, textToSpeech } from '@/lib/utils'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { VideoPlayer } from '@/components/video/video-player'
import { ShimmerPost } from '@/layout/public'
import { useQuery } from '@tanstack/react-query'
import { posts } from '@/lib/queries/post'
import { SeverErrorIllustration } from '@/lib/illustrations'

const ActivityDetailsPage = () => {
  const { slug } = useParams({ strict: false })
  const id = slug?.slice(slug.lastIndexOf('-') + 1)
  const { data: post, isLoading, isError } = useQuery(posts().one.queryOptions(id))

  const handleTextToSpeech = () => {
    if (post) textToSpeech(stripHtml(post.description))
  }

  return (
    <>
      {isLoading && < ShimmerPost />}
      {post && !isError ? (
        <>
          <SEO
            title={post?.title}
            description={post?.description}
            image={post?.cover}
          />
          <div>
            <div className='mt-4'>
              <div className='text-center'>
                <span className='text-main-color font-bold text-lg'>{post?.title}</span>
                <hr className='my-4 block'></hr>
              </div>
            </div>
            <div className='mt-4'>
              <div className='aspect-video mx-auto w-full md:w-4/5 rounded-2xl relative image-container'>
                <LazyLoadImage
                  widths={[
                    { screenWidth: 640, imageWidth: 640 },  // Phone
                    { screenWidth: 1024, imageWidth: 1024 },  // Tablet
                    { screenWidth: 1920, imageWidth: 1920 },  // Desktop and larger
                  ]}
                  className='w-full h-full object-cover rounded-xl'
                  alt={post?.title}
                  src={post?.cover}
                  effect='blur'
                />
              </div>
            </div>
            <div className='mt-4 w-fit mx-auto'>
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger>
                    <Button className='bg-main-color hover:bg-main-color/80 size-12' onClick={handleTextToSpeech}>
                      <LucideVolume2 size={32} />
                    </Button>
                  </TooltipTrigger>
                  <TooltipContent>
                    <p>Bấm vào để dược nghe chuyển thông tin thành giọng nói</p>
                  </TooltipContent>
                </Tooltip>
              </TooltipProvider>
            </div>
            <div className='mt-4 w-full'>
              <div className='w-fit mx-auto' dangerouslySetInnerHTML={{ __html: post?.description }}></div>
            </div>
            <div className='my-4'>
              <div className='w-full md:w-3/5 mx-auto'>
                <div className='text-center'>
                  <h3 className='text-main-color'>Video thực hiện chương trình</h3>
                  <hr className='block my-4'></hr>
                </div>
                <VideoPlayer videoUrl='/videos/22.07.2018-chao-tinh-thuong.mp4' />
              </div>

              <div className='w-full md:w-3/5 mx-auto'>
                <div className='text-center'>
                  <h3 className='text-main-color'>Hình ảnh thực hiện chương trình</h3>
                  <hr className='block my-4'></hr>
                </div>
                {post?.images.map((image) => (
                  <div key={image} className='rounded-2xl my-4 aspect-video w-full relative image-container'>
                    <LazyLoadImage
                      widths={[
                        { screenWidth: 640, imageWidth: 640 },  // Phone
                        { screenWidth: 1024, imageWidth: 1024 },  // Tablet
                        { screenWidth: 1920, imageWidth: 1920 },  // Desktop and larger
                      ]}
                      className='h-full w-full object-cover rounded-xl'
                      alt={image}
                      src={image}
                      effect='blur'
                    />
                  </div>
                ))}
              </div>
            </div>
          </div>
        </>
      ) : (
        <SeverErrorIllustration />
      )}
    </>
  )
}

export default ActivityDetailsPage