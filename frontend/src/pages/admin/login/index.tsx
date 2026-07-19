import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from '@/components/ui/card'
import { Field, FieldGroup } from '@/components/ui/field'
import { FormProvider, RHFPasswordField, RHFTextField } from '@/components/hook-form'
import z from 'zod'
import { useForm, type Resolver } from 'react-hook-form'
import { loginFormSchema } from '@/lib/validators/auth-validator'
import { useMutation } from '@tanstack/react-query'
import { auth } from '@/lib/queries/auth'
import { Spinner } from '@/components/ui/spinner'
import { useNavigate } from '@tanstack/react-router'
import { toast } from 'sonner'
import { Toaster } from '@/components/ui/sonner'

const zodResolver: Resolver<z.infer<typeof loginFormSchema>> = async (values) => {
    const result = loginFormSchema.safeParse(values)
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

const LoginPage = () => {
    const { mutateAsync } = useMutation(auth().login.mutationOptions())
    const navigate = useNavigate()
    const form = useForm<z.infer<typeof loginFormSchema>>({
        resolver: zodResolver,
        defaultValues: {
            username: '',
            password: ''
        },
    })

    const { formState: { isSubmitting }, handleSubmit } = form

    const onSubmit = async (values: z.infer<typeof loginFormSchema>) => {
        await mutateAsync(values, {
            onSuccess(data) {
                navigate({ to: '/cms' })
                toast.success(data.message)
            }
        })
    }

    return (
        <>
            <div className='flex min-h-svh w-full items-center justify-center p-6 md:p-10'>
                <div className='w-full max-w-sm'>
                    <div className={cn('flex flex-col gap-6')}>
                        <Card>
                            <CardHeader>
                                <CardTitle>Trang quản lý ánh sáng từ thiện</CardTitle>
                                <CardDescription>
                                    Nhập tên người dùng và mật khẩu để đăng nhập
                                </CardDescription>
                            </CardHeader>
                            <CardContent>
                                <FormProvider
                                    methods={form}
                                    onSubmit={handleSubmit(onSubmit)}
                                >
                                    <FieldGroup>
                                        <RHFTextField
                                            name='username'
                                            fieldLabel='Tên người dùng'
                                            placeholder='admin'
                                        />
                                        <RHFPasswordField
                                            name='password'
                                            fieldLabel='Mật khẩu'
                                            placeholder='*******'
                                        />
                                        <Field>
                                            <Button disabled={isSubmitting} type='submit'>
                                                {isSubmitting ? (
                                                    <Spinner />
                                                ) : (
                                                    <>Đăng nhập</>
                                                )}
                                            </Button>
                                        </Field>
                                    </FieldGroup>
                                </FormProvider>
                            </CardContent>
                        </Card>
                    </div>
                </div>
            </div>
            <Toaster richColors />
        </>
    )
}

export default LoginPage