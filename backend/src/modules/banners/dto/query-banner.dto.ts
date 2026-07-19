import { IsOptional } from "class-validator";

export class QueryBanner {
    @IsOptional()
    isActive: string

    @IsOptional()
    page: number
}
