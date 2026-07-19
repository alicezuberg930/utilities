import { type ColumnDef } from '@tanstack/react-table'
import moment from 'moment'
import { type RequestLog } from '@/@types/log'
import { DataTableColumnHeader } from '@/components/data-table'
import { Badge } from '@/components/ui/badge'
import { LongText } from '@/components/long-text'
import { DataTableRowActions } from './data-table-row-actions'

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

export const logsColumns: ColumnDef<RequestLog>[] = [
  {
    accessorKey: 'method',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Method' />,
    cell: ({ row }) => (
      <Badge variant='outline' className='whitespace-nowrap'>
        {row.original.method}
      </Badge>
    ),
    filterFn: (row, id, value: string[]) => value.includes(row.getValue(id)),
    enableHiding: false,
  },
  {
    accessorKey: 'path',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Path' />,
    cell: ({ row }) => (
      <LongText className='w-40'>
        {row.original.path}
      </LongText>
    ),
    filterFn: (row, id, value) => String(row.getValue(id) ?? '').toLowerCase().includes(String(value ?? '').toLowerCase()),
    enableHiding: false,
  },
  {
    accessorKey: 'ipAddress',
    header: ({ column }) => <DataTableColumnHeader column={column} title='IP Address' />,
    cell: ({ row }) => (
      <span className='whitespace-nowrap'>{row.original.ipAddress}</span>
    ),
    filterFn: (row, id, value) =>
      String(row.getValue(id) ?? '').toLowerCase().includes(String(value ?? '').toLowerCase()),
  },
  {
    accessorKey: 'userAgent',
    header: ({ column }) => <DataTableColumnHeader column={column} title='User Agent' />,
    cell: ({ row }) => (
      <LongText className='w-90'>
        {row.original.userAgent}
      </LongText>
    ),
  },
  {
    accessorKey: 'referrer',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Referrer' />,
    cell: ({ row }) => (
      <LongText className='w-50'>
        {row.original.referrer || '-'}
      </LongText>
    ),
  },
  {
    accessorKey: 'createdAt',
    header: ({ column }) => <DataTableColumnHeader column={column} title='Created At' />,
    cell: ({ row }) => (
      <span className='whitespace-nowrap'>{moment(row.original.createdAt).format('DD/MM/YYYY HH:mm:ss')}</span>
    ),
    filterFn: (row, id, value) => normalizeDateFilterValue(row.getValue(id)) === normalizeDateFilterValue(value),
  },
  {
    id: 'actions',
    cell: DataTableRowActions,
    enableHiding: false,
    enableSorting: false,
  },
]