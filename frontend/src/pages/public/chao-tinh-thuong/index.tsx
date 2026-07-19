import Section from '@/layout/public/section'
import ActivityList from '@/layout/public/activity-list'
import ShimmerItemList from '@/layout/public/shimmer-item-list'
import { useQuery } from '@tanstack/react-query'
import { posts } from '@/lib/queries/post'

const ChaoTinhThuongPage = () => {
  const { data, isLoading } = useQuery(posts().all.queryOptions({ category: 'chao-tinh-thuong' }))

  return (
    <>
      <Section title='HOẠT ĐỘNG CHÁO TÌNH THƯƠNG' />
      {isLoading && <ShimmerItemList count={9} />}
      {data && <ActivityList posts={data} />}
    </>
  )
}

export default ChaoTinhThuongPage