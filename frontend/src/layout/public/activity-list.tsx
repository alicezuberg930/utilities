import { Link } from '@tanstack/react-router'
import { slugify } from "@/lib/utils"
import type { Post } from '@/@types/post'
import LazyLoadImage from '@/components/lazy-load-image/lazy-load-image'
import { stripHtml } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'

type ActivityListProps = {
  posts: Post[]
}

const ActivityList = ({ posts }: ActivityListProps) => {
  return (
    <div className='flex flex-wrap mb-4 -mx-2.5'>
      {posts.map((post, i) => (
        <div className='w-full p-2.5 sm:w-1/2 lg:w-1/3' key={i}>
          <div className='w-full'>
            <Link
              className='block relative'
              to={`/${post.category}/${slugify(post.title)}-${post._id}`}
            >
              <Badge className='absolute flex items-center bg-main-color rounded-md top-3 left-3 p-3 z-1'>
                {post.date}
              </Badge>

              <div className='shadow-md space-y-3 rounded-lg overflow-hidden'>
                <div className='w-full h-72 overflow-hidden'>
                  <LazyLoadImage
                    widths={[
                      { screenWidth: 640, imageWidth: 300 },  // Phone
                      { screenWidth: 1024, imageWidth: 400 },  // Tablet
                      { screenWidth: 1920, imageWidth: 500 },  // Desktop and larger
                    ]}
                    className='hover:scale-105 h-full w-full object-cover'
                    alt={post.title}
                    src={post.cover}
                    effect='blur'
                  />
                </div>
                <div className='space-y-3 p-3'>
                  <h5 className='line-clamp-1 text-lg font-bold'>{post.title}</h5>
                  <p className='line-clamp-2'>{stripHtml(post.description)}</p>
                </div>
              </div>
            </Link>
          </div>
        </div>
      ))}
    </div>
  )
}

export default ActivityList