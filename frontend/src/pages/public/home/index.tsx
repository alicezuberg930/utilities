import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Progress } from "@/components/ui/progress"
import { httpClient } from "@/lib/repository/http-client"
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
    const data = await httpClient.get('/youtube/info', {
      url: "https://music.youtube.com/watch?v=mjd1CD6VSck"
    })
    console.log(data)
  }

  return (
    <>
      <Input value={url} onChange={(e) => setUrl(e.currentTarget.value)} />
      <Button onClick={download}>Download</Button>
      <Progress value={progress} />
    </>
  )
}

export default HomePage