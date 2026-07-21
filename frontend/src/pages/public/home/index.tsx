import type { ApiResponse, FilteredMetadata } from "@/@types"
import { LazyLoadImage } from "@/components/lazy-load-image"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Progress } from "@/components/ui/progress"
import { Typography } from "@/components/ui/typography"
import { httpClient } from "@/lib/repository/http-client"
import { formatByte } from "@/lib/utils"
import { useEffect, useState } from "react"

const HomePage = () => {
  const [progress, setProgress] = useState<number>(0)
  const [id, setId] = useState<string | null>(null)
  const [url, setUrl] = useState<string>("")

  useEffect(() => {
    if (id) {
      const eventSource = new EventSource(`http://localhost:4000/api/v1/youtube/progress/${id}`)

      eventSource.onmessage = event => {
        const data = JSON.parse(event.data)
        setProgress(Number(data.progress))
        console.log(event)
      }

      eventSource.addEventListener("complete", () => {
        eventSource.close()
        const a = document.createElement("a");
        a.href = `http://localhost:4000/api/v1/youtube/file/${id}`
        a.style.display = "none"
        document.body.appendChild(a)
        a.click()
        a.remove()
        setId(null)
      })

      return () => eventSource.close()
    }
  }, [id])

  const download = async () => {
    // const job = await httpClient.post('/youtube/job', {
    //   url,
    //   format: "video",
    //   videoQuality: "360p"
    // })
    // setId(job.data.jobId)
    const data = await httpClient.get<ApiResponse<FilteredMetadata>>('/youtube/info', {
      url: "https://music.youtube.com/watch?v=mjd1CD6VSck"
    })
    setInfo(data.data ?? null)
  }
  const [info, setInfo] = useState<FilteredMetadata | null>(null)

  return (
    <>
      <Input value={url} onChange={(e) => setUrl(e.currentTarget.value)} />
      <Button onClick={download}>Download</Button>
      <Progress value={progress} />
      {info && (
        <>
          <div>
            <LazyLoadImage alt={info.thumbnail} src={info.thumbnail} wrapperClassName="mb-2" className="rounded-md" />
            <Typography variant={'h6'}>{info.title}</Typography>
          </div>
          {info.videoFormats.map(format => (
            <Card>
              <Typography variant={'caption'}>{format.video_ext}</Typography>
              <Typography variant={'caption'}>{format.format}</Typography>
              <Typography variant={'caption'}>{format.resolution}</Typography>
              <Typography variant={'caption'}>{formatByte(format.filesize)} MB</Typography>
            </Card>
          ))}
          {info.audioFormats.map(format => (
            <Card>
              <Typography variant={'caption'}>{format.audio_ext}</Typography>
              <Typography variant={'caption'}>{format.format}</Typography>
              <Typography variant={'caption'}>{format.abr} kbps</Typography>
              <Typography variant={'caption'}>{formatByte(format.filesize)} MB</Typography>
            </Card>
          ))}
        </>
      )}
    </>
  )
}

export default HomePage