import { Module } from '@nestjs/common'
import { MongooseModule } from '@nestjs/mongoose'
import { FileService } from '../file/file.service'
import { Event, EventSchema } from './shemas/event.schema'
import { EventsController } from './events.controller'
import { EventsService } from './events.service'

@Module({
  imports: [MongooseModule.forFeature([{ name: Event.name, schema: EventSchema }])],
  controllers: [EventsController],
  providers: [EventsService, FileService],
})
export class EventsModule { }
