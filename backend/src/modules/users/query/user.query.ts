import { IsOptional } from "class-validator"

export class UserQuery {
    @IsOptional()
    page: string

    @IsOptional()
    pageSize: string

    @IsOptional()
    name: string

    // @IsOptional()
    // categoryId: string

    // @IsOptional()
    // brandId: string
}