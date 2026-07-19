import { ImagePlus } from 'lucide-react'
import { useDropzone } from 'react-dropzone'
import { cn } from '@/lib/utils'
import { Typography } from '../ui/typography'
import RejectionFiles from './errors/rejection-files'
import AvatarPreview from './preview/avatar-preview'
//
import { type UploadProps } from './types'

export default function UploadAvatar({
  error,
  file,
  disabled,
  helperText,
  ...other
}: Readonly<UploadProps>) {
  const {
    getRootProps,
    getInputProps,
    isDragActive,
    isDragReject,
    fileRejections,
  } = useDropzone({
    multiple: false,
    disabled,
    ...other,
  })

  const hasFile = !!file

  const isError = isDragReject || !!error

  return (
    <>
      <div
        {...getRootProps()}
        className={cn(
          'relative mx-auto flex h-36 w-36 cursor-pointer items-center justify-center overflow-hidden rounded-full border border-dashed border-main-color bg-main-color transition-opacity',
          isDragActive && 'opacity-70',
          isError && 'border-red-300',
          isError && hasFile && 'bg-red-50',
          disabled && 'pointer-events-none opacity-50',
          hasFile && 'hover:[&_.placeholder]:opacity-100'
        )}
      >
        <input {...getInputProps()} />

        {hasFile && <AvatarPreview file={file} />}

        <div
          className={cn(
            'placeholder absolute z-7 flex h-[calc(100%-16px)] w-[calc(100%-16px)] flex-col items-center justify-center rounded-full bg-main-color text-white transition-opacity hover:opacity-80',
            hasFile && 'z-10 bg-main-color/75 text-white opacity-0',
            isError && 'bg-red-50 text-red-600'
          )}
        >
          <ImagePlus className='mb-2' />
          <Typography variant='caption'>
            {file ? 'Cập nhật ảnh' : 'Tải ảnh lên'}
          </Typography>
        </div>
      </div>

      {helperText}

      <RejectionFiles fileRejections={fileRejections} />
    </>
  )
}
