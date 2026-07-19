import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose'
import { HydratedDocument } from 'mongoose'

export type LogDocument = HydratedDocument<Log>

@Schema({ timestamps: true })
export class Log {
    @Prop()
    ipAddress: string

    @Prop()
    userAgent: string

    @Prop()
    path: string

    @Prop()
    method: string

    @Prop()
    referrer: string
}

export const LogSchema = SchemaFactory.createForClass(Log)
