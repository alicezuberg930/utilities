import { type ColumnDef } from '@tanstack/react-table'
import moment from 'moment'
import { postCategoryTitles, type Post } from '@/@types/post'
import { Badge } from '@/components/ui/badge'
import { DataTableColumnHeader } from '@/components/data-table'
import { DataTableRowActions } from './data-table-row-actions'
import { LazyLoadImage } from '@/components/lazy-load-image'
import { LongText } from '@/components/long-text'
import { stripHtml } from '@/lib/utils'

const normalizeDateFilterValue = (value: unknown) => {
  const raw = String(value ?? '').trim()
  if (!raw) return ''
  const date = moment(raw, [
    'D/M/YYYY',
    'DD/MM/YYYY',
    'D-M-YYYY',
    'DD-MM-YYYY',
    moment.ISO_8601,
  ], true)
  return date.isValid() ? date.format('DD-MM-YYYY') : raw.toLowerCase()
}

export const postsColumns: ColumnDef<Post>[] = [
  {
    accessorKey: 'title',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Tiêu đề' />,
    cell: ({ row }) => (
      <LongText className='w-64'>
        {row.original.title}
      </LongText>
    ),
    enableHiding: false,
  },
  {
    accessorKey: 'description',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Mô tả' />,
    cell: ({ row }) => (
      <LongText className='w-80'>
        {stripHtml(row.original.description)}
      </LongText>
    ),
  },
  {
    accessorKey: 'date',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Ngày' />,
    cell: ({ row }) => <span className='whitespace-nowrap'>{row.original.date}</span>,
    filterFn: (row, id, value) => normalizeDateFilterValue(row.getValue(id)) === normalizeDateFilterValue(value),
  },
  {
    accessorKey: 'category',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Loại' />,
    cell: ({ row }) => (
      <Badge variant='outline' className='whitespace-nowrap'>
        {postCategoryTitles[row.original.category]}
      </Badge>
    ),
    filterFn: (row, id, value: string[]) => value.includes(row.getValue(id)),
  },
  {
    accessorKey: 'cover',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Ảnh bìa' />,
    cell: ({ row }) => (
      <div className='h-24 w-32 rounded-md overflow-hidden'>
        <LazyLoadImage
          widths={[
            { screenWidth: 640, imageWidth: 300 },  // Phone
            { screenWidth: 1024, imageWidth: 400 },  // Tablet
            { screenWidth: 1920, imageWidth: 500 },  // Desktop and larger
          ]}
          className='hover:scale-105 h-full w-full object-cover'
          alt={row.original.title}
          src={row.original.cover}
          effect='blur'
        />
      </div>
    ),
    enableSorting: false,
  },
  {
    accessorKey: 'createdAt',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Ngày tạo' />,
    cell: ({ row }) => (
      <span className='whitespace-nowrap'>{moment(row.original.createdAt).format('DD/MM/YYYY HH:mm:ss')}</span>
    ),
  },
  {
    accessorKey: 'updatedAt',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Ngày cập nhật' />,
    cell: ({ row }) => (
      <span className='whitespace-nowrap'>{moment(row.original.updatedAt).format('DD/MM/YYYY HH:mm:ss')}</span>
    ),
  },
  {
    id: 'actions',
    cell: DataTableRowActions,
    enableHiding: false,
    enableSorting: false,
  },
]
