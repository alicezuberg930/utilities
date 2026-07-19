import { getRouteApi } from '@tanstack/react-router'
import type { NavigateFn } from '@/hooks/use-table-url-state'
import { useGetBannersHook } from '@/hooks/banner.hook'
import { Main } from '@/layout/admin'
import { ShimmerList } from '@/layout/common'
import { BannersDialogs } from './components/banners-dialogs'
import { BannersPrimaryButtons } from './components/banners-primary-buttons'
import { BannersProvider } from './components/banners-provider'
import { BannersTable } from './components/banners-table'

const route = getRouteApi('/cms/banner/list')

const BannersPage = () => {
  const filter = { page: 1 }
  const { data: banners, isLoading } = useGetBannersHook(filter)
  const search = route.useSearch()
  const navigate = route.useNavigate() as NavigateFn

  return (
    <BannersProvider>
      <Main>
        <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
          <div className='w-full'>
            <span className='text-xl font-bold'>Ảnh banner</span>
          </div>
          <BannersPrimaryButtons />
        </div>
        {isLoading ? (
          <div className='rounded-md border p-3'>
            <ShimmerList />
          </div>
        ) : (
          <BannersTable data={banners?.data ?? []} search={search} navigate={navigate} />
        )}
      </Main>
      <BannersDialogs />
    </BannersProvider>
  )
}

export default BannersPage
