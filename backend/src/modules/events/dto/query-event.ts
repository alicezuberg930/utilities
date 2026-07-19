import { IsOptional } from "class-validator"

export class QueryEvent {
    @IsOptional()
    isActive: boolean

    @IsOptional()
    page: number
}
