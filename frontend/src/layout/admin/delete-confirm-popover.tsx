import { useState } from 'react'
import { Trash2 } from 'lucide-react'
import { ConfirmDialog } from '@/components/confirm-dialog'
import { Button } from '@/components/ui/button'

type DeleteConfirmPopoverProps = {
  onConfirm: () => void
  isLoading?: boolean
}

export const DeleteConfirmPopover = ({
  onConfirm,
  isLoading,
}: DeleteConfirmPopoverProps) => {
  const [open, setOpen] = useState(false)

  return (
    <>
      <Button
        type='button'
        variant='destructive'
        className='p-3'
        title='Xóa'
        onClick={() => setOpen(true)}
      >
        <Trash2 size={16} />
      </Button>
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title='Xóa dữ liệu'
        desc='Bạn có chắc chắn muốn xóa mục này? Thao tác này không thể hoàn tác.'
        confirmText='Xóa'
        cancelBtnText='Hủy'
        destructive
        isLoading={isLoading}
        handleConfirm={() => {
          onConfirm()
          setOpen(false)
        }}
      />
    </>
  )
}

export default DeleteConfirmPopover
