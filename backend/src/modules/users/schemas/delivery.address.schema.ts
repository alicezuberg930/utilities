import { Prop, Schema } from "@nestjs/mongoose"

@Schema({ timestamps: false })
export class DeliveryAddress {    
    @Prop()
    contactName: string

    @Prop()
    contactPhone: string

    @Prop()
    city: string

    @Prop()
    district: string

    @Prop()
    ward: string

    @Prop()
    street: string

    @Prop({ default: false })
    isDefault: boolean
}
