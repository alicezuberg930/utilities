import { useEffect } from 'react'
import { Check, Moon, Sun } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useTheme } from 'next-themes'
import { Button } from '@/components/ui/button'
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

export const ThemeSwitch = () => {
    const { theme, setTheme } = useTheme()
    /* Update theme-color meta tag when theme is updated */
    useEffect(() => {
        const themeColor = theme === 'dark' ? '#020817' : '#fff'
        const metaThemeColor = document.querySelector("meta[name='theme-color']")
        if (metaThemeColor) metaThemeColor.setAttribute('content', themeColor)
    }, [theme])

    return (
        <DropdownMenu modal={false}>
            <DropdownMenuTrigger>
                <Button variant='ghost' size='icon-sm' className='scale-100 rounded-full'>
                    <Sun className='scale-100 size-4 rotate-0 transition-all dark:scale-0 dark:-rotate-90' />
                    <Moon className='absolute size-4 scale-0 rotate-90 transition-all dark:scale-100 dark:rotate-0' />
                    <span className='sr-only'>Toggle theme</span>
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align='end'>
                <DropdownMenuItem onClick={() => setTheme('light')}>
                    Light{' '}
                    <Check
                        size={14}
                        className={cn('ms-auto', theme !== 'light' && 'hidden')}
                    />
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => setTheme('dark')}>
                    Dark
                    <Check
                        size={14}
                        className={cn('ms-auto', theme !== 'dark' && 'hidden')}
                    />
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => setTheme('system')}>
                    System
                    <Check
                        size={14}
                        className={cn('ms-auto', theme !== 'system' && 'hidden')}
                    />
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    )
}
