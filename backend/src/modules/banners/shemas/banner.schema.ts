import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose'
import { HydratedDocument, Types } from 'mongoose'

export type BannerDocument = HydratedDocument<Banner>

@Schema({ timestamps: true })
export class Banner {
    @Prop()
    order: number

    @Prop()
    image: string

    @Prop()
    name: string

    @Prop({ default: true })
    isActive: boolean
}

export const BannerSchema = SchemaFactory.createForClass(Banner)