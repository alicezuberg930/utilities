import { FormProvider as Form, type UseFormReturn } from 'react-hook-form'

type Props = {
  children: React.ReactNode
  methods: UseFormReturn<any>
  onSubmit?: React.FormEventHandler<HTMLFormElement>
  id?: string
}

export const FormProvider = ({
  children,
  onSubmit,
  methods,
  id,
}: Readonly<Props>) => {
  return (
    <Form {...methods}>
      <form onSubmit={onSubmit} id={id}>
        {children}
      </form>
    </Form>
  )
}
