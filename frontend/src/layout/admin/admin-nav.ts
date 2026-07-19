import { CalendarDays, FilePlus2, FileText, Flag, Info, type LucideIcon } from 'lucide-react'
import { ROUTES } from '@/routes/path'

export type AdminNavItem = {
  title: string
  url: string
  activePrefix: string
  icon: LucideIcon
}

export const adminNavItems: AdminNavItem[] = [
  {
    title: 'Hoạt động',
    url: ROUTES.post.list,
    activePrefix: '/cms/post',
    icon: FilePlus2,
  },
  {
    title: 'Logs',
    url: ROUTES.log.list,
    activePrefix: '/cms/log',
    icon: FileText,
  },
  {
    title: 'Sự kiện',
    url: ROUTES.event.list,
    activePrefix: '/cms/event',
    icon: CalendarDays,
  },
  {
    title: 'Thông tin',
    url: '/cms/information',
    activePrefix: '/cms/information',
    icon: Info,
  },
  {
    title: 'Banner',
    url: ROUTES.banner.list,
    activePrefix: '/cms/banner',
    icon: Flag,
  },
]

export const getAdminPageTitle = (pathname: string) => {
  const currentItem = adminNavItems.find((item) => pathname.startsWith(item.activePrefix))
  return currentItem?.title ?? 'Dashboard'
}
