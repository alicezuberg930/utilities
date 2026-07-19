import { Controller, Get, Post, Body, Patch, Param, Delete, Query } from '@nestjs/common'
import { ResponseMessage } from 'src/common/decorators/public.decorator'
import { EventData } from './dto/create-event.dto'
import { UpdateEventData } from './dto/update-event.dto'
import { EventsService } from './events.service'
import { QueryEvent } from './dto/query-event'

@Controller('events')
export class EventsController {
  constructor(private readonly eventsService: EventsService) { }

  @ResponseMessage('Tạo sự kiện thành công')
  @Post()
  create(@Body() eventData: EventData) {
    return this.eventsService.create(eventData)
  }

  @ResponseMessage('Lấy thông tin sự kiện thành công')
  @Get()
  findAll(@Query() query: QueryEvent) {
    return this.eventsService.findAll(query)
  }

  @Get(':id')
  findOne(@Param('id') id: string) {
    return this.eventsService.findOne(+id)
  }

  @ResponseMessage('Cập nhật sự kiện thành công')
  @Patch(':id')
  update(@Param('id') id: string, @Body() eventData: UpdateEventData) {
    return this.eventsService.update(id, eventData)
  }

  @ResponseMessage('Xóa sự kiện thành công')
  @Delete(':id')
  remove(@Param('id') id: string) {
    return this.eventsService.remove(id)
  }
}
