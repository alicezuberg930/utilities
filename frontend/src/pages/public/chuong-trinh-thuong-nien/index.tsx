import Section from '@/layout/public/section'
import ActivityList from '@/layout/public/activity-list'
import ShimmerItemList from '@/layout/public/shimmer-item-list'
import { useQuery } from '@tanstack/react-query'
import { posts } from '@/lib/queries/post'

const ChuongTrinhThuongNienPage = () => {
  const { data, isLoading } = useQuery(posts().all.queryOptions({ category: 'chuong-trinh-thuong-nien' }))

  return (
    <>
      <Section title='HOẠT ĐỘNG CHƯƠNG TRÌNH THƯỜNG NIÊN' />
      {isLoading && <ShimmerItemList count={9} />}
      {data && <ActivityList posts={data} />}
    </>
  )
}

export default ChuongTrinhThuongNienPage