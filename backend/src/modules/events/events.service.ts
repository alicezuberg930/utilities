import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common'
import { InjectModel } from '@nestjs/mongoose'
import { Model } from 'mongoose'
import { FileService } from '../file/file.service'
import { Event, EventDocument } from './shemas/event.schema'
import { EventData } from './dto/create-event.dto'
import { UpdateEventData } from './dto/update-event.dto'
import { QueryEvent } from './dto/query-event'

@Injectable()
export class EventsService {
  constructor(@InjectModel(Event.name) private eventsModel: Model<EventDocument>, private fileService: FileService) { }

  async create(eventData: EventData) {
    try {
      if (eventData.isActive) {
        await this.eventsModel.updateOne({ isActive: true }, { $set: { isActive: false } })
      }
      return await this.eventsModel.create({ ...eventData })
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async findAll(query: QueryEvent) {
    try {
      const { page, isActive } = query
      const filter: Record<string, any> = {}
      if (isActive) filter.isActive = isActive
      const events = await this.eventsModel.find(filter).sort({ createdAt: -1 })
      return events
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  findOne(id: number) {

  }

  async update(id: string, eventData: UpdateEventData) {
    try {
      const event = await this.eventsModel.findOneAndUpdate({ _id: id }, { ...eventData }, { new: true })
      if (!event) throw new NotFoundException('Không tìm thấy sự kiện')
      if (eventData.isActive) {
        await this.eventsModel.updateOne({ isActive: true }, { $set: { isActive: false } })
      }
      return event
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async remove(id: string) {
    try {
      const event = await this.eventsModel.findOneAndDelete({ _id: id })
      if (!event) throw new NotFoundException('Không tìm thấy sự kiện')
      await this.fileService.delete([event.image])
      if (event.isActive) {
        await this.eventsModel.updateOne({ isActive: false }, { $set: { isActive: true } })
      }
      return event
    } catch (error) {
      throw new BadRequestException(error)
    }
  }
}
