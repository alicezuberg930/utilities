import { type FileRejection } from 'react-dropzone'
// utils
import { fData } from '@/lib/format-number'
import { Paper } from '@/components/ui/paper'
import { Typography } from '@/components/ui/typography'
// components
import { fileData } from '@/components/file-thumbnail'

type Props = {
  fileRejections: readonly FileRejection[]
}

const rejectionMessages: Record<string, string> = {
  'file-invalid-type': 'Định dạng tệp không được hỗ trợ',
  'file-too-large': 'Tệp vượt quá dung lượng cho phép',
  'file-too-small': 'Tệp nhỏ hơn dung lượng tối thiểu',
  'too-many-files': 'Bạn đã chọn quá số lượng tệp cho phép',
}

export default function RejectionFiles({ fileRejections }: Readonly<Props>) {
  if (!fileRejections.length) return null

  return (
    <Paper variant='outline' className='mt-8 border-red-300 bg-red-50 py-2'>
      {fileRejections.map(({ file, errors }) => {
        const { path, size } = fileData(file)

        return (
          <div key={path} className='my-2 flex flex-col'>
            <Typography variant='caption' className='w-fit font-semibold'>
              {path} - {size ? fData(size) : ''}
            </Typography>

            {errors.map((error) => (
              <Typography key={error.code} variant='p' className='m-0'>
                - {rejectionMessages[error.code] ?? 'Không thể tải tệp này lên'}
              </Typography>
            ))}
          </div>
        )
      })}
    </Paper>
  )
}
