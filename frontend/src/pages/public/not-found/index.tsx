import { Link } from '@tanstack/react-router'
import { buttonVariants } from '@/components/ui/button'
import { PageNotFoundIllustration } from '@/lib/illustrations'

const NotFoundPage = () => {
  return (
    <main className='flex min-h-screen items-center justify-center px-6 py-12'>
      <div className='flex w-full max-w-xl flex-col items-center text-center'>
        <PageNotFoundIllustration className='w-full max-w-md' />

        <h1 className='mt-6 text-3xl font-bold tracking-tight sm:text-4xl'>
          Không tìm thấy trang
        </h1>
        <p className='mt-3 max-w-md text-muted-foreground'>
          Trang bạn đang tìm kiếm không tồn tại hoặc đã được di chuyển.
        </p>

        <Link to='/' className={buttonVariants({ size: 'lg', className: 'mt-8' })}>
          Về trang chủ
        </Link>
      </div>
    </main>
  )
}
export default NotFoundPage