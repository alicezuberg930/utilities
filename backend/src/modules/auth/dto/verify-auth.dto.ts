import { IsNotEmpty } from "class-validator"

export class VerifyDto {
    @IsNotEmpty()
    id: string

    @IsNotEmpty()
    code: string
}
