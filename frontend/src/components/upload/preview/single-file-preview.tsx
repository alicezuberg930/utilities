import { fileFormat, fileThumb } from '@/components/file-thumbnail'
import { type CustomFile } from '../types'
import { FileAudioIcon, FileIcon, FileVideoIcon } from 'lucide-react'

type Props = {
  file: CustomFile | string | null
}

export default function SingleFilePreview({ file }: Readonly<Props>) {
  if (!file) return null
  let format = ''
  let imgUrl = ''
  if (typeof file === 'string') {
    imgUrl = file
    format = 'image'
  } else {
    format = fileFormat(file.path)
    imgUrl = format === 'image' ? file.preview! : fileThumb(format)
  }

  if (format !== 'image') {
    const PreviewIcon = format === 'audio'
      ? FileAudioIcon
      : format === 'video'
        ? FileVideoIcon
        : FileIcon

    return (
      <div className='absolute inset-2 z-10 flex flex-col items-center justify-center gap-3 rounded-lg bg-muted/70 px-12 text-foreground'>
        <PreviewIcon className='size-12 text-muted-foreground' />
        <p className='max-w-full truncate text-sm font-medium'>
          {typeof file === 'string' ? file.split('/').pop() : file.name}
        </p>
      </div>
    )
  }

  return (
    <img
      alt='Xem trước tệp'
      src={imgUrl}
      className={`absolute top-2 left-2 z-10 h-[calc(100%-16px)] w-[calc(100%-16px)] rounded-lg ${format !== 'image' ? 'object-contain p-8' : 'object-cover'}`}
    />
  )
}
