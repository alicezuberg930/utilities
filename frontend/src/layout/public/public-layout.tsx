import { Outlet } from '@tanstack/react-router'
import { Toaster } from '@/components/ui/sonner'
import Footer from '@/layout/public/footer'
import Header from '@/layout/public/header'
import { memo } from 'react'

const PublicLayout = () => {
  return (
    <>
      <div className='w-full h-screen overflow-hidden'>
        <div className='h-full overflow-hidden'>
          <div className='flex flex-col h-full overflow-hidden'>
            <Header />
            <div className='flex-auto overflow-y-auto overflow-x-hidden'>
              <div className='max-w-7xl mx-auto px-3 lg:px-0'>
                <Outlet />
              </div>
              <Footer />
            </div>
          </div>
        </div>
      </div>
      <Toaster richColors />
    </>
  )
}

export default memo(PublicLayout)