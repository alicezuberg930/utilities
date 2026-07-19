import { Link } from '@tanstack/react-router'
import { useDialogState } from '@/hooks/use-dialog-state'
import { useAuth } from '@/providers/auth-provider'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuGroup,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuShortcut,
    DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { LogOut, SettingsIcon, UserIcon } from 'lucide-react'
// import { SignOutDialog } from '@/components/sign-out-dialog'

export const ProfileDropdown = () => {
    const [, setOpen] = useDialogState()
    const { user } = useAuth()
    const fallback = (user?.name ?? user?.email ?? 'AD')
        .split(' ')
        .map((part) => part[0])
        .join('')
        .slice(0, 2)
        .toUpperCase()

    return (
        <>
            <DropdownMenu modal={false}>
                <DropdownMenuTrigger>
                    <Button variant='ghost' className='relative h-8 w-8 rounded-full'>
                        <Avatar className='h-8 w-8'>
                            <AvatarImage src={user?.avatar ?? '/assets/user.png'} alt={user?.name ?? 'Người dùng'} />
                            <AvatarFallback>{fallback}</AvatarFallback>
                        </Avatar>
                    </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent className='w-56' align='end'>
                    <DropdownMenuGroup>
                        <DropdownMenuLabel className='font-normal'>
                            <div className='flex flex-col gap-1.5'>
                                <p className='text-sm leading-none font-medium'>
                                    {user?.name ?? 'Quản trị viên'}
                                </p>
                                <p className='text-xs leading-none text-muted-foreground'>
                                    {user?.email ?? 'Chưa đăng nhập'}
                                </p>
                            </div>
                        </DropdownMenuLabel>
                        <DropdownMenuSeparator />
                        <DropdownMenuGroup>
                            <DropdownMenuItem
                                render={
                                    <Link to='/settings'>
                                        Profile
                                        <DropdownMenuShortcut>
                                            <UserIcon size={24} />
                                        </DropdownMenuShortcut>
                                    </Link>
                                }
                            />
                            <DropdownMenuItem
                                render={
                                    <Link to='/settings'>
                                        Settings
                                        <DropdownMenuShortcut>
                                            <SettingsIcon size={24} />
                                        </DropdownMenuShortcut>
                                    </Link>
                                }
                            />
                        </DropdownMenuGroup>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem
                            variant='destructive'
                            onClick={() => setOpen(true)}
                        >
                            Sign out
                            <DropdownMenuShortcut className='text-current'>
                                <LogOut size={24} />
                            </DropdownMenuShortcut>
                        </DropdownMenuItem>
                    </DropdownMenuGroup>
                </DropdownMenuContent>
            </DropdownMenu>
            {/* <SignOutDialog open={!!open} onOpenChange={setOpen} /> */}
        </>
    )
}
