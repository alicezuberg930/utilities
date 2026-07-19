import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { type Banner } from '@/@types/banner'
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
import { useDeleteBannerHook } from '@/hooks/banner.hook'

type BannersDeleteDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow: Banner
}

export const BannersDeleteDialog = ({
  open,
  onOpenChange,
  currentRow,
}: BannersDeleteDialogProps) => {
  const [value, setValue] = useState('')
  const remove = useDeleteBannerHook()
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
            Xóa banner
          </DialogTitle>
          <DialogDescription>
            Thao tác này sẽ xóa vĩnh viễn banner đã chọn.
          </DialogDescription>
        </DialogHeader>
        <div className='space-y-4'>
          <p>
            Nhập lại mã banner{' '}
            <span className='font-semibold'>{currentRow._id}</span> để xác nhận.
          </p>
          <Field>
            <FieldLabel>Mã banner</FieldLabel>
            <Input
              value={value}
              onChange={(event) => setValue(event.target.value)}
              placeholder='Nhập mã banner để xác nhận'
            />
          </Field>
          <Alert variant='destructive'>
            <AlertTitle>Cảnh báo</AlertTitle>
            <AlertDescription>
              Banner đã xóa không thể khôi phục.
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
