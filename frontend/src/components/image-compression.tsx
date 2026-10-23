import type {
  ImageCompressionFailureEvent,
  ImageCompressionProgressEvent,
} from '@/@types/image-compression'
import { createUploadImage, type CustomFile, Upload } from '@/components/upload'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Progress, ProgressLabel } from '@/components/ui/progress'
import { Slider } from '@/components/ui/slider'
import { Spinner } from '@/components/ui/spinner'
import { Switch } from '@/components/ui/switch'
import { formatByte } from '@/lib/utils'
import {
  createImageCompressionJob,
  getCompressedImageUrl,
  getImageCompressionProgressUrl,
} from '@/services/api.service'
import {
  AlertCircleIcon,
  CheckCircle2Icon,
  DownloadIcon,
  ImageDownIcon,
  RefreshCwIcon,
  UploadCloudIcon,
} from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { FileRejection } from 'react-dropzone'

const MAX_FILE_SIZE = 20 * 1024 * 1024
const DEFAULT_QUALITY = 80

type CompressionStatus =
  | 'idle'
  | 'ready'
  | 'uploading'
  | 'compressing'
  | 'completed'
  | 'failed'

const parseEvent = <T,>(event: MessageEvent<string>): T | null => {
  try {
    return JSON.parse(event.data) as T
  } catch {
    return null
  }
}

const startBrowserDownload = (jobId: string) => {
  const anchor = document.createElement('a')
  anchor.href = getCompressedImageUrl(jobId)
  anchor.style.display = 'none'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
}

const getExtension = (fileName: string) => fileName.split('.').pop()?.toLowerCase() ?? ''

const getOutputFormat = (fileName: string, convertToWebp: boolean) => {
  const extension = getExtension(fileName)
  if (convertToWebp || extension === 'webp') return 'WEBP'
  return extension === 'png' ? 'PNG' : 'JPG'
}

const UploadPlaceholder = () => (
  <div className='flex flex-col items-center justify-center gap-4 text-center'>
    <div className='flex size-14 items-center justify-center rounded-full bg-background shadow-sm ring-1 ring-foreground/10'>
      <UploadCloudIcon className='size-7 text-muted-foreground' />
    </div>
    <div className='space-y-1'>
      <p className='font-medium'>Drag and drop an image</p>
      <p className='text-sm text-muted-foreground'>or click to browse from your device</p>
    </div>
    <div className='flex flex-wrap justify-center gap-1.5'>
      {['JPG', 'PNG', 'WEBP', 'Up to 20 MB'].map(item => (
        <Badge key={item} variant='outline'>{item}</Badge>
      ))}
    </div>
  </div>
)

export default function ImageCompression() {
  const [file, setFile] = useState<CustomFile | null>(null)
  const [quality, setQuality] = useState(DEFAULT_QUALITY)
  const [convertToWebp, setConvertToWebp] = useState(false)
  const [status, setStatus] = useState<CompressionStatus>('idle')
  const [progress, setProgress] = useState(0)
  const [jobId, setJobId] = useState<string | null>(null)
  const [requestError, setRequestError] = useState<string | null>(null)
  const eventSourceRef = useRef<EventSource | null>(null)
  const terminalEventRef = useRef(false)

  const isBusy = status === 'uploading' || status === 'compressing'
  const fileExtension = file ? getExtension(file.name) : ''
  const isWebpInput = fileExtension === 'webp'
  const isLosslessPng = fileExtension === 'png' && !convertToWebp

  const closeProgressStream = useCallback(() => {
    eventSourceRef.current?.close()
    eventSourceRef.current = null
  }, [])

  useEffect(() => closeProgressStream, [closeProgressStream])

  useEffect(() => {
    const previewUrl = file?.preview
    return () => {
      if (previewUrl?.startsWith('blob:')) URL.revokeObjectURL(previewUrl)
    }
  }, [file])

  const resetJob = useCallback(() => {
    closeProgressStream()
    terminalEventRef.current = false
    setJobId(null)
    setProgress(0)
    setRequestError(null)
  }, [closeProgressStream])

  const handleDrop = useCallback((acceptedFiles: File[], fileRejections: FileRejection[]) => {
    resetJob()
    setConvertToWebp(false)
    const selectedFile = acceptedFiles[0]

    if (!selectedFile) {
      setStatus('idle')
      if (fileRejections.length > 0) {
        setRequestError('Choose a JPEG, PNG, or WebP image up to 20 MB.')
      }
      return
    }

    setFile(createUploadImage(selectedFile))
    setStatus('ready')
  }, [resetJob])

  const handleDelete = useCallback(() => {
    resetJob()
    setFile(null)
    setConvertToWebp(false)
    setStatus('idle')
  }, [resetJob])

  const monitorJob = useCallback((createdJobId: string) => {
    closeProgressStream()
    terminalEventRef.current = false

    const eventSource = new EventSource(getImageCompressionProgressUrl(createdJobId), {
      withCredentials: true,
    })
    eventSourceRef.current = eventSource

    eventSource.onmessage = event => {
      const data = parseEvent<ImageCompressionProgressEvent>(event)
      if (data) {
        setProgress(Math.min(100, Math.max(0, Number(data.progress))))
        setStatus('compressing')
      }
    }

    eventSource.addEventListener('complete', () => {
      terminalEventRef.current = true
      eventSource.close()
      eventSourceRef.current = null
      setProgress(100)
      setStatus('completed')
      startBrowserDownload(createdJobId)
    })

    eventSource.addEventListener('error', event => {
      if (event instanceof MessageEvent) {
        const data = parseEvent<ImageCompressionFailureEvent>(event)
        terminalEventRef.current = true
        eventSource.close()
        eventSourceRef.current = null
        setRequestError(data?.message ?? 'The image compression failed.')
        setStatus('failed')
        return
      }

      if (!terminalEventRef.current) {
        eventSource.close()
        eventSourceRef.current = null
        setRequestError('The progress connection was interrupted.')
        setStatus('failed')
      }
    })
  }, [closeProgressStream])

  const startCompression = async () => {
    if (!file || isBusy) return

    resetJob()
    setStatus('uploading')

    try {
      const response = await createImageCompressionJob(file, quality, convertToWebp)
      setJobId(response.data.jobId)
      setProgress(response.data.progress)
      setStatus('compressing')
      monitorJob(response.data.jobId)
    } catch (error) {
      setRequestError(error instanceof Error ? error.message : 'The request failed. Please try again.')
      setStatus('failed')
    }
  }

  return (
    <section className='mx-auto w-full max-w-5xl py-8 md:py-14'>
      <Card className='shadow-sm'>
        <CardHeader className='border-b'>
          <CardTitle className='flex items-center gap-2 text-xl'>
            <ImageDownIcon className='size-5' />
            Compress image
          </CardTitle>
          <CardDescription>
            Reduce JPEG, PNG, or WebP file size with server-side SkiaSharp compression.
          </CardDescription>
        </CardHeader>

        <CardContent>
          <div className='grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]'>
            <div className='space-y-3'>
              <Upload
                accept={{
                  'image/jpeg': ['.jpg', '.jpeg'],
                  'image/png': ['.png'],
                  'image/webp': ['.webp'],
                }}
                file={file}
                maxFiles={1}
                maxSize={MAX_FILE_SIZE}
                multiple={false}
                disabled={isBusy}
                onDrop={handleDrop}
                onDelete={handleDelete}
                placeholder={<UploadPlaceholder />}
                className='min-h-72 aspect-auto border-2 border-border bg-muted/25 p-6 text-foreground hover:bg-muted/50 hover:opacity-100'
              />

              {file && (
                <div className='flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm'>
                  <div className='min-w-0'>
                    <p className='truncate font-medium' title={file.name}>{file.name}</p>
                    <p className='text-xs text-muted-foreground'>{formatByte(file.size)} MB</p>
                  </div>
                  <Badge variant='secondary'>
                    Output: {getOutputFormat(file.name, convertToWebp)}
                  </Badge>
                </div>
              )}
            </div>

            <Card size='sm' className='h-fit'>
              <CardHeader>
                <CardTitle>
                  {isLosslessPng ? 'PNG compression effort' : 'Compression quality'}
                </CardTitle>
                <CardDescription>
                  {isLosslessPng
                    ? 'PNG remains lossless. More effort can produce a smaller file.'
                    : 'Lower quality usually creates a smaller file.'}
                </CardDescription>
              </CardHeader>
              <CardContent className='space-y-5'>
                <div className='flex items-center justify-between gap-4 rounded-lg border p-3'>
                  <div className='space-y-1'>
                    <Label htmlFor='convert-to-webp'>Convert to WebP</Label>
                    <p className='text-xs text-muted-foreground'>
                      {isWebpInput
                        ? 'This image is already WebP.'
                        : 'Optional; the original format is kept by default.'}
                    </p>
                  </div>
                  <Switch
                    id='convert-to-webp'
                    checked={convertToWebp}
                    disabled={!file || isWebpInput || isBusy}
                    onCheckedChange={setConvertToWebp}
                  />
                </div>

                <div className='space-y-3'>
                  <div className='flex items-center justify-between gap-3'>
                    <Label id='image-quality-label'>
                      {isLosslessPng ? 'Compression effort' : 'Quality'}
                    </Label>
                    <span className='text-sm font-medium tabular-nums'>{quality}%</span>
                  </div>
                  <Slider
                    min={1}
                    max={100}
                    step={1}
                    value={[quality]}
                    disabled={isBusy}
                    onValueChange={value => {
                      const nextQuality = Array.isArray(value) ? value[0] : value
                      if (nextQuality !== undefined) setQuality(nextQuality)
                    }}
                    aria-labelledby='image-quality-label'
                  />
                  <div className='flex justify-between text-xs text-muted-foreground'>
                    <span>{isLosslessPng ? 'Faster' : 'Smaller file'}</span>
                    <span>{isLosslessPng ? 'Smaller file' : 'Higher quality'}</span>
                  </div>
                </div>

                {requestError && (
                  <Alert variant='destructive'>
                    <AlertCircleIcon />
                    <AlertTitle>Unable to compress image</AlertTitle>
                    <AlertDescription>{requestError}</AlertDescription>
                  </Alert>
                )}

                <Button
                  type='button'
                  size='lg'
                  className='w-full'
                  disabled={!file || isBusy}
                  onClick={startCompression}
                >
                  {isBusy ? <Spinner /> : <RefreshCwIcon />}
                  {status === 'uploading'
                    ? 'Uploading image'
                    : status === 'compressing'
                      ? 'Compressing image'
                      : status === 'failed'
                        ? 'Try compression again'
                        : status === 'completed'
                          ? 'Compress again'
                          : 'Compress image'}
                </Button>

                {jobId && (
                  <div className='space-y-3 rounded-xl border p-4' aria-live='polite'>
                    <Progress value={progress}>
                      <ProgressLabel>
                        {status === 'completed' ? 'Compression complete' : 'Processing image'}
                      </ProgressLabel>
                      <span className='ml-auto text-sm text-muted-foreground tabular-nums'>
                        {Math.round(progress)}%
                      </span>
                    </Progress>

                    {status === 'completed' && (
                      <div className='space-y-3'>
                        <p className='flex items-center gap-2 text-sm text-emerald-600 dark:text-emerald-400'>
                          <CheckCircle2Icon className='size-4' />
                          Your compressed image is ready.
                        </p>
                        <Button
                          type='button'
                          variant='outline'
                          className='w-full'
                          onClick={() => startBrowserDownload(jobId)}
                        >
                          <DownloadIcon /> Download again
                        </Button>
                      </div>
                    )}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </CardContent>
      </Card>
    </section>
  )
}
