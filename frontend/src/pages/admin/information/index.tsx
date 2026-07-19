import { useEffect } from 'react'
import { type Resolver, useForm } from 'react-hook-form'
import { z } from 'zod'
import { Pen } from 'lucide-react'
import type { WebsiteInformation } from '@/@types/information'
import { FormProvider, RHFTextField } from '@/components/hook-form'
import { Button } from '@/components/ui/button'
import { Main } from '@/layout/admin'
import { useMutation, useQuery } from '@tanstack/react-query'
import { information } from '@/lib/queries/information'

const informationFormSchema = z.object({
  activityAddress: z.string().trim(),
  storageAddress: z.string().trim(),
  email: z.string().trim(),
  hotline: z.string().trim(),
  facebookUrl: z.string().trim(),
  zaloURL: z.string().trim(),
  youtubeURL: z.string().trim(),
  googleMapURL: z.string().trim(),
  websiteURL: z.string().trim(),
  achaubankNumber: z.string().trim(),
  vpbankNumber: z.string().trim(),
})

const zodResolver: Resolver<WebsiteInformation> = async (values) => {
  const result = informationFormSchema.safeParse(values)
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

const defaultValues = (information?: WebsiteInformation) => ({
  activityAddress: information?.activityAddress ?? '',
  storageAddress: information?.storageAddress ?? '',
  email: information?.email ?? '',
  hotline: information?.hotline ?? '',
  facebookUrl: information?.facebookUrl ?? '',
  zaloURL: information?.zaloURL ?? '',
  youtubeURL: information?.youtubeURL ?? '',
  googleMapURL: information?.googleMapURL ?? '',
  websiteURL: information?.websiteURL ?? '',
  achaubankNumber: information?.achaubankNumber ?? '',
  vpbankNumber: information?.vpbankNumber ?? '',
})

const informationFields: {
  name: keyof WebsiteInformation
  label: string
  placeholder: string
  type?: string
}[] = [
    {
      name: 'activityAddress',
      label: 'Địa chỉ nấu cháo',
      placeholder: 'Nhập địa chỉ nấu cháo',
    },
    {
      name: 'storageAddress',
      label: 'Địa chỉ kho',
      placeholder: 'Nhập địa chỉ kho',
    },
    {
      name: 'email',
      label: 'Email',
      placeholder: 'Nhập email',
      type: 'email',
    },
    {
      name: 'hotline',
      label: 'Hotline',
      placeholder: 'Nhập hotline',
    },
    {
      name: 'facebookUrl',
      label: 'Trang facebook',
      placeholder: 'Nhập địa chỉ URL trang facebook',
    },
    {
      name: 'zaloURL',
      label: 'Trang zalo',
      placeholder: 'Nhập địa chỉ URL trang zalo',
    },
    {
      name: 'youtubeURL',
      label: 'Trang youtube',
      placeholder: 'Nhập địa chỉ URL trang youtube',
    },
    {
      name: 'googleMapURL',
      label: 'URL google map',
      placeholder: 'Nhập URL của google map',
    },
    {
      name: 'websiteURL',
      label: 'Địa chỉ website',
      placeholder: 'Nhập địa chỉ URL của website',
    },
    {
      name: 'achaubankNumber',
      label: 'STK Á châu bank',
      placeholder: 'Nhập STK ngân hàng',
    },
    {
      name: 'vpbankNumber',
      label: 'STK VP bank',
      placeholder: 'Nhập STK ngân hàng',
    },
  ]

const InformationPage = () => {
  const { mutateAsync, isPending } = useMutation(information().update.mutationOptions())
  const { data } = useQuery(information().one.queryOptions())
  const form = useForm<WebsiteInformation>({
    resolver: zodResolver,
    defaultValues: defaultValues(),
  })

  const { formState: { isSubmitting } } = form

  useEffect(() => {
    if (data) {
      form.reset(defaultValues(data))
    }
  }, [form, data])

  const onSubmit = async (values: WebsiteInformation) => await mutateAsync(values)

  return (
    <Main>
      <div className='mb-10 flex flex-col gap-1'>
        <span className='text-xl font-bold'>Chỉnh sửa thông tin website</span>
        <span className='text-sm text-gray-500'>
          Điền đầy đủ thông tin của website
        </span>
      </div>

      <FormProvider
        id='information-form'
        methods={form}
        onSubmit={form.handleSubmit(onSubmit)}
      >
        <div className='grid gap-4 md:grid-cols-2'>
          {informationFields.map((field) => (
            <RHFTextField
              key={field.name}
              name={field.name}
              fieldLabel={field.label}
              placeholder={field.placeholder}
              type={field.type ?? 'text'}
            />
          ))}
        </div>
        <Button
          type='submit'
          form='information-form'
          className='mt-6 gap-1.5'
          disabled={isSubmitting || isPending}
        >
          <Pen size={18} />
          <span>{isSubmitting || isPending ? 'Đang lưu...' : 'Chỉnh sửa'}</span>
        </Button>
      </FormProvider>
    </Main>
  )
}

export default InformationPage
