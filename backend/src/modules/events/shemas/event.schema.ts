import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose'
import { HydratedDocument, Types } from 'mongoose'

export type EventDocument = HydratedDocument<Event>

@Schema({ timestamps: true })
export class Event {
    @Prop({ default: false })
    isActive: boolean

    @Prop()
    image: string
}

export const EventSchema = SchemaFactory.createForClass(Event)