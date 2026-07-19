import { IsEnum, IsNotEmpty, IsOptional } from "class-validator"
import { category } from "./enum"

export class PostData {
    @IsNotEmpty({ message: "Tên bài đăng không được để trống" })
    title: string

    @IsOptional()
    description: string

    @IsNotEmpty({ message: "Ngày thực hiện không được để trống" })
    date: string

    @IsEnum(category, { message: 'Loại danh mục không hợp lệ' })
    category: string

    @IsNotEmpty({ message: "Ảnh bài đăng không được để trống" })
    cover: string

    @IsNotEmpty({ message: "Ảnh chi tiết không được để trống" })
    images: string[]
}
