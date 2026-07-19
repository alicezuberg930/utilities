import { IsEmail, IsNotEmpty, IsOptional, Length, Max } from 'class-validator';

export class CreateUserDto {
    @IsNotEmpty({ message: "Tên người dùng không được để trống" })
    name: string;

    @IsEmail({}, { message: "Email sai định dạng" })
    email: string;

    @IsNotEmpty()
    password: string;

    @Length(10, 10)
    phone: string

    @IsOptional()
    address: string

    @IsOptional()
    avatar: string
}
