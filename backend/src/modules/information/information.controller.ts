import { Controller, Get, Post, Body } from '@nestjs/common'
import { InformationData } from './dto/information.dto'
import { ResponseMessage } from 'src/common/decorators/public.decorator'
import { InformationService } from './information.service'

@Controller('information')
export class InformationController {
  constructor(private readonly informationService: InformationService) { }

  @Post()
  @ResponseMessage('Cập nhật thông tin thành công')
  update(@Body() informationData: InformationData) {
    return this.informationService.update(informationData)
  }

  @ResponseMessage('Lấy thông tin thành công')
  @Get()
  findAll() {
    return this.informationService.findAll()
  }
}
