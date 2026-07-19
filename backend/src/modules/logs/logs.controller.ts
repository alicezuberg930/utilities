import { Body, Controller, Delete, Get, Param, Patch, Post } from '@nestjs/common'
import { ResponseMessage } from 'src/common/decorators/public.decorator'
import { LogsService } from './logs.service'
import { Log } from './schemas/log.schema'

@Controller('logs')
export class LogsController {
  constructor(private readonly logsService: LogsService) { }

  @ResponseMessage('Create log successfully')
  @Post()
  create(@Body() logData: Partial<Log>) {
    return this.logsService.create(logData)
  }

  @ResponseMessage('Get logs successfully')
  @Get()
  findAll() {
    return this.logsService.findAll()
  }

  @ResponseMessage('Get log successfully')
  @Get(':id')
  findOne(@Param('id') id: string) {
    return this.logsService.findOne(id)
  }

  @ResponseMessage('Update log successfully')
  @Patch(':id')
  update(@Param('id') id: string, @Body() logData: Partial<Log>) {
    return this.logsService.update(id, logData)
  }

  @ResponseMessage('Delete logs successfully')
  @Delete('many')
  removeMany(@Body() filter: Partial<Pick<Log, 'ipAddress' | 'userAgent' | 'path' | 'method' | 'referrer'>>) {
    return this.logsService.removeMany(filter)
  }

  @ResponseMessage('Delete log successfully')
  @Delete(':id')
  remove(@Param('id') id: string) {
    return this.logsService.remove(id)
  }
}
