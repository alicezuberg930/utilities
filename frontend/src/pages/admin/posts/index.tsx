import { ShimmerList } from '@/layout/common'
import { PostsDialogs } from './components/posts-dialogs'
import { PostsPrimaryButtons } from './components/posts-primary-buttons'
import { PostsProvider } from './components/posts-provider'
import { PostsTable } from './components/posts-table'
import { getRouteApi } from '@tanstack/react-router'
import type { NavigateFn } from '@/hooks/use-table-url-state'
import { Main } from '@/layout/admin'
import { useQuery } from '@tanstack/react-query'
import { posts } from '@/lib/queries/post'

const route = getRouteApi('/cms/post/list')

const PostsPage = () => {
  const { data, isLoading } = useQuery(posts().all.queryOptions({ page: 1 }))
  const search = route.useSearch()
  const navigate = route.useNavigate() as NavigateFn

  return (
    <PostsProvider>
      <Main>
        <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
          <div className='w-full'>
            <span className='text-xl font-bold'>Bài viết về hoạt động</span>
          </div>
          <PostsPrimaryButtons />
        </div>
        {isLoading && (
          <div className='rounded-md border p-3'>
            <ShimmerList />
          </div>
        )}
        {data && (
          <PostsTable data={data} search={search} navigate={navigate} />
        )}
      </Main>
      <PostsDialogs />
    </PostsProvider>
  )
}

export default PostsPage