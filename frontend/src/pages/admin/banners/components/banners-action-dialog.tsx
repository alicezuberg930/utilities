import { useEffect } from 'react'
import { type Resolver, useForm } from 'react-hook-form'
import { z } from 'zod'
import { type Banner, type BannerPayload } from '@/@types/banner'
import { FormProvider, RHFSwitch, RHFTextField, RHFUpload } from '@/components/hook-form'
import { isLocalUploadImage, type UploadImage } from '@/components/upload'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useCreateBannerHook, useUpdateBannerHook } from '@/hooks/banner.hook'
import { useUploadFileHook } from '@/hooks/file.hook'

type BannerFormValues = {
  order: string
  isActive: boolean
  image: UploadImage | null
}

const bannerFormSchema = z.object({
  order: z.string().trim().min(1, 'Vui lòng nhập thứ tự.'),
  isActive: z.boolean(),
  image: z.custom<UploadImage>(
    (value) => typeof value === 'string' || (typeof value === 'object' && value !== null),
    { message: 'Vui lòng chọn ảnh.' }
  ),
})

const zodResolver: Resolver<BannerFormValues> = async (values) => {
  const result = bannerFormSchema.safeParse(values)
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

const defaultValues = (banner?: Banner): BannerFormValues => ({
  order: banner?.order !== undefined ? String(banner.order) : '',
  isActive: banner?.isActive ?? false,
  image: banner?.image ?? null,
})

type BannersActionDialogProps = {
  currentRow?: Banner | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

export const BannersActionDialog = ({
  currentRow,
  open,
  onOpenChange,
}: BannersActionDialogProps) => {
  const isEdit = !!currentRow
  const upload = useUploadFileHook()
  const createBanner = useCreateBannerHook()
  const updateBanner = useUpdateBannerHook()
  const form = useForm<BannerFormValues>({
    resolver: zodResolver,
    defaultValues: defaultValues(currentRow ?? undefined),
  })

  const { formState: { isSubmitting } } = form

  useEffect(() => {
    if (open) {
      form.reset(defaultValues(currentRow ?? undefined))
    }
  }, [currentRow, form, open])

  const onSubmit = async (values: BannerFormValues) => {
    try {
      let imageUrl = typeof values.image === 'string' ? values.image : ''

      if (isLocalUploadImage(values.image)) {
        const formData = new FormData()
        formData.set('files', values.image)
        const response = await upload.mutateAsync({ file: formData })
        imageUrl = response.data![0] ?? ''
      }

      const banner: BannerPayload = {
        order: values.order,
        isActive: values.isActive,
        image: imageUrl,
      }

      if (currentRow) {
        await updateBanner.mutateAsync({ id: currentRow._id, banner })
      } else {
        await createBanner.mutateAsync({ banner })
      }

      form.reset(defaultValues())
      onOpenChange(false)
    } catch {
      // Hook-level onError handlers show the API error.
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(state) => {
        if (!state) form.reset(defaultValues(currentRow ?? undefined))
        onOpenChange(state)
      }}
    >
      <DialogContent className='max-h-[calc(100vh-2rem)] overflow-y-auto sm:max-w-3xl'>
        <DialogHeader className='text-start'>
          <DialogTitle>{isEdit ? 'Cập nhật banner' : 'Tạo banner'}</DialogTitle>
          <DialogDescription>
            Nhập thông tin banner và lưu thay đổi tại đây.
          </DialogDescription>
        </DialogHeader>
        <FormProvider
          id='banner-form'
          methods={form}
          onSubmit={form.handleSubmit(onSubmit)}
        >
          <div className='space-y-4'>
            <RHFTextField
              name='order'
              fieldLabel='Thứ tự'
              type='number'
              placeholder='Nhập thứ tự'
            />
            <RHFSwitch
              name='isActive'
              label='Kích hoạt'
            />
            <RHFUpload
              name='image'
              fieldLabel='Ảnh'
              accept={{ 'image/*': [] }}
            />
          </div>
        </FormProvider>
        <DialogFooter>
          <Button type='submit' form='banner-form' disabled={isSubmitting}>
            {isSubmitting ? 'Đang lưu...' : 'Lưu thay đổi'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
