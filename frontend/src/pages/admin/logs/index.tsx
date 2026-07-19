import { getRouteApi } from '@tanstack/react-router'
import { ShimmerList } from '@/layout/common'
import type { NavigateFn } from '@/hooks/use-table-url-state'
import { LogsDialogs } from './components/logs-dialogs'
import { LogsProvider } from './components/logs-provider'
import { LogsTable } from './components/logs-table'
import { Main } from '@/layout/admin'
import { useQuery } from '@tanstack/react-query'
import { logs } from '@/lib/queries/log'

const route = getRouteApi('/cms/log/list')

const LogsPage = () => {
  const { data, isLoading } = useQuery(logs().all.queryOptions())
  const search = route.useSearch()
  const navigate = route.useNavigate() as NavigateFn

  return (
    <LogsProvider>
      <Main>
        <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
          <div className='w-full'>
            <span className='text-xl font-bold'>Danh sách log</span>
          </div>
        </div>
        {isLoading && (
          <div className='rounded-md border p-3'>
            <ShimmerList />
          </div>
        )}
        {data && (
          <LogsTable data={data} search={search} navigate={navigate} />
        )}
      </Main>
      <LogsDialogs />
    </LogsProvider>
  )
}

export default LogsPage
