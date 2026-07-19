import { Link } from '@tanstack/react-router'
import { Menu } from 'lucide-react'
import { memo } from 'react'
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion'
import {
  NavigationMenu,
  NavigationMenuContent,
  NavigationMenuItem,
  NavigationMenuLink,
  NavigationMenuList,
  NavigationMenuTrigger
} from '@/components/ui/navigation-menu'
import { Sheet, SheetClose, SheetContent, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { ROUTES, ROOT_HOME } from '@/routes/path'

type NavItem = {
  name: string
  link: string
} | {
  name: string
  children: {
    name: string
    link: string
  }[]
}

const tabs: NavItem[] = [
  {
    name: 'Video',
    link: ROUTES.video
  },
  {
    name: 'Liên hệ',
    link: ROUTES.contact
  },
  {
    name: 'Giới thiệu',
    children: [
      {
        name: 'Quy định',
        link: ROUTES.rule
      },
      {
        name: 'Tiêu chí nhóm',
        link: ROUTES.criteria
      },
      {
        name: 'Cơ cấu tổ chức',
        link: ROUTES.structure
      }
    ]
  },
  {
    name: 'Hoạt động',
    children: [
      {
        name: 'Cháo tình thương',
        link: ROUTES.chaoTinhThuong
      },
      {
        name: 'Chương trình thường niên',
        link: ROUTES.chuongTrinhThuongNien
      },
      {
        name: 'Hỗ trợ hoàn cảnh',
        link: ROUTES.hoTroHoanCanh
      },
      {
        name: 'Tiếp sức tri thức',
        link: ROUTES.tiepSucTriThuc
      }
    ]
  },
  {
    name: 'Dịch vụ',
    children: [
      {
        name: 'Thiết kế',
        link: ROUTES.design
      },
      {
        name: 'Chụp ảnh',
        link: ROUTES.photoshoot
      }
    ]
  },
]

const desktopTabs = [tabs[2], tabs[3], tabs[0], tabs[4], tabs[1]]

const Header = () => {
  return (
    <header className='bg-white h-20 content-center z-10 shadow-md flex-none'>
      <nav className='flex font-semibold justify-between items-center relative max-w-7xl mx-auto px-2 lg:px-0'>
        <div className='flex items-center flex-auto'>
          <Link to={ROOT_HOME}>
            <img src='/assets/logo.png' className='inline-block w-14 h-14' alt='logo' />
          </Link>
          <span className='hidden md:block ml-3'>
            <div className='flex flex-col items-end'>
              <h4 className='text-main-color font-bold text-2xl tracking-tighter'>ÁNH SÁNG TỪ THIỆN</h4>
              <span className='font-semibold'>Since 2010</span>
            </div>
          </span>
        </div>
        {/* mobile menu */}
        <Sheet>
          <SheetTrigger aria-label='Menu' className='block md:hidden' type='button'>
            <Menu size={30} color='purple' />
          </SheetTrigger>
          <SheetContent
            side='right'
            className='w-56 gap-0 overflow-auto bg-white p-0 text-main-color data-[side=right]:w-56 sm:max-w-56'
            showCloseButton={false}
          >
            <SheetTitle className='sr-only'>Menu</SheetTitle>
            <Accordion className='w-full' multiple>
              {tabs.map(tab => (
                'link' in tab ? (
                  <SheetClose key={tab.name} render={<Link to={tab.link} />} nativeButton={false} className='block p-2 text-main-color hover:bg-gray-200'>
                    {tab.name}
                  </SheetClose>
                ) : (
                  <AccordionItem key={tab.name} value={tab.name} className='border-0'>
                    <AccordionTrigger className='p-2 font-semibold text-main-color hover:no-underline'>
                      <span>{tab.name}</span>
                    </AccordionTrigger>
                    <AccordionContent className='pb-0 [&_a]:no-underline'>
                      {tab.children.map(child => (
                        <SheetClose
                          key={child.link}
                          render={<Link to={child.link} />}
                          nativeButton={false}
                          className='block p-2 text-main-color hover:bg-gray-200'
                        >
                          {child.name}
                        </SheetClose>
                      ))}
                    </AccordionContent>
                  </AccordionItem>
                )
              ))}
            </Accordion>
          </SheetContent>
        </Sheet>
        {/* desktop menu */}
        <NavigationMenu
          render={<div />}
          delay={0}
          closeDelay={100}
          className='hidden flex-none md:flex'
        >
          <NavigationMenuList className='gap-7 text-main-color font-bold'>
            {desktopTabs.map(tab => (
              <NavigationMenuItem className='relative' key={tab.name} value={tab.name}>
                {'link' in tab ? (
                  <NavigationMenuLink
                    render={<Link to={tab.link} />}
                    className='p-0 text-main-color'
                    closeOnClick
                  >
                    <span>{tab.name}</span>
                  </NavigationMenuLink>
                ) : (
                  <>
                    <NavigationMenuTrigger className='h-auto rounded-none p-0 font-bold text-main-color hover:bg-transparent focus:bg-transparent data-open:bg-transparent data-open:hover:bg-transparent data-open:focus:bg-transparent data-popup-open:bg-transparent data-popup-open:hover:bg-transparent'>
                      <span>{tab.name}</span>
                    </NavigationMenuTrigger>
                    <NavigationMenuContent className='w-max min-w-44 rounded-md border border-gray-300 bg-white p-1 text-main-color shadow-lg'>
                      {tab.children.map(child => (
                        <NavigationMenuLink
                          key={child.link}
                          render={<Link to={child.link} />}
                          className='px-3 py-2 text-main-color hover:bg-gray-200 focus:bg-gray-200'
                          closeOnClick
                        >
                          <span>{child.name}</span>
                        </NavigationMenuLink>
                      ))}
                    </NavigationMenuContent>
                  </>
                )}
              </NavigationMenuItem>
            ))}
          </NavigationMenuList>
        </NavigationMenu>
      </nav>
    </header>
  )
}

export default memo(Header)