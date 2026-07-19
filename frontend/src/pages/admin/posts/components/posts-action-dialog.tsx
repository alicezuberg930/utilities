import { useEffect } from 'react'
import { type Resolver, useForm } from 'react-hook-form'
import { z } from 'zod'
import { postCategoryTitles, type Post, type PostCategory, type PostPayload, } from '@/@types/post'
import { useUploadFileHook } from '@/hooks/file.hook'
import {
  FormProvider,
  RFHStyledSelect,
  RHFRichTextEditor,
  RHFSingleDatePicker,
  RHFTextField,
  RHFUpload,
} from '@/components/hook-form'
import { isLocalUploadImage, type UploadImage, } from '@/components/upload'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import moment from 'moment'
import { postFormSchema } from '@/lib/validators/post-validator'
import { useMutation } from '@tanstack/react-query'
import { posts } from '@/lib/queries/post'

const zodResolver: Resolver<z.infer<typeof postFormSchema>> = async (values) => {
  const result = postFormSchema.safeParse(values)
  if (result.success) {
    return { values: result.data, errors: {} }
  }
  return {
    values: {},
    errors: result.error.issues.reduce<Record<string, { type: string; message: string }>>((errors, issue) => {
      const fieldName = issue.path[0]
      if (typeof fieldName === 'string') {
        errors[fieldName] = {
          type: issue.code,
          message: issue.message,
        }
      }
      return errors
    }, {}),
  }
}

const defaultValues = (post?: Post): z.infer<typeof postFormSchema> => ({
  title: post?.title ?? '',
  description: post?.description ?? '',
  date: post?.date ? moment(post.date, 'DD/MM/YYYY', true).toDate() : new Date(),
  category: post?.category ?? 'chao-tinh-thuong',
  cover: post?.cover ?? null,
  images: post?.images ?? [],
})

type PostsActionDialogProps = {
  currentRow?: Post | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

export const PostsActionDialog = ({
  currentRow,
  open,
  onOpenChange,
}: PostsActionDialogProps) => {
  const isEdit = !!currentRow
  const upload = useUploadFileHook()
  const { mutateAsync: create } = useMutation(posts().create.mutationOptions())
  const { mutateAsync: update } = useMutation(posts().update.mutationOptions())
  const form = useForm<z.infer<typeof postFormSchema>>({
    resolver: zodResolver,
    defaultValues: defaultValues(currentRow ?? undefined),
  })

  const { formState: { isSubmitting }, handleSubmit } = form

  useEffect(() => {
    if (open) {
      form.reset(defaultValues(currentRow ?? undefined))
    }
  }, [currentRow, form, open])

  const onSubmit = async (values: z.infer<typeof postFormSchema>) => {
    let coverUrl = typeof values.cover === 'string' ? values.cover : ''
    let images: string[] = []

    const formData = new FormData()
    if (isLocalUploadImage(values.cover)) {
      formData.set('files', values.cover)
      const response = await upload.mutateAsync({ file: formData })
      coverUrl = response.data![0]
    }
    formData.delete('files')
    for (let i = 0; i < values.images.length; i++) {
      if (isLocalUploadImage(values.images[i])) {
        formData.append('files', values.images[i])
      }
    }
    const imagesResponse = await upload.mutateAsync({ file: formData })
    images = imagesResponse.data!

    const post: PostPayload = {
      title: values.title,
      description: values.description,
      date: moment(values.date).format('DD/MM/YYYY'),
      category: values.category,
      cover: coverUrl,
      images
    }

    if (currentRow) {
      await update({ id: currentRow._id, ...post })
    } else {
      await create({ ...post })
    }

    form.reset(defaultValues())
    onOpenChange(false)
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(state) => {
        if (!state) form.reset(defaultValues(currentRow ?? undefined))
        onOpenChange(state)
      }}
    >
      <DialogContent className='max-h-[calc(100vh-2rem)] overflow-y-auto sm:max-w-4xl'>
        <DialogHeader className='text-start'>
          <DialogTitle>{isEdit ? 'Cập nhật bài viết' : 'Tạo bài viết'}</DialogTitle>
          <DialogDescription>
            Nhập thông tin bài viết và lưu thay đổi tại đây.
          </DialogDescription>
        </DialogHeader>
        <FormProvider
          id='post-form'
          methods={form}
          onSubmit={handleSubmit(onSubmit)}
        >
          <div className='grid gap-4 md:grid-cols-1'>
            <RHFTextField
              name='title'
              fieldLabel='Tiêu đề'
              placeholder='Nhập tiêu đề'
            />
            <RHFSingleDatePicker
              name='date'
              fieldLabel='Ngày'
              placeholder='Chọn ngày'
            />
            <RFHStyledSelect
              name='category'
              fieldLabel='Loại hoạt động'
              groups={[
                { items: Object.entries(postCategoryTitles).map(([value, label]) => ({ label, value })) }
              ]}
            />
            <RHFUpload
              name='cover'
              fieldLabel='Ảnh bìa'
              accept={{ 'image/*': [] }}
            />
            <RHFUpload
              name='images'
              multiple
              fieldLabel='Ảnh sự kiện'
              accept={{ 'image/*': [] }}
            />
          </div>
          <div className='mt-4'>
            <RHFRichTextEditor name='description' fieldLabel='Nội dung' />
          </div>
        </FormProvider>
        <DialogFooter>
          <Button type='submit' form='post-form' disabled={isSubmitting}>
            {isSubmitting ? 'Đang lưu...' : 'Lưu thay đổi'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
