import { Controller, Get, Post, Body, Patch, Param, Delete, Query, Res, Req } from '@nestjs/common'
import { AuthRequired, ResponseMessage } from 'src/common/decorators/public.decorator'
import { YoutubeService } from './youtube.service'
import { YoutubeDto } from './dto/youtube.dto'
import { Format } from './dto/enum'
import { Request, Response } from 'express'
import { createReadStream } from 'node:fs'

@Controller('youtube')
export class YoutubeController {
  constructor(private readonly youtubeService: YoutubeService) { }

  @Get('info')
  async getInfo(@Query() query: { url: string }) {
    const info = await this.youtubeService.getInfo(query.url)
    return this.youtubeService.extractStreams(info)
  }

  @ResponseMessage('Job created successfully')
  @Post('job')
  createJob(@Body() data: YoutubeDto) {
    return this.youtubeService.createJob(data)
  }

  @Get("progress/:jobId")
  progress(@Param("jobId") id: string, @Res() res: Response, @Req() req: Request) {
    res.set({
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache",
      Connection: "keep-alive",
    })
    res.flushHeaders()
    this.youtubeService.addClient(id, res)

    req.on("close", () => {
      this.youtubeService.removeClient(id)
    })
  }

  @Get("file/:jobId")
  async file(@Param("jobId") id: string, @Res() res: Response) {
    const job = await this.youtubeService.findOne(id)
    if (job.format === Format.audio) {
      res.setHeader("Content-Type", "audio/mpeg");
    }
    if (job.format === Format.video) {
      res.setHeader("Content-Type", "video/mp4");
    }
    const extension = job.format === Format.audio ? "mp3" : "mp4"
    res.setHeader(
      "Content-Disposition",
      `attachment; filename="${id}.${extension}"`,
    );
    // createReadStream(`/tmp/jobs/${id}.${extension}`).pipe(res)
    res.download(`/tmp/jobs/${id}.${extension}`)
  }
}
