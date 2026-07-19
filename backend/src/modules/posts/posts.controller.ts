import { Controller, Get, Post, Body, Patch, Param, Delete, Query } from '@nestjs/common';
import { PostsService } from './posts.service';
import { ResponseMessage } from 'src/common/decorators/public.decorator';
import { PostData } from './dto/create-post.dto';
import { UpdatePostData } from './dto/update-post.dto';
import { QueryPost } from './dto/query-post';

@Controller('posts')
export class PostsController {
  constructor(private readonly postsService: PostsService) { }

  @ResponseMessage('Tạo bài đăng thành công')
  @Post()
  create(@Body() postData: PostData) {
    return this.postsService.create(postData);
  }

  @ResponseMessage('Lấy danh sách bài đăng thành công')
  @Get()
  findAll(@Query() query: QueryPost) {
    return this.postsService.findAll(query);
  }

  @ResponseMessage('Lấy bài đăng thành công')
  @Get(':id')
  findOne(@Param('id') id: string) {
    return this.postsService.findOne(id);
  }

  @ResponseMessage('Cập nhật bài đăng thành công')
  @Patch(':id')
  update(@Param('id') id: string, @Body() postData: UpdatePostData) {
    return this.postsService.update(id, postData);
  }

  @ResponseMessage('Xóa bài đăng thành công')
  @Delete(':id')
  remove(@Param('id') id: string) {
    return this.postsService.remove(id);
  }
}
