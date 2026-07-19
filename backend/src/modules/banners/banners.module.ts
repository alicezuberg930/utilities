import { Module } from '@nestjs/common';
import { BannersService } from './banners.service';
import { BannersController } from './banners.controller';
import { MongooseModule } from '@nestjs/mongoose';
import { Banner, BannerSchema } from './shemas/banner.schema';
import { FileService } from '../file/file.service';

@Module({
  imports: [MongooseModule.forFeature([{ name: Banner.name, schema: BannerSchema }])],
  controllers: [BannersController],
  providers: [BannersService, FileService],
})
export class BannersModule { }
