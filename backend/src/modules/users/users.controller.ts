import { Controller, Get, Post, Body, Patch, Param, Delete, Query } from '@nestjs/common'
import { UsersService } from './users.service'
import { CreateUserDto } from './dto/create-user.dto'
import { UpdateUserDto } from './dto/update-user.dto'
import { AuthRequired, ResponseMessage } from 'src/common/decorators/public.decorator'
import { UserQuery } from './query/user.query'
import { CurrentUser } from 'src/common/decorators/id.decorator'

@Controller('users')
export class UsersController {
  constructor(private readonly usersService: UsersService) { }

  @ResponseMessage('Đăng ký thành công')
  @Post()
  create(@Body() userData: CreateUserDto) {
    return this.usersService.create(userData)
  }

  @ResponseMessage('Lấy dữ liệu thành công')
  @Get()
  findAll(@Query() query: UserQuery) {
    return this.usersService.findAll(query)
  }

  @Get('profile')
  @AuthRequired()
  findOne(@CurrentUser('_id') userId: string) {
    return this.usersService.findOne(userId)
  }

  @Patch(':id')
  update(@Param('id') id: string, @Body() userData: UpdateUserDto) {
    return this.usersService.update(id, userData)
  }

  @Delete(':id')
  delete(@Param('id') id: string) {
    return this.usersService.delete(id)
  }
}
