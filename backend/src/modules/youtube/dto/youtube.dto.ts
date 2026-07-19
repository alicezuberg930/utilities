import { IsEnum, IsNotEmpty, IsOptional } from 'class-validator';
import { Format, VideoQuality, AudioQuality } from './enum';

export class YoutubeDto {
    @IsNotEmpty({ message: "Youtube URL cannot be empty" })
    url: string;

    @IsOptional()
    @IsEnum(AudioQuality, { message: 'Audio quality is invalid' })
    audioQuality: string

    @IsOptional()
    @IsEnum(VideoQuality, { message: 'Video quality is invalid' })
    videoQuality: string

    @IsEnum(Format, { message: 'Format is invalid' })
    format: string
}
