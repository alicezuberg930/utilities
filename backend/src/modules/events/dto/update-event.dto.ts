import { PartialType } from '@nestjs/mapped-types'
import { EventData } from './create-event.dto'

export class UpdateEventData extends PartialType(EventData) { }
