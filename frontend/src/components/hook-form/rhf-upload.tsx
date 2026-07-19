// form
import { useFormContext, Controller } from 'react-hook-form'
import { Field, FieldError, FieldLabel } from '../ui/field'
// components
import {
  UploadAvatar,
  Upload,
  createUploadImage,
  type UploadProps,
} from '../upload'

interface Props extends Omit<UploadProps, 'file' | 'files'> {
  name: string
  multiple?: boolean
  fieldLabel: string
}

export const RHFUploadAvatar = ({ name, ...other }: Readonly<Props>) => {
  const { control } = useFormContext()

  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState: { error } }) => (
        <Field data-invalid={!!error}>
          {/* <FieldLabel htmlFor={field.name}>{fieldLabel}</FieldLabel> */}
          <UploadAvatar
            accept={{ 'image/*': [] }}
            error={!!error}
            file={field.value}
            {...other}
          />
          {!!error && <FieldError errors={[error]} className='mt-1' />}
        </Field>
      )}
    />
  )
}

export const RHFUpload = ({
  name,
  multiple,
  helperText,
  fieldLabel,
  onDrop,
  onDelete,
  onRemove,
  onRemoveAll,
  ...other
}: Readonly<Props>) => {
  const { control } = useFormContext()

  return (
    <Controller
      name={name}
      control={control}
      render={({ field, fieldState: { error } }) =>
        multiple ? (
          <Field data-invalid={!!error}>
            <FieldLabel htmlFor={field.name}>{fieldLabel}</FieldLabel>
            <Upload
              thumbnail
              multiple
              accept={{ 'image/*': [] }}
              files={field.value}
              onDrop={(acceptedFiles, fileRejections, event) => {
                field.onChange([
                  ...(field.value ?? []),
                  ...acceptedFiles.map(createUploadImage),
                ])
                onDrop?.(acceptedFiles, fileRejections, event)
              }}
              onRemove={(file) => {
                field.onChange(
                  (field.value ?? []).filter(
                    (currentFile: File | string) => currentFile !== file
                  )
                )
                onRemove?.(file)
              }}
              onRemoveAll={() => {
                field.onChange([])
                onRemoveAll?.()
              }}
              error={!!error}
              helperText={
                (!!error || helperText) && (
                  <FieldError errors={[error]} className='mt-3' />
                )
              }
              {...other}
            />
          </Field>
        ) : (
          <Field data-invalid={!!error}>
            <FieldLabel htmlFor={field.name}>{fieldLabel}</FieldLabel>
            <Upload
              accept={{ 'image/*': [] }}
              file={field.value}
              onDrop={(acceptedFiles, fileRejections, event) => {
                const file = acceptedFiles[0]
                field.onChange(file ? createUploadImage(file) : null)
                onDrop?.(acceptedFiles, fileRejections, event)
              }}
              onDelete={() => {
                field.onChange(null)
                onDelete?.()
              }}
              error={!!error}
              helperText={
                (!!error || helperText) && (
                  <FieldError errors={[error]} className='mt-3' />
                )
              }
              {...other}
            />
          </Field>
        )
      }
    />
  )
}
