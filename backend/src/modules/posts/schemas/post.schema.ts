import { Prop, Schema, SchemaFactory } from "@nestjs/mongoose"
import { HydratedDocument, Schema as MongooseSchema } from "mongoose"
import { category } from "../dto/enum";

export type PostDocument = HydratedDocument<Post>

@Schema({ timestamps: true })
export class Post {
    @Prop()
    title: string;

    @Prop()
    description: string

    @Prop()
    date: string

    @Prop({ enum: category })
    category: string

    @Prop()
    cover: string

    @Prop()
    images: string[]
}

export const PostSchema = SchemaFactory.createForClass(Post)