import ShimmerItemList from '@/layout/public/shimmer-item-list'
import ActivityList from '@/layout/public/activity-list'
import Section from '@/layout/public/section'
import { useQuery } from '@tanstack/react-query'
import { posts } from '@/lib/queries/post'

const TiepSucTriThucPage = () => {
  const { data, isLoading } = useQuery(posts().all.queryOptions({ category: 'tiep-suc-tri-thuc' }))

  return (
    <>
      <Section title={'HOẠT ĐỘNG TIẾP SỨC TRI THỨC'} />
      {isLoading && <ShimmerItemList count={9} />}
      {data && <ActivityList posts={data} />}
    </>
  )
}

export default TiepSucTriThucPage