import ShimmerItemList from '@/layout/public/shimmer-item-list'
import ActivityList from '@/layout/public/activity-list'
import Section from '@/layout/public/section'
import { useQuery } from '@tanstack/react-query'
import { posts } from '@/lib/queries/post'

const HoTroHoanCanhPage = () => {
  const { data, isLoading } = useQuery(posts().all.queryOptions({ category: 'ho-tro-hoan-canh' }))

  return (
    <>
      <Section title='HOẠT ĐỘNG HỖ TRỢ HOÀN CẢNH' />
      {isLoading && <ShimmerItemList count={9} />}
      {data && <ActivityList posts={data} />}
    </>
  )
}

export default HoTroHoanCanhPage