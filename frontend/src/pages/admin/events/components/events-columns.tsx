import { type ColumnDef } from '@tanstack/react-table'
import { Check, CircleX } from 'lucide-react'
import moment from 'moment'
import { type AppEvent } from '@/@types/event'
import { DataTableColumnHeader } from '@/components/data-table'
import { LazyLoadImage } from '@/components/lazy-load-image'
import { Badge } from '@/components/ui/badge'
import { DataTableRowActions } from './data-table-row-actions'

const formatDateTime = (value?: string) => (
  value ? moment(value).format('DD/MM/YYYY HH:mm:ss') : '-'
)

export const eventsColumns: ColumnDef<AppEvent>[] = [
  {
    accessorKey: 'image',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Ảnh' />,
    cell: ({ row }) => (
      <div className='h-24 w-40 overflow-hidden rounded-md'>
        <LazyLoadImage
          widths={[
            { screenWidth: 640, imageWidth: 300 },
            { screenWidth: 1024, imageWidth: 400 },
            { screenWidth: 1920, imageWidth: 500 },
          ]}
          className='h-full w-full object-cover hover:scale-105'
          alt={row.original.image}
          src={row.original.image}
          effect='blur'
        />
      </div>
    ),
    enableSorting: false,
    enableHiding: false,
  },
  {
    id: 'isActive',
    accessorFn: (row) => String(row.isActive),
    header: ({ column }) => <DataTableColumnHeader column={column} title='Kích hoạt' />,
    cell: ({ row }) => (
      <Badge variant='outline' className='gap-1 whitespace-nowrap'>
        {row.original.isActive ? (
          <>
            <Check className='size-4 text-green-600' />
            Đang bật
          </>
        ) : (
          <>
            <CircleX className='size-4 text-red-600' />
            Đang tắt
          </>
        )}
      </Badge>
    ),
    filterFn: (row, id, value: string[]) => value.includes(row.getValue(id)),
  },
  {
    accessorKey: 'createdAt',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Ngày tạo' />,
    cell: ({ row }) => (
      <span className='whitespace-nowrap'>{formatDateTime(row.original.createdAt)}</span>
    ),
  },
  {
    accessorKey: 'updatedAt',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Ngày cập nhật' />,
    cell: ({ row }) => (
      <span className='whitespace-nowrap'>{formatDateTime(row.original.updatedAt)}</span>
    ),
  },
  {
    id: 'actions',
    cell: DataTableRowActions,
    enableHiding: false,
    enableSorting: false,
  },
]
