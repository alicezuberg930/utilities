import { createUploadImage, type CustomFile, Upload } from '@/components/upload'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Separator } from '@/components/ui/separator'
import { Slider } from '@/components/ui/slider'
import {
  AlertCircleIcon,
  CropIcon,
  DownloadIcon,
  ImagePlusIcon,
  LoaderCircleIcon,
  RefreshCcwIcon,
  RotateCcwIcon,
  RotateCwIcon,
  UploadCloudIcon,
  ZoomInIcon,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { getAbsoluteZoom, getZoomFactor } from 'advanced-cropper/extensions/absolute-zoom'
import { Cropper, type CropperRef } from 'react-advanced-cropper'
import 'react-advanced-cropper/dist/style.css'
import { formatByte } from '@/lib/utils'

const MAX_FILE_SIZE = 20 * 1024 * 1024

const ASPECT_RATIOS = [
  { value: 'free', label: 'Free', ratio: undefined },
  { value: '1:1', label: 'Square (1:1)', ratio: 1 },
  { value: '4:3', label: 'Landscape (4:3)', ratio: 4 / 3 },
  { value: '3:2', label: 'Photo (3:2)', ratio: 3 / 2 },
  { value: '16:9', label: 'Widescreen (16:9)', ratio: 16 / 9 },
  { value: '4:5', label: 'Portrait (4:5)', ratio: 4 / 5 },
] as const

type AspectRatioValue = (typeof ASPECT_RATIOS)[number]['value']
type CropSize = { width: number; height: number }

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

const getDownloadName = (fileName: string) => {
  const baseName = fileName.replace(/\.[^/.]+$/, '').trim() || 'image'
  return `${baseName}-cropped.png`
}

export default function CropImagePage() {
  const cropperRef = useRef<CropperRef>(null)
  const [image, setImage] = useState<CustomFile | null>(null)
  const [aspectRatio, setAspectRatio] = useState<AspectRatioValue>('free')
  const [zoom, setZoom] = useState(0)
  const [rotation, setRotation] = useState(0)
  const [cropSize, setCropSize] = useState<CropSize | null>(null)
  const [isReady, setIsReady] = useState(false)
  const [isExporting, setIsExporting] = useState(false)
  const [editorError, setEditorError] = useState<string | null>(null)

  const imageSource = image?.preview ?? null
  const selectedAspectRatio = useMemo(
    () => ASPECT_RATIOS.find(option => option.value === aspectRatio)?.ratio,
    [aspectRatio]
  )

  useEffect(() => {
    return () => {
      if (imageSource?.startsWith('blob:')) URL.revokeObjectURL(imageSource)
    }
  }, [imageSource])

  const resetEditorState = useCallback(() => {
    setZoom(0)
    setRotation(0)
    setCropSize(null)
    setIsReady(false)
    setIsExporting(false)
    setEditorError(null)
  }, [])

  const handleDrop = useCallback((acceptedFiles: File[]) => {
    const selectedFile = acceptedFiles[0]
    if (!selectedFile) return

    resetEditorState()
    setImage(createUploadImage(selectedFile))
  }, [resetEditorState])

  const handleChooseAnother = useCallback(() => {
    setImage(null)
    resetEditorState()
  }, [resetEditorState])

  const syncCropperState = useCallback((cropper: CropperRef) => {
    const coordinates = cropper.getCoordinates({ round: true })
    setCropSize(previous => {
      if (!coordinates) return null
      const next = { width: coordinates.width, height: coordinates.height }
      return previous?.width === next.width && previous.height === next.height ? previous : next
    })

    const absoluteZoom = getAbsoluteZoom(cropper.getState(), cropper.getSettings())
    if (Number.isFinite(absoluteZoom)) {
      const nextZoom = Math.round(absoluteZoom * 100)
      setZoom(previous => previous === nextZoom ? previous : nextZoom)
    }

    const roundedRotation = Math.round(cropper.getTransforms().rotate)
    const nextRotation = ((roundedRotation % 360) + 360) % 360
    setRotation(previous => previous === nextRotation ? previous : nextRotation)
  }, [])

  const handleZoomChange = useCallback((value: number | number[]) => {
    const nextZoom = Array.isArray(value) ? value[0] : value
    const cropper = cropperRef.current
    if (!cropper || nextZoom === undefined) return

    const factor = getZoomFactor(cropper.getState(), cropper.getSettings(), nextZoom / 100)
    if (Number.isFinite(factor) && factor > 0) {
      cropper.zoomImage(factor, { transitions: false })
      setZoom(nextZoom)
    }
  }, [])

  const handleRotate = useCallback((degrees: number) => {
    cropperRef.current?.rotateImage(degrees)
  }, [])

  const handleReset = useCallback(() => {
    cropperRef.current?.reset()
    setRotation(0)
    setEditorError(null)
  }, [])

  const handleDownload = useCallback(() => {
    const canvas = cropperRef.current?.getCanvas({
      imageSmoothingEnabled: true,
      imageSmoothingQuality: 'high',
      maxWidth: 4096,
      maxHeight: 4096,
    })

    if (!canvas || !image) {
      setEditorError('The cropped image is not ready yet. Please try again.')
      return
    }

    setEditorError(null)
    setIsExporting(true)

    canvas.toBlob(blob => {
      setIsExporting(false)

      if (!blob) {
        setEditorError('The browser could not create the cropped image.')
        return
      }

      const resultUrl = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = resultUrl
      link.download = getDownloadName(image.name)
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.setTimeout(() => URL.revokeObjectURL(resultUrl), 1000)
    }, 'image/png')
  }, [image])

  return (
    <section className='mx-auto w-full max-w-6xl py-8 md:py-14'>
      <Card className='shadow-sm'>
        <CardHeader className='border-b'>
          <CardTitle className='flex items-center gap-2 text-xl'>
            <CropIcon className='size-5' />
            Crop image
          </CardTitle>
          <CardDescription>
            Crop, zoom, and rotate an image directly in your browser. Touch and pinch gestures are supported.
          </CardDescription>
          {image && (
            <CardAction>
              <Button type='button' variant='outline' disabled={isExporting} onClick={handleChooseAnother}>
                <ImagePlusIcon /> Choose another
              </Button>
            </CardAction>
          )}
        </CardHeader>

        <CardContent>
          {!image || !imageSource ? (
            <Upload
              accept={{
                'image/jpeg': ['.jpg', '.jpeg'],
                'image/png': ['.png'],
                'image/webp': ['.webp'],
              }}
              maxFiles={1}
              maxSize={MAX_FILE_SIZE}
              multiple={false}
              onDrop={handleDrop}
              placeholder={<UploadPlaceholder />}
              className='min-h-64 aspect-auto border-2 border-border bg-muted/25 p-6 text-foreground hover:bg-muted/50 hover:opacity-100'
            />
          ) : (
            <div className='grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]'>
              <div className='min-w-0 space-y-3'>
                <div className='relative h-[52vh] min-h-80 max-h-[640px] overflow-hidden rounded-xl border bg-zinc-950'>
                  <Cropper
                    ref={cropperRef}
                    src={imageSource}
                    className='h-full w-full'
                    stencilProps={{
                      aspectRatio: selectedAspectRatio,
                      grid: true,
                    }}
                    backgroundWrapperProps={{
                      scaleImage: { touch: true, wheel: { ratio: 0.1 } },
                      moveImage: { touch: true, mouse: true },
                      rotateImage: false,
                    }}
                    onChange={syncCropperState}
                    onReady={cropper => {
                      setIsReady(true)
                      setEditorError(null)
                      syncCropperState(cropper)
                    }}
                    onError={() => {
                      setIsReady(false)
                      setEditorError('This image could not be loaded. Try a different file.')
                    }}
                  />

                  {!isReady && !editorError && (
                    <div className='absolute inset-0 flex items-center justify-center bg-zinc-950/80 text-white'>
                      <LoaderCircleIcon className='size-6 animate-spin' />
                      <span className='ml-2 text-sm'>Loading editor</span>
                    </div>
                  )}
                </div>

                <div className='flex flex-col gap-2 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between'>
                  <p>Drag to position. Pinch or use the mouse wheel to zoom.</p>
                  {cropSize && (
                    <Badge variant='secondary' className='w-fit tabular-nums'>
                      {cropSize.width} × {cropSize.height} px
                    </Badge>
                  )}
                </div>
              </div>

              <Card size='sm' className='h-fit'>
                <CardHeader>
                  <CardTitle>Adjust image</CardTitle>
                  <CardDescription>Fine-tune the crop before downloading it.</CardDescription>
                </CardHeader>

                <CardContent className='space-y-5'>
                  <div className='space-y-2'>
                    <Label htmlFor='crop-aspect-ratio'>Aspect ratio</Label>
                    <Select
                      value={aspectRatio}
                      disabled={!isReady}
                      onValueChange={value => {
                        if (value) setAspectRatio(value as AspectRatioValue)
                      }}
                    >
                      <SelectTrigger id='crop-aspect-ratio' className='w-full'>
                        <SelectValue placeholder='Choose an aspect ratio' />
                      </SelectTrigger>
                      <SelectContent alignItemWithTrigger={false}>
                        {ASPECT_RATIOS.map(option => (
                          <SelectItem key={option.value} value={option.value}>
                            {option.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className='space-y-3'>
                    <div className='flex items-center justify-between gap-3'>
                      <Label id='crop-zoom-label'>Zoom</Label>
                      <span className='text-xs text-muted-foreground tabular-nums'>{zoom}%</span>
                    </div>
                    <div className='flex items-center gap-3'>
                      <ZoomInIcon className='size-4 text-muted-foreground' />
                      <Slider
                        id='crop-zoom'
                        min={0}
                        max={100}
                        step={1}
                        value={[zoom]}
                        disabled={!isReady}
                        onValueChange={handleZoomChange}
                        aria-labelledby='crop-zoom-label'
                      />
                    </div>
                  </div>

                  <div className='space-y-2'>
                    <div className='flex items-center justify-between gap-3'>
                      <Label>Rotation</Label>
                      <span className='text-xs text-muted-foreground tabular-nums'>{rotation}°</span>
                    </div>
                    <div className='grid grid-cols-2 gap-2'>
                      <Button type='button' variant='outline' disabled={!isReady} onClick={() => handleRotate(-90)}>
                        <RotateCcwIcon /> Left
                      </Button>
                      <Button type='button' variant='outline' disabled={!isReady} onClick={() => handleRotate(90)}>
                        <RotateCwIcon /> Right
                      </Button>
                    </div>
                  </div>

                  <Button type='button' variant='secondary' className='w-full' disabled={!isReady} onClick={handleReset}>
                    <RefreshCcwIcon /> Reset adjustments
                  </Button>

                  <Separator />

                  <div className='space-y-1 rounded-lg bg-muted/50 p-3'>
                    <p className='truncate text-sm font-medium' title={image.name}>{image.name}</p>
                    <p className='text-xs text-muted-foreground'>
                      {formatByte(image.size)} MB · Processed locally
                    </p>
                  </div>

                  {editorError && (
                    <Alert variant='destructive'>
                      <AlertCircleIcon />
                      <AlertTitle>Unable to crop image</AlertTitle>
                      <AlertDescription>{editorError}</AlertDescription>
                    </Alert>
                  )}

                  <Button
                    type='button'
                    size='lg'
                    className='w-full'
                    disabled={!isReady || isExporting}
                    onClick={handleDownload}
                  >
                    {isExporting ? <LoaderCircleIcon className='animate-spin' /> : <DownloadIcon />}
                    {isExporting ? 'Preparing image' : 'Download cropped image'}
                  </Button>
                </CardContent>
              </Card>
            </div>
          )}
        </CardContent>
      </Card>
    </section>
  )
}
