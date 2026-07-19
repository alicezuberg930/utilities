import { type Row } from '@tanstack/react-table'
import { EllipsisVertical, Pencil, Trash2 } from 'lucide-react'
import { type AppEvent } from '@/@types/event'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useEvents } from './events-provider'

type DataTableRowActionsProps = {
  row: Row<AppEvent>
}

export const DataTableRowActions = ({ row }: DataTableRowActionsProps) => {
  const { setCurrentRow, setOpen } = useEvents()

  return (
    <DropdownMenu modal={false}>
      <DropdownMenuTrigger>
        <Button
          type='button'
          variant='ghost'
          className='flex h-8 w-8 p-0 data-[state=open]:bg-muted'
        >
          <EllipsisVertical className='h-4 w-4' />
          <span className='sr-only'>Open menu</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align='end' className='w-40'>
        <DropdownMenuItem
          onClick={() => {
            setCurrentRow(row.original)
            setOpen('edit')
          }}
        >
          Chỉnh sửa
          <DropdownMenuShortcut>
            <Pencil size={16} />
          </DropdownMenuShortcut>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className='text-red-500!'
          onClick={() => {
            setCurrentRow(row.original)
            setOpen('delete')
          }}
        >
          Xóa
          <DropdownMenuShortcut>
            <Trash2 size={16} />
          </DropdownMenuShortcut>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
