import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common'
import { InjectModel } from '@nestjs/mongoose'
import { Post, PostDocument } from './schemas/post.schema'
import { Model, Types } from 'mongoose'
import { PostData } from './dto/create-post.dto'
import { UpdatePostData } from './dto/update-post.dto'
import { QueryPost } from './dto/query-post'
import { FileService } from '../file/file.service'

@Injectable()
export class PostsService {
  constructor(@InjectModel(Post.name) private postModel: Model<PostDocument>, private fileService: FileService) { }

  async create(postData: PostData) {
    try {
      const post = await this.postModel.create({ ...postData })
      return post
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async findAll(query: QueryPost) {
    try {
      const { page, category } = query
      const filter: Record<string, any> = {}
      if (category) filter.category = category
      let posts = await this.postModel.find(filter).sort({ createdAt: -1 })
      return posts
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async findOne(id: string) {
    try {
      let post = await this.postModel.findById(id)
      if (!post) throw new NotFoundException('Không tìm thấy bài đăng')
      return post
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async update(id: string, postData: UpdatePostData) {
    try {
      const post = await this.postModel.findOne({ _id: id })
      await this.fileService.delete([...post.images, post.cover])
      await post.updateOne({ ...postData }, { new: true })
      if (!post) throw new NotFoundException('Không tìm thấy bài đăng')
      return post
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async remove(id: string) {
    try {
      const post = await this.postModel.findOneAndDelete({ _id: id })
      if (!post) throw new NotFoundException('Không tìm thấy bài đăng')
      await this.fileService.delete([...post.images, post.cover])
      return post
    } catch (error) {
      throw new BadRequestException(error)
    }
  }
}