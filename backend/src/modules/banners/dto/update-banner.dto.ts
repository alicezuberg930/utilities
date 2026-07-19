import { PartialType } from '@nestjs/mapped-types'
import { BannerData } from './create-banner.dto'

export class UpdateBannerData extends PartialType(BannerData) { }
