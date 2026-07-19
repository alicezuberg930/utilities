import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { type AppEvent } from '@/@types/event'
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
import { useDeleteEventHook } from '@/hooks/event.hook'

type EventsDeleteDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow: AppEvent
}

export const EventsDeleteDialog = ({
  open,
  onOpenChange,
  currentRow,
}: EventsDeleteDialogProps) => {
  const [value, setValue] = useState('')
  const remove = useDeleteEventHook()
  const isConfirmed = value.trim() === currentRow._id

  const handleDelete = () => {
    if (!isConfirmed) return

    remove.mutate(
      { id: currentRow._id },
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
            Xóa sự kiện
          </DialogTitle>
          <DialogDescription>
            Thao tác này sẽ xóa vĩnh viễn sự kiện đã chọn.
          </DialogDescription>
        </DialogHeader>
        <div className='space-y-4'>
          <p>
            Nhập lại mã sự kiện{' '}
            <span className='font-semibold'>{currentRow._id}</span> để xác nhận.
          </p>
          <Field>
            <FieldLabel>Mã sự kiện</FieldLabel>
            <Input
              value={value}
              onChange={(event) => setValue(event.target.value)}
              placeholder='Nhập mã sự kiện để xác nhận'
            />
          </Field>
          <Alert variant='destructive'>
            <AlertTitle>Cảnh báo</AlertTitle>
            <AlertDescription>
              Sự kiện đã xóa không thể khôi phục.
            </AlertDescription>
          </Alert>
        </div>
        <DialogFooter>
          <Button
            type='button'
            variant='destructive'
            disabled={!isConfirmed || remove.isPending}
            onClick={handleDelete}
          >
            {remove.isPending ? 'Đang xóa...' : 'Xóa'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
