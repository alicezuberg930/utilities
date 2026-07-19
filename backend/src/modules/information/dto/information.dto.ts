import { IsEmail, IsNotEmpty, IsOptional, Length, Matches } from "class-validator"

export class InformationData {
    @IsNotEmpty({ message: "Địa chỉ nấu cháo không được để trống" })
    activityAddress: string

    @IsNotEmpty({ message: "Địa chỉ kho không được để trống" })
    storageAddress: string

    @IsEmail({}, { message: "Email sai định dạng" })
    email: string

    @IsNotEmpty({ message: "Số điện thoại không được để trống" })
    hotline: string

    @IsNotEmpty({ message: "Link facebook không được để trống" })
    facebookUrl: string

    @IsNotEmpty({ message: "Link zalo không được để trống" })
    zaloURL: string

    @IsNotEmpty({ message: "Link youtube không được để trống" })
    youtubeURL: string

    @IsNotEmpty({ message: "Link google map không được để trống" })
    googleMapURL: string

    @IsNotEmpty({ message: "Link website không được để trống" })
    websiteURL: string

    @IsOptional()
    telephone: string

    @IsOptional()
    achaubankNumber: string

    @IsOptional()
    viettinbankNumber: string

    @IsOptional()
    sacombankNumber: string

    @IsOptional()
    vietcombankNumber: string

    @IsOptional()
    agribankNumber: string

    @IsOptional()
    vpbankNumber: string
}