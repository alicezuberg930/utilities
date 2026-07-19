import { useEffect } from 'react'
import { type Resolver, useForm } from 'react-hook-form'
import { z } from 'zod'
import { type AppEvent, type EventPayload } from '@/@types/event'
import { FormProvider, RHFSwitch, RHFUpload } from '@/components/hook-form'
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
import { useCreateEventHook, useUpdateEventHook } from '@/hooks/event.hook'
import { useUploadFileHook } from '@/hooks/file.hook'

type EventFormValues = {
  isActive: boolean
  image: UploadImage | null
}

const eventFormSchema = z.object({
  isActive: z.boolean(),
  image: z.custom<UploadImage>(
    (value) => typeof value === 'string' || (typeof value === 'object' && value !== null),
    { message: 'Vui lòng chọn ảnh.' }
  ),
})

const zodResolver: Resolver<EventFormValues> = async (values) => {
  const result = eventFormSchema.safeParse(values)
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

const defaultValues = (event?: AppEvent): EventFormValues => ({
  isActive: event?.isActive ?? false,
  image: event?.image ?? null,
})

type EventsActionDialogProps = {
  currentRow?: AppEvent | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

export const EventsActionDialog = ({
  currentRow,
  open,
  onOpenChange,
}: EventsActionDialogProps) => {
  const isEdit = !!currentRow
  const upload = useUploadFileHook()
  const createEvent = useCreateEventHook()
  const updateEvent = useUpdateEventHook()
  const form = useForm<EventFormValues>({
    resolver: zodResolver,
    defaultValues: defaultValues(currentRow ?? undefined),
  })

  const { formState: { isSubmitting } } = form

  useEffect(() => {
    if (open) {
      form.reset(defaultValues(currentRow ?? undefined))
    }
  }, [currentRow, form, open])

  const onSubmit = async (values: EventFormValues) => {
    try {
      let imageUrl = typeof values.image === 'string' ? values.image : ''

      if (isLocalUploadImage(values.image)) {
        const formData = new FormData()
        formData.set('files', values.image)
        const response = await upload.mutateAsync({ file: formData })
        imageUrl = response.data![0] ?? ''
      }

      const event: EventPayload = {
        isActive: values.isActive,
        image: imageUrl,
      }

      if (currentRow) {
        await updateEvent.mutateAsync({ id: currentRow._id, event })
      } else {
        await createEvent.mutateAsync({ event })
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
          <DialogTitle>{isEdit ? 'Cập nhật sự kiện' : 'Tạo sự kiện'}</DialogTitle>
          <DialogDescription>
            Nhập thông tin sự kiện và lưu thay đổi tại đây.
          </DialogDescription>
        </DialogHeader>
        <FormProvider
          id='event-form'
          methods={form}
          onSubmit={form.handleSubmit(onSubmit)}
        >
          <div className='space-y-4'>
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
          <Button type='submit' form='event-form' disabled={isSubmitting}>
            {isSubmitting ? 'Đang lưu...' : 'Lưu thay đổi'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
