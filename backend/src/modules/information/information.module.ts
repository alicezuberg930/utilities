import { Module } from '@nestjs/common';

import { MongooseModule } from '@nestjs/mongoose';
import { Information, InformationSchema } from './schemas/information.schemas';
import { InformationController } from './information.controller';
import { InformationService } from './information.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: Information.name, schema: InformationSchema }])],
  controllers: [InformationController],
  providers: [InformationService],
  exports: [InformationService]
})
export class InformationModule { }
