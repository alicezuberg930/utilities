//
import { CircleX } from 'lucide-react'
import { useDropzone } from 'react-dropzone'
// import { useLocales } from '@/lib/locales'
// assets
import { UploadIllustration } from '@/lib/illustrations'
//
import { cn } from '@/lib/utils'
import { Button } from '../ui/button'
import { Typography } from '@/components/ui/typography'
// components
import RejectionFiles from './errors/rejection-files'
import MultiFilePreview from './preview/multi-file-preview'
import SingleFilePreview from './preview/single-file-preview'
import { type UploadProps } from './types'

export default function Upload({
  disabled,
  multiple = false,
  error,
  helperText,
  //
  file,
  onDelete,
  //
  files,
  thumbnail,
  onUpload,
  onRemove,
  onRemoveAll,
  ...other
}: Readonly<UploadProps>) {
  const {
    getRootProps,
    getInputProps,
    isDragActive,
    isDragReject,
    fileRejections,
  } = useDropzone({ multiple, disabled, ...other })

  const hasFile = !!file && !multiple

  const hasFiles = files && multiple && files.length > 0

  const isError = isDragReject || !!error

  return (
    <div className='relative w-full'>
      <div
        {...getRootProps()}
        className={cn(
          'relative cursor-pointer rounded-lg border border-dashed border-main-color bg-main-color p-10 text-white transition-all hover:opacity-80',
          isDragActive && 'opacity-80',
          isError && 'border-red-300 bg-red-50 text-red-600',
          disabled && 'pointer-events-none opacity-50',
          hasFile && 'aspect-4/3'
        )}
      >
        <input {...getInputProps()} />

        <Placeholder className={cn(hasFile && 'opacity-0')} />

        {hasFile && <SingleFilePreview file={file} />}
      </div>

      {helperText}

      <RejectionFiles fileRejections={fileRejections} />

      {hasFile && onDelete && (
        <Button
          type='button'
          size='sm'
          onClick={onDelete}
          aria-label='Xóa tệp'
          className='absolute top-4 right-4 z-10 bg-main-color/80 text-white hover:bg-main-color'
        >
          <CircleX width={18} />
        </Button>
      )}

      {hasFiles && (
        <>
          <div className='my-3'>
            <MultiFilePreview
              files={files}
              thumbnail={thumbnail}
              onRemove={onRemove}
            />
          </div>

          <div className='flex justify-end gap-1.5'>
            {onRemoveAll && (
              <Button
                type='button'
                variant='outline'
                size='sm'
                onClick={onRemoveAll}
                className='border-main-color text-main-color hover:bg-main-color/10'
              >
                Xóa tất cả
              </Button>
            )}

            {onUpload && (
              <Button
                type='button'
                size='sm'
                onClick={onUpload}
                className='bg-main-color text-white hover:bg-main-color/80'
              >
                Tải tệp lên
              </Button>
            )}
          </div>
        </>
      )}
    </div>
  )
}

function Placeholder({
  className,
  ...other
}: Readonly<React.HTMLAttributes<HTMLDivElement>>) {
  return (
    <div
      className={cn(
        'flex w-full flex-col items-center justify-center gap-5 text-center md:flex-row md:text-left',
        className
      )}
      {...other}
    >
      <UploadIllustration style={{ width: 220 }} />
      <div>
        <Typography variant='h5'>Kéo thả hoặc chọn tệp</Typography>
        <Typography variant='p' className='text-sm opacity-80'>
          Kéo tệp vào đây hoặc bấm
          <Typography
            variant='span'
            className='mx-1 inline-block underline lg:text-sm'
          >
            chọn tệp
          </Typography>
          từ máy của bạn
        </Typography>
      </div>
    </div>
  )
}
