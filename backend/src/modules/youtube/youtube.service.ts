import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common'
import { spawn } from 'node:child_process'
import { YoutubeDto } from './dto/youtube.dto'
import { AudioQuality, Format, VideoQuality } from './dto/enum'
import { InjectModel } from '@nestjs/mongoose'
import { Job, JobDocument } from './schemas/job.schema'
import { Model } from 'mongoose'
import { createId } from 'src/common/helpers/create-cuid'
import { Response } from 'express'
import { mkdir } from "node:fs/promises"

@Injectable()
export class YoutubeService {
    constructor(
        @InjectModel(Job.name) private jobModel: Model<JobDocument>,
    ) { }

    private clients = new Map<string, Response>()

    async createJob(data: YoutubeDto) {
        try {
            if (data.format === Format.audio && !Object.values(AudioQuality).includes(data.audioQuality as AudioQuality)) {
                throw new BadRequestException('Audio format should only have audio quality')
            }
            if (data.format === Format.video && !Object.values(VideoQuality).includes(data.videoQuality as VideoQuality)) {
                throw new BadRequestException('Video format should only have video quality')
            }
            await mkdir("/tmp/jobs", { recursive: true })
            const job = await this.jobModel.create({ jobId: createId(), status: 'pending', ...data })
            const extension = job.format === Format.audio ? "mp3" : "mp4"
            const outputPath = `/tmp/jobs/${job.jobId}.${extension}`
            this.download(job, outputPath)
            return job
        } catch (error) {
            throw new BadRequestException(error)
        }
    }

    async findOne(jobId: string) {
        try {
            return await this.jobModel.findOne({ jobId })
        } catch (error) {
            throw new BadRequestException(error)
        }
    }

    addClient(id: string, res: Response) {
        this.clients.set(id, res)
    }

    removeClient(id: string) {
        this.clients.delete(id)
    }

    private download(job: Job, outputPath: string) {
        let args = []
        if (job.format === Format.audio) {
            args.push(
                '-x',
                '--audio-format',
                'mp3',
            )
        }
        if (job.format === Format.video) {
            args.push(
                '-f',
                'bv*+ba/b',
                '--merge-output-format',
                'mp4',
            )
        }
        const process = spawn('/usr/bin/yt-dlp', [
            ...args,
            "--newline",
            '-o',
            outputPath,
            job.url,
        ])

        process.stdout.on('data', chunk => {
            console.error(chunk.toString())
            const match = chunk.toString().match(/\[download\]\s+(\d+(?:\.\d+)?)%/)
            if (match) {
                const progress = Number(match[1])
                const client = this.clients.get(String(job.jobId))
                client?.write(`data: ${JSON.stringify({ progress })}\n\n`)
            }
        })

        process.on('error', (err) => {
            console.error(err)
        })

        process.on('close', () => {
            const client = this.clients.get(String(job.jobId))
            client?.write(`event: complete\n` + `data: done\n\n`)
            client?.end()
            console.log("ended")
        })
    }

    getInfo(url: string) {
        return new Promise((resolve, reject) => {
            const process = spawn("/usr/bin/yt-dlp", [
                "--dump-json",
                url,
            ])

            let stdout = ""
            let stderr = ""

            process.stdout.on("data", (chunk) => {
                stdout += chunk.toString()
            })

            process.stderr.on("data", (chunk) => {
                stderr += chunk.toString()
            })

            process.on("error", (err) => {
                reject(err)
            })

            process.on("close", (code) => {
                if (code !== 0) {
                    return reject(new Error(stderr))
                }
                try {
                    const info = JSON.parse(stdout)
                    resolve(info)
                } catch (err) {
                    reject(err)
                }
            })
        })
    }

    extractStreams(metadata: any) {
        // Video-only streams (mp4 or webm video)
        const videoFormats = metadata.formats.filter(fmt => {
            return (fmt.video_ext === "mp4" || fmt.video_ext === "webm") && fmt.audio_ext === "none"
        })
        // Audio-only streams (webm or m4a audio)
        const audioFormats = metadata.formats.filter(fmt => {
            return (fmt.audio_ext === "webm" || fmt.audio_ext === "m4a") && fmt.video_ext === "none"
        })
        return {
            title: metadata.title,
            thumbnail: metadata.thumbnail,
            videoFormats,
            audioFormats,
            combinedFormat: metadata.format_id ?? null, // "398+251"
        }
    }

}