import { IsNotEmpty, IsOptional } from "class-validator";

export class EventData {
    @IsOptional()
    isActive: boolean

    @IsNotEmpty({ message: "Ảnh sự kiện không được để trống" })
    image: string
}
