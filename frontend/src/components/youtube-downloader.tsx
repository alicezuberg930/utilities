import type {
  FilteredMetadata,
  YoutubeAudioQuality,
  YoutubeDownloadFormat,
  YoutubeFailureEvent,
  YoutubeProgressEvent,
  YoutubeVideoQuality,
} from '@/@types/youtube'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Progress, ProgressLabel } from '@/components/ui/progress'
import { Spinner } from '@/components/ui/spinner'
import {
  createYoutubeJob,
  getYoutubeFileUrl,
  getYoutubeInfo,
  getYoutubeProgressUrl,
} from '@/services/api.service'
import {
  AlertCircleIcon,
  CheckCircle2Icon,
  DownloadIcon,
  MusicIcon,
  SearchIcon,
  VideoIcon,
} from 'lucide-react'
import { type FormEvent, useEffect, useRef, useState } from 'react'

const AUDIO_QUALITIES: YoutubeAudioQuality[] = ['128kbps', '196kbps', '320kbps']
const VIDEO_QUALITIES: YoutubeVideoQuality[] = ['360p', '480p', '720p', '1080p']

type DownloadStatus =
  | 'idle'
  | 'inspecting'
  | 'ready'
  | 'creating'
  | 'downloading'
  | 'completed'
  | 'failed'

const getErrorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'The request failed. Please try again.'

const parseEvent = <T,>(event: MessageEvent<string>): T | null => {
  try {
    return JSON.parse(event.data) as T
  } catch {
    return null
  }
}

const triggerFileDownload = (jobId: string) => {
  const anchor = document.createElement('a')
  anchor.href = getYoutubeFileUrl(jobId)
  anchor.style.display = 'none'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
}

export default function YoutubeDownloader() {
  const [url, setUrl] = useState('')
  const [metadata, setMetadata] = useState<FilteredMetadata | null>(null)
  const [format, setFormat] = useState<YoutubeDownloadFormat>('video')
  const [audioQuality, setAudioQuality] = useState<YoutubeAudioQuality>('320kbps')
  const [videoQuality, setVideoQuality] = useState<YoutubeVideoQuality>('720p')
  const [status, setStatus] = useState<DownloadStatus>('idle')
  const [progress, setProgress] = useState(0)
  const [jobId, setJobId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const eventSourceRef = useRef<EventSource | null>(null)
  const terminalEventRef = useRef(false)

  const closeProgressStream = () => {
    eventSourceRef.current?.close()
    eventSourceRef.current = null
  }

  useEffect(() => closeProgressStream, [])

  const inspectUrl = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const normalizedUrl = url.trim()
    if (!normalizedUrl) {
      setError('Enter a YouTube URL first.')
      return
    }

    closeProgressStream()
    setError(null)
    setMetadata(null)
    setJobId(null)
    setProgress(0)
    setStatus('inspecting')

    try {
      const response = await getYoutubeInfo(normalizedUrl)
      setMetadata(response.data)
      setStatus('ready')
    } catch (requestError) {
      setError(getErrorMessage(requestError))
      setStatus('failed')
    }
  }

  const monitorJob = (createdJobId: string) => {
    closeProgressStream()
    terminalEventRef.current = false

    const eventSource = new EventSource(getYoutubeProgressUrl(createdJobId), {
      withCredentials: true,
    })
    eventSourceRef.current = eventSource

    eventSource.onmessage = (event) => {
      const data = parseEvent<YoutubeProgressEvent>(event)
      if (data) {
        setProgress(Math.min(100, Math.max(0, Number(data.progress))))
        setStatus('downloading')
      }
    }

    eventSource.addEventListener('complete', () => {
      terminalEventRef.current = true
      eventSource.close()
      eventSourceRef.current = null
      setProgress(100)
      setStatus('completed')
      triggerFileDownload(createdJobId)
    })

    eventSource.addEventListener('error', (event) => {
      if (event instanceof MessageEvent) {
        const data = parseEvent<YoutubeFailureEvent>(event)
        terminalEventRef.current = true
        eventSource.close()
        eventSourceRef.current = null
        setError(data?.message ?? 'The download failed.')
        setStatus('failed')
        return
      }

      if (!terminalEventRef.current) {
        eventSource.close()
        eventSourceRef.current = null
        setError('The progress connection was interrupted.')
        setStatus('failed')
      }
    })
  }

  const startDownload = async () => {
    if (!metadata) return

    setError(null)
    setProgress(0)
    setStatus('creating')

    try {
      const response = await createYoutubeJob({
        url: url.trim(),
        format,
        ...(format === 'audio'
          ? { audioQuality }
          : { videoQuality }),
      })

      setJobId(response.data.jobId)
      setStatus('downloading')
      monitorJob(response.data.jobId)
    } catch (requestError) {
      setError(getErrorMessage(requestError))
      setStatus('failed')
    }
  }

  const isBusy = status === 'inspecting' || status === 'creating' || status === 'downloading'
  const activeQualities = format === 'audio' ? AUDIO_QUALITIES : VIDEO_QUALITIES
  const selectedQuality = format === 'audio' ? audioQuality : videoQuality

  const selectQuality = (quality: YoutubeAudioQuality | YoutubeVideoQuality) => {
    if (format === 'audio') {
      setAudioQuality(quality as YoutubeAudioQuality)
    } else {
      setVideoQuality(quality as YoutubeVideoQuality)
    }
  }

  return (
    <section className='mx-auto w-full max-w-4xl py-8 md:py-14'>
      <Card className='gap-6 p-1 shadow-sm'>
        <CardHeader className='gap-2 border-b pb-5'>
          <CardTitle className='text-2xl'>YouTube downloader</CardTitle>
          <CardDescription>
            Inspect a public YouTube link, choose an output format, and download the processed file.
          </CardDescription>
        </CardHeader>

        <CardContent className='space-y-6 pb-5'>
          <form className='flex flex-col gap-3 sm:flex-row' onSubmit={inspectUrl}>
            <Input
              type='url'
              inputMode='url'
              placeholder='https://www.youtube.com/watch?v=...'
              value={url}
              disabled={isBusy}
              aria-label='YouTube URL'
              onChange={(event) => {
                setUrl(event.currentTarget.value)
                setMetadata(null)
                setError(null)
                if (!isBusy) setStatus('idle')
              }}
              className='h-10 flex-1'
            />
            <Button type='submit' size='lg' disabled={isBusy || !url.trim()}>
              {status === 'inspecting' ? <Spinner /> : <SearchIcon />}
              {status === 'inspecting' ? 'Inspecting' : 'Inspect link'}
            </Button>
          </form>

          {error && (
            <Alert variant='destructive'>
              <AlertCircleIcon />
              <AlertTitle>Unable to continue</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {metadata && (
            <div className='grid gap-6 md:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]'>
              <div className='overflow-hidden rounded-xl border bg-muted/30'>
                {metadata.thumbnail ? (
                  <img
                    src={metadata.thumbnail}
                    alt={metadata.title ?? 'YouTube thumbnail'}
                    className='aspect-video h-auto w-full object-cover'
                  />
                ) : (
                  <div className='flex aspect-video items-center justify-center text-muted-foreground'>
                    No thumbnail available
                  </div>
                )}
                <div className='space-y-2 p-4'>
                  <h2 className='line-clamp-2 font-semibold'>
                    {metadata.title ?? 'Untitled video'}
                  </h2>
                  <p className='text-sm text-muted-foreground'>
                    {metadata.videoFormats.length} video streams · {metadata.audioFormats.length} audio streams
                  </p>
                </div>
              </div>

              <div className='space-y-5'>
                <div className='space-y-2'>
                  <p className='text-sm font-medium'>Output format</p>
                  <div className='grid grid-cols-2 gap-2'>
                    <Button
                      type='button'
                      variant={format === 'video' ? 'default' : 'outline'}
                      size='lg'
                      disabled={isBusy}
                      onClick={() => setFormat('video')}
                    >
                      <VideoIcon /> Video
                    </Button>
                    <Button
                      type='button'
                      variant={format === 'audio' ? 'default' : 'outline'}
                      size='lg'
                      disabled={isBusy}
                      onClick={() => setFormat('audio')}
                    >
                      <MusicIcon /> Audio
                    </Button>
                  </div>
                </div>

                <div className='space-y-2'>
                  <p className='text-sm font-medium'>Quality</p>
                  <div className='flex flex-wrap gap-2'>
                    {activeQualities.map((quality) => (
                      <Button
                        key={quality}
                        type='button'
                        variant={selectedQuality === quality ? 'secondary' : 'outline'}
                        disabled={isBusy}
                        onClick={() => selectQuality(quality)}
                      >
                        {quality}
                      </Button>
                    ))}
                  </div>
                </div>

                <Button
                  type='button'
                  size='lg'
                  className='w-full'
                  disabled={isBusy}
                  onClick={startDownload}
                >
                  {status === 'creating' || status === 'downloading' ? <Spinner /> : <DownloadIcon />}
                  {status === 'creating' ? 'Creating job' : status === 'downloading' ? 'Downloading' : 'Start download'}
                </Button>
              </div>
            </div>
          )}

          {jobId && (
            <div className='space-y-3 rounded-xl border p-4' aria-live='polite'>
              <Progress value={progress}>
                <ProgressLabel>
                  {status === 'completed' ? 'Download complete' : 'Processing download'}
                </ProgressLabel>
                <span className='ml-auto text-sm text-muted-foreground tabular-nums'>
                  {Math.round(progress)}%
                </span>
              </Progress>

              {status === 'completed' && (
                <div className='flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between'>
                  <p className='flex items-center gap-2 text-sm text-emerald-600 dark:text-emerald-400'>
                    <CheckCircle2Icon className='size-4' />
                    Your file download has started.
                  </p>
                  <Button type='button' variant='outline' onClick={() => triggerFileDownload(jobId)}>
                    <DownloadIcon /> Download again
                  </Button>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </section>
  )
}
