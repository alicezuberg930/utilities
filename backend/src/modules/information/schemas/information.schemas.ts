import { Prop, SchemaFactory, Schema } from "@nestjs/mongoose";
import { HydratedDocument } from "mongoose";

export type InformationDocument = HydratedDocument<Information>

@Schema({ timestamps: true })
export class Information {
    @Prop()
    activityAddress: string

    @Prop()
    storageAddress: string

    @Prop()
    email: string

    @Prop()
    hotline: string

    @Prop()
    facebookUrl: string

    @Prop()
    zaloURL: string

    @Prop()
    youtubeURL: string

    @Prop()
    googleMapURL: string

    @Prop()
    websiteURL: string

    @Prop()
    telephone: string

    @Prop()
    achaubankNumber: string

    @Prop()
    viettinbankNumber: string

    @Prop()
    sacombankNumber: string

    @Prop()
    vietcombankNumber: string

    @Prop()
    agribankNumber: string

    @Prop()
    vpbankNumber: string
}

export const InformationSchema = SchemaFactory.createForClass(Information)