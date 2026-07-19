import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose'
import { HydratedDocument, Types } from 'mongoose'
import { AudioQuality, Format, JobStatus, VideoQuality } from '../dto/enum'

export type JobDocument = HydratedDocument<Job>

@Schema({ timestamps: true })
export class Job {
    @Prop()
    jobId: String

    @Prop({ enum: JobStatus })
    status: string

    @Prop()
    url: string

    @Prop({ enum: AudioQuality })
    audioQuality: string

    @Prop({ enum: VideoQuality })
    videoQuality: string

    @Prop({ enum: Format })
    format: string
}

export const JobSchema = SchemaFactory.createForClass(Job)