import { Module } from '@nestjs/common'
import { AuthService } from './auth.service'
import { AuthController } from './auth.controller'
import { UsersModule } from '../users/users.module'
import { JwtModule } from '@nestjs/jwt'
import { ConfigService } from '@nestjs/config'
import { PassportModule } from '@nestjs/passport'
import { LocalStrategy } from './passport/local.strategy'
import { JwtStrategy } from './passport/jwt.strategy'

@Module({
  imports: [
    UsersModule,
    JwtModule.registerAsync({
      useFactory: async (configService: ConfigService) => ({
        global: true,
        secret: configService.get<string>("JWT_SECRET_KEY"),
        signOptions: { expiresIn: configService.get<string>("JWT_EXPIRED_IN"), }
      }),
      inject: [ConfigService]
    }),
    PassportModule
  ],
  controllers: [AuthController],
  providers: [AuthService, LocalStrategy, JwtStrategy],
})
export class AuthModule { }