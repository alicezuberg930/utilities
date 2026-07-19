import { useState } from 'react'
import { AlertTriangle } from 'lucide-react'
import { type Post } from '@/@types/post'
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
import { posts } from '@/lib/queries/post'

type PostsDeleteDialogProps = {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentRow: Post
}

export const PostsDeleteDialog = ({
  open,
  onOpenChange,
  currentRow,
}: PostsDeleteDialogProps) => {
  const [value, setValue] = useState('')
  const { mutate, isPending } = useMutation(posts().delete.mutationOptions())
  const isConfirmed = value.trim() === currentRow.title

  const handleDelete = () => {
    if (!isConfirmed) return
    mutate(currentRow._id, {
      onSuccess() {
        setValue('')
        onOpenChange(false)
      }
    })
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
            Xóa bài viết
          </DialogTitle>
          <DialogDescription>
            Thao tác này sẽ xóa vĩnh viễn bài viết.
          </DialogDescription>
        </DialogHeader>
        <div className='space-y-4'>
          <p>
            Nhập lại tiêu đề{' '}
            <span className='font-semibold'>{currentRow.title}</span> để xác nhận.
          </p>
          <Field>
            <FieldLabel>Tiêu đề</FieldLabel>
            <Input
              value={value}
              onChange={(event) => setValue(event.target.value)}
              placeholder='Nhập tiêu đề để xác nhận'
            />
          </Field>
          <Alert variant='destructive'>
            <AlertTitle>Cảnh báo</AlertTitle>
            <AlertDescription>
              Bài viết đã xóa không thể khôi phục.
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
