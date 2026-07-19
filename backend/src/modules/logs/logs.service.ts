import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common'
import { InjectModel } from '@nestjs/mongoose'
import { Model } from 'mongoose'
import { Log, LogDocument } from './schemas/log.schema'

@Injectable()
export class LogsService {
  constructor(@InjectModel(Log.name) private logModel: Model<LogDocument>) { }

  async create(logData: Partial<Log>) {
    try {
      return await this.logModel.create({ ...logData })
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async findAll() {
    try {
      return await this.logModel.find().sort({ createdAt: -1 })
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async findOne(id: string) {
    try {
      const log = await this.logModel.findById(id)
      if (!log) throw new NotFoundException('Log not found')
      return log
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async update(id: string, logData: Partial<Log>) {
    try {
      const log = await this.logModel.findOneAndUpdate({ _id: id }, { ...logData }, { new: true })
      if (!log) throw new NotFoundException('Log not found')
      return log
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async remove(id: string) {
    try {
      const log = await this.logModel.findOneAndDelete({ _id: id })
      if (!log) throw new NotFoundException('Log not found')
      return log
    } catch (error) {
      throw new BadRequestException(error)
    }
  }

  async removeMany(filter: Partial<Pick<Log, 'ipAddress' | 'userAgent' | 'path' | 'method' | 'referrer'>>) {
    try {
      const entries = Object.entries(filter ?? {})
      if (entries.length !== 1) {
        throw new BadRequestException('Provide exactly one of: ipAddress, userAgent, path, method, referrer')
      }
      const [field, value] = entries[0]
      const allowedFields = ['ipAddress', 'userAgent', 'path', 'method', 'referrer'] as const
      if (!allowedFields.includes(field as (typeof allowedFields)[number])) {
        throw new BadRequestException('Only one of ipAddress, userAgent, path, method, referrer is allowed')
      }
      const result = await this.logModel.deleteMany({ [field]: value })
      return { deletedCount: result.deletedCount ?? 0 }
    } catch (error) {
      throw new BadRequestException(error)
    }
  }
}
