import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { type RequestLog } from '@/@types/log'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { useMutation } from '@tanstack/react-query'
import { logs } from '@/lib/queries/log'

type LogsDeleteDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow: RequestLog
}

export const LogsDeleteDialog = ({
  open,
  onOpenChange,
  currentRow,
}: LogsDeleteDialogProps) => {
  const [value, setValue] = useState('')
  const { mutateAsync, isPending } = useMutation(logs().delete.mutationOptions())
  const confirmValue = currentRow.path || currentRow._id
  const isConfirmed = value.trim() === confirmValue

  const handleDelete = async () => {
    if (!isConfirmed) return

    await mutateAsync(
      currentRow._id,
      {
        onSuccess() {
          setValue('')
          onOpenChange(false)
        },
      }
    )
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(state) => {
        if (!state) setValue('')
        onOpenChange(state)
      }}
    >
      <DialogContent>
        <DialogHeader className='text-start'>
          <DialogTitle className='text-destructive'>
            <AlertTriangle className='me-1 inline-block stroke-destructive' size={18} />
            Xóa log
          </DialogTitle>
          <DialogDescription>
            Thao tác này sẽ xóa vĩnh viễn log đã chọn.
          </DialogDescription>
        </DialogHeader>
        <div className='space-y-4'>
          <p>
            Nhập lại đường dẫn{' '}
            <span className='font-semibold'>{confirmValue}</span> để xác nhận.
          </p>
          <Field>
            <FieldLabel>Đường dẫn</FieldLabel>
            <Input
              value={value}
              onChange={(event) => setValue(event.target.value)}
              placeholder='Nhập đường dẫn để xác nhận'
            />
          </Field>
          <Alert variant='destructive'>
            <AlertTitle>Cảnh báo</AlertTitle>
            <AlertDescription>
              Log đã xóa không thể khôi phục.
            </AlertDescription>
          </Alert>
        </div>
        <DialogFooter>
          <Button
            type='button'
            variant='destructive'
            disabled={!isConfirmed || isPending}
            onClick={handleDelete}
          >
            {isPending ? 'Đang xóa...' : 'Xóa'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
