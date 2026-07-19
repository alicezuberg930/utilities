import { getRouteApi } from '@tanstack/react-router'
import type { NavigateFn } from '@/hooks/use-table-url-state'
import { useGetEventsHook } from '@/hooks/event.hook'
import { Main } from '@/layout/admin'
import { ShimmerList } from '@/layout/common'
import { EventsDialogs } from './components/events-dialogs'
import { EventsPrimaryButtons } from './components/events-primary-buttons'
import { EventsProvider } from './components/events-provider'
import { EventsTable } from './components/events-table'

const route = getRouteApi('/cms/event/list')

const EventsPage = () => {
  const filter = { page: 1 }
  const { data: events, isLoading } = useGetEventsHook({ filter })
  const search = route.useSearch()
  const navigate = route.useNavigate() as NavigateFn

  return (
    <EventsProvider>
      <Main>
        <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
          <div className='w-full'>
            <span className='text-xl font-bold'>Ảnh sự kiện</span>
          </div>
          <EventsPrimaryButtons />
        </div>
        {isLoading ? (
          <div className='rounded-md border p-3'>
            <ShimmerList />
          </div>
        ) : (
          <EventsTable data={events?.data ?? []} search={search} navigate={navigate} />
        )}
      </Main>
      <EventsDialogs />
    </EventsProvider>
  )
}

export default EventsPage
