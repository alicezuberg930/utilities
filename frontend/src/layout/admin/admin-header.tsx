import { memo } from 'react'
import { useRouterState } from '@tanstack/react-router'
import { Bell } from 'lucide-react'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { getAdminPageTitle } from './admin-nav'
import { ThemeSwitch } from '@/components/theme-switch'
import { ProfileDropdown } from '../../components/profile-dropdown'

const AdminHeader = () => {
  const pageTitle = useRouterState({ select: (state) => getAdminPageTitle(state.location.pathname) })

  return (
    <header className='sticky top-0 z-20 flex h-14 shrink-0 items-center gap-3 border-b bg-background/95 px-4 backdrop-blur supports-backdrop-filter:bg-background/75'>
      <SidebarTrigger />
      <Separator orientation='vertical' className='h-full' />

      <Breadcrumb className='min-w-0 flex-1'>
        <BreadcrumbList>
          <BreadcrumbItem>
            <span className='text-muted-foreground'>CMS</span>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage className='truncate'>{pageTitle}</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <div className='ml-auto flex items-center gap-1.5'>
        {/* notifications */}
        <Button type='button' variant='ghost' size='icon-sm' aria-label='Notifications'>
          <Bell className='size-4' />
        </Button>
        {/* theme settings */}
        <ThemeSwitch />
        {/* admin profile */}
        <ProfileDropdown />
      </div>
    </header>
  )
}

export default memo(AdminHeader)