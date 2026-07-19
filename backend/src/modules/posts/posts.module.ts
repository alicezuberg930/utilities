import { Module } from '@nestjs/common'
import { PostsService } from './posts.service'
import { PostsController } from './posts.controller'
import { MongooseModule } from '@nestjs/mongoose'
import { Post, PostSchema } from './schemas/post.schema'
import { FileService } from '../file/file.service'

@Module({
  imports: [
    MongooseModule.forFeature([{ name: Post.name, schema: PostSchema }])
  ],
  controllers: [PostsController],
  providers: [PostsService, FileService],
})
export class PostsModule { }
