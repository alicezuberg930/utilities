import { IsOptional } from "class-validator"

export class QueryPost {
    @IsOptional()
    category: string

    @IsOptional()
    page: number
}
