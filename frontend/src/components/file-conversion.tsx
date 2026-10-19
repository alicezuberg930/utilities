import type {
  FileConversionFailureEvent,
  FileConversionFormat,
  FileConversionProgressEvent,
} from '@/@types/file-conversion'
import { FormProvider, RHFUpload } from '@/components/hook-form'
import type { CustomFile } from '@/components/upload'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldError, FieldLabel } from '@/components/ui/field'
import { Progress, ProgressLabel } from '@/components/ui/progress'
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Spinner } from '@/components/ui/spinner'
import {
  createFileConversionJob,
  getConvertedFileUrl,
  getFileConversionProgressUrl,
} from '@/services/api.service'
import {
  AlertCircleIcon,
  CheckCircle2Icon,
  DownloadIcon,
  RefreshCwIcon,
  UploadCloudIcon,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Controller, type Resolver, useForm, useWatch } from 'react-hook-form'
import type { FileRejection } from 'react-dropzone'
import { z } from 'zod'

const AUDIO_INPUTS = new Set(['aac', 'flac', 'm4a', 'mp3', 'ogg', 'opus', 'wav', 'wma'])
const VIDEO_INPUTS = new Set(['avi', 'flv', 'm4v', 'mkv', 'mov', 'mp4', 'mpeg', 'mpg', 'webm', 'wmv'])
const CAD_INPUTS = new Set(['dwg', 'dxf', 'dws', 'dwt'])
const AUDIO_OUTPUTS: FileConversionFormat[] = ['mp3', 'wav', 'm4a', 'aac', 'flac', 'ogg', 'opus']
const VIDEO_OUTPUTS: FileConversionFormat[] = ['mp4', 'webm', 'mov', 'mkv', 'avi']
const CAD_OUTPUTS: FileConversionFormat[] = ['glb']
const ALL_OUTPUTS = [...AUDIO_OUTPUTS, ...VIDEO_OUTPUTS, ...CAD_OUTPUTS]
const FILE_ACCEPT = {
  'audio/*': [...AUDIO_INPUTS].map(format => `.${format}`),
  'video/*': [...VIDEO_INPUTS].map(format => `.${format}`),
  'application/octet-stream': [...CAD_INPUTS].map(format => `.${format}`),
  'image/vnd.dwg': ['.dwg'],
  'image/vnd.dxf': ['.dxf'],
}

type ConversionStatus = 'idle' | 'ready' | 'uploading' | 'converting' | 'completed' | 'failed'

type FileConversionFormValues = {
  file: CustomFile | null
  convertedFormat: FileConversionFormat | ''
}

const getExtension = (fileName: string) => fileName.split('.').pop()?.toLowerCase() ?? ''

const isSupportedSource = (value: unknown): value is CustomFile => {
  if (!(value instanceof File)) return false
  const extension = getExtension(value.name)
  return AUDIO_INPUTS.has(extension) || VIDEO_INPUTS.has(extension) || CAD_INPUTS.has(extension)
}

const getValidOutputs = (sourceFormat: string): FileConversionFormat[] => {
  if (AUDIO_INPUTS.has(sourceFormat)) return AUDIO_OUTPUTS
  if (VIDEO_INPUTS.has(sourceFormat)) return [...VIDEO_OUTPUTS, ...AUDIO_OUTPUTS]
  if (CAD_INPUTS.has(sourceFormat)) return CAD_OUTPUTS
  return []
}

const fileConversionSchema = z.object({
  file: z.custom<CustomFile | null>(isSupportedSource, {
    message: 'Select a supported audio, video, or AutoCAD drawing file.',
  }),
  convertedFormat: z.custom<FileConversionFormat | ''>(
    value => typeof value === 'string' && ALL_OUTPUTS.includes(value as FileConversionFormat),
    { message: 'Choose an output format.' }
  ),
}).superRefine((values, context) => {
  if (!isSupportedSource(values.file) || !values.convertedFormat) return

  const sourceFormat = getExtension(values.file.name)
  const validOutputs = getValidOutputs(sourceFormat)

  if (sourceFormat === values.convertedFormat || !validOutputs.includes(values.convertedFormat)) {
    context.addIssue({
      code: 'custom',
      path: ['convertedFormat'],
      message: `Conversion from ${sourceFormat.toUpperCase()} to ${values.convertedFormat.toUpperCase()} is not supported.`,
    })
  }
})

const zodResolver: Resolver<FileConversionFormValues> = async (values) => {
  const result = fileConversionSchema.safeParse(values)
  if (result.success) return { values: result.data, errors: {} }

  return {
    values: {},
    errors: result.error.issues.reduce<Record<string, { type: string; message: string }>>((errors, issue) => {
      const fieldName = issue.path[0]
      if (typeof fieldName === 'string' && !errors[fieldName]) {
        errors[fieldName] = {
          type: issue.code,
          message: issue.message,
        }
      }
      return errors
    }, {}),
  }
}

const parseEvent = <T,>(event: MessageEvent<string>): T | null => {
  try {
    return JSON.parse(event.data) as T
  } catch {
    return null
  }
}

const startBrowserDownload = (jobId: string) => {
  const anchor = document.createElement('a')
  anchor.href = getConvertedFileUrl(jobId)
  anchor.style.display = 'none'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
}

const UploadPlaceholder = () => (
  <div className='flex flex-col items-center justify-center gap-4 text-center'>
    <div className='flex size-14 items-center justify-center rounded-full bg-background shadow-sm ring-1 ring-foreground/10'>
      <UploadCloudIcon className='size-7 text-muted-foreground' />
    </div>
    <div className='space-y-1'>
      <p className='font-medium'>Drag and drop an audio, video, or AutoCAD drawing file</p>
      <p className='text-sm text-muted-foreground'>or click to browse from your device</p>
    </div>
    <div className='flex max-w-2xl flex-wrap justify-center gap-1.5'>
      {['MP3', 'WAV', 'MP4', 'MOV', 'DWG', 'DXF', 'GLB'].map(format => (
        <Badge key={format} variant='outline'>{format}</Badge>
      ))}
    </div>
  </div>
)

export default function FileConversion() {
  const form = useForm<FileConversionFormValues>({
    resolver: zodResolver,
    defaultValues: {
      file: null,
      convertedFormat: '',
    },
  })
  const file = useWatch({ control: form.control, name: 'file' })
  const convertedFormat = useWatch({ control: form.control, name: 'convertedFormat' })
  const [status, setStatus] = useState<ConversionStatus>('idle')
  const [progress, setProgress] = useState(0)
  const [jobId, setJobId] = useState<string | null>(null)
  const [requestError, setRequestError] = useState<string | null>(null)
  const eventSourceRef = useRef<EventSource | null>(null)
  const terminalEventRef = useRef(false)

  const sourceFormat = file ? getExtension(file.name) : ''
  const sourceIsVideo = VIDEO_INPUTS.has(sourceFormat)
  const sourceIsCad = CAD_INPUTS.has(sourceFormat)
  const isBusy = status === 'uploading' || status === 'converting'

  const outputFormats = useMemo(() => {
    const formats = getValidOutputs(sourceFormat)
    return formats.filter(format => format !== sourceFormat)
  }, [sourceFormat])

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

  const handleFileDrop = useCallback((acceptedFiles: File[], fileRejections: FileRejection[]) => {
    resetJob()

    const selectedFile = acceptedFiles[0]
    if (!selectedFile) {
      form.setValue('convertedFormat', '')
      setStatus('idle')
      if (fileRejections.length > 0) {
        form.setError('file', {
          type: 'file-invalid-type',
          message: 'That file type is not supported. Choose a supported audio, video, or AutoCAD drawing file.',
        })
      }
      return
    }

    const format = getExtension(selectedFile.name)
    const availableFormats = getValidOutputs(format)
      .filter(output => output !== format)

    form.clearErrors(['file', 'convertedFormat'])
    form.setValue('convertedFormat', availableFormats[0] ?? '', {
      shouldDirty: true,
      shouldValidate: form.formState.isSubmitted,
    })
    setStatus('ready')
  }, [form, resetJob])

  const handleFileDelete = useCallback(() => {
    resetJob()
    form.setValue('convertedFormat', '')
    form.clearErrors()
    setStatus('idle')
  }, [form, resetJob])

  const monitorJob = (createdJobId: string) => {
    closeProgressStream()
    terminalEventRef.current = false

    const eventSource = new EventSource(getFileConversionProgressUrl(createdJobId), {
      withCredentials: true,
    })
    eventSourceRef.current = eventSource

    eventSource.onmessage = (event) => {
      const data = parseEvent<FileConversionProgressEvent>(event)
      if (data) {
        setProgress(Math.min(100, Math.max(0, Number(data.progress))))
        setStatus('converting')
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

    eventSource.addEventListener('error', (event) => {
      if (event instanceof MessageEvent) {
        const data = parseEvent<FileConversionFailureEvent>(event)
        terminalEventRef.current = true
        eventSource.close()
        eventSourceRef.current = null
        setRequestError(data?.message ?? 'The conversion failed.')
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
  }

  const startConversion = async (values: FileConversionFormValues) => {
    if (!isSupportedSource(values.file) || !values.convertedFormat) return

    resetJob()
    setStatus('uploading')

    try {
      const response = await createFileConversionJob(values.file, values.convertedFormat)
      setJobId(response.data.jobId)
      setProgress(response.data.progress)
      setStatus('converting')
      monitorJob(response.data.jobId)
    } catch (error) {
      setRequestError(error instanceof Error ? error.message : 'The request failed. Please try again.')
      setStatus('failed')
    }
  }

  return (
    <section className='mx-auto w-full max-w-6xl py-8 md:py-14'>
      <Card className='shadow-sm'>
        <CardHeader>
          <CardTitle className='text-xl'>File converter</CardTitle>
          <CardDescription>
            Convert audio and video files, or turn DWG, DXF, DWT, and DWS drawings into GLB models.
          </CardDescription>
        </CardHeader>

        <CardContent>
          <FormProvider methods={form} onSubmit={form.handleSubmit(startConversion)}>
            <div className='space-y-6'>
              <RHFUpload
                name='file'
                fieldLabel='Source file'
                accept={FILE_ACCEPT}
                disabled={isBusy}
                maxFiles={1}
                onDrop={handleFileDrop}
                onDelete={handleFileDelete}
                placeholder={<UploadPlaceholder />}
                className='min-h-52 aspect-auto border-2 border-border bg-muted/25 p-6 text-foreground hover:bg-muted/50 hover:opacity-100'
              />

              {file && (
                <div className='grid gap-5 rounded-xl border p-4 md:grid-cols-[minmax(0,1fr)_minmax(16rem,0.7fr)] md:items-end'>
                  <div className='min-w-0 space-y-1'>
                    <p className='text-sm font-medium'>Selected file</p>
                    <p className='truncate text-sm text-muted-foreground' title={file.name}>
                      {file.name} &middot; {sourceFormat.toUpperCase()}
                    </p>
                  </div>

                  <Controller
                    name='convertedFormat'
                    control={form.control}
                    render={({ field, fieldState: { error } }) => (
                      <Field data-invalid={!!error}>
                        <FieldLabel htmlFor={field.name}>Convert to</FieldLabel>
                        <Select
                          value={field.value || null}
                          disabled={isBusy}
                          onValueChange={(value) => {
                            field.onChange(value ?? '')
                            form.clearErrors('convertedFormat')
                            resetJob()
                            setStatus(file ? 'ready' : 'idle')
                          }}
                        >
                          <SelectTrigger id={field.name} className='h-10 w-full' aria-invalid={!!error}>
                            <SelectValue placeholder='Choose a format' />
                          </SelectTrigger>
                          <SelectContent alignItemWithTrigger={false}>
                            {sourceIsCad && (
                              <SelectGroup>
                                <SelectLabel>3D model</SelectLabel>
                                {outputFormats.filter(format => CAD_OUTPUTS.includes(format)).map(format => (
                                  <SelectItem key={format} value={format}>{format.toUpperCase()}</SelectItem>
                                ))}
                              </SelectGroup>
                            )}
                            {sourceIsVideo && (
                              <SelectGroup>
                                <SelectLabel>Video</SelectLabel>
                                {outputFormats.filter(format => VIDEO_OUTPUTS.includes(format)).map(format => (
                                  <SelectItem key={format} value={format}>{format.toUpperCase()}</SelectItem>
                                ))}
                              </SelectGroup>
                            )}
                            {!sourceIsCad && (
                              <SelectGroup>
                                <SelectLabel>{sourceIsVideo ? 'Audio' : 'Audio formats'}</SelectLabel>
                                {outputFormats.filter(format => AUDIO_OUTPUTS.includes(format)).map(format => (
                                  <SelectItem key={format} value={format}>{format.toUpperCase()}</SelectItem>
                                ))}
                              </SelectGroup>
                            )}
                          </SelectContent>
                        </Select>
                        {!!error && <FieldError errors={[error]} />}
                      </Field>
                    )}
                  />
                </div>
              )}

              {requestError && (
                <Alert variant='destructive'>
                  <AlertCircleIcon />
                  <AlertTitle>Unable to convert file</AlertTitle>
                  <AlertDescription>{requestError}</AlertDescription>
                </Alert>
              )}

              {file && (
                <Button
                  type='submit'
                  size='lg'
                  className='w-full'
                  disabled={isBusy || !convertedFormat}
                >
                  {isBusy ? <Spinner /> : <RefreshCwIcon />}
                  {status === 'uploading'
                    ? 'Uploading file'
                    : status === 'converting'
                      ? 'Converting file'
                      : status === 'failed'
                        ? 'Try conversion again'
                        : status === 'completed'
                          ? 'Convert again'
                          : 'Convert file'}
                </Button>
              )}

              {jobId && (
                <div className='space-y-3 rounded-xl border p-4' aria-live='polite'>
                  <Progress value={progress}>
                    <ProgressLabel>
                      {status === 'completed' ? 'Conversion complete' : 'Processing conversion'}
                    </ProgressLabel>
                    <span className='ml-auto text-sm text-muted-foreground tabular-nums'>
                      {Math.round(progress)}%
                    </span>
                  </Progress>

                  {status === 'completed' && (
                    <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
                      <p className='flex items-center gap-2 text-sm text-emerald-600 dark:text-emerald-400'>
                        <CheckCircle2Icon className='size-4' />
                        Your converted file is ready.
                      </p>
                      <Button type='button' variant='outline' onClick={() => startBrowserDownload(jobId)}>
                        <DownloadIcon /> Download again
                      </Button>
                    </div>
                  )}
                </div>
              )}
            </div>
          </FormProvider>
        </CardContent>
      </Card>
    </section>
  )
}
