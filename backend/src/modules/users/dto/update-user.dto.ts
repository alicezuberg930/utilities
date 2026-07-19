import { PartialType } from '@nestjs/mapped-types';
import { CreateUserDto } from './create-user.dto';
import { IsEmail, IsEnum, IsNotEmpty, IsOptional, IsPhoneNumber, Length, Matches, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

export class UpdateUserDto extends PartialType(CreateUserDto) {
    @IsOptional()
    balance: number
}