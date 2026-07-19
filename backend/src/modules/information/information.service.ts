import { BadRequestException, Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import mongoose, { Model } from 'mongoose';
import { InformationData } from './dto/information.dto';
import { Information, InformationDocument } from './schemas/information.schemas';

@Injectable()
export class InformationService {
  constructor(@InjectModel(Information.name) private informationModel: Model<InformationDocument>) { }

  async update(informationData: InformationData) {
    let information: mongoose.Document<unknown, {}, Information>
    try {
      if (await this.informationModel.countDocuments() > 0) {
        information = await this.informationModel.findOneAndUpdate({}, { ...informationData }, { new: true })
      } else {
        information = await this.informationModel.create({ ...informationData })
      }
      return information
    } catch (error) {
      throw new BadRequestException("Cập nhật thông tin thất bại")
    }
  }

  async findAll() {
    return await this.informationModel.findOne()
  }
}
