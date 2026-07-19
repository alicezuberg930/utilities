import { BadRequestException, Injectable } from '@nestjs/common'
import { UsersService } from '../users/users.service'
import { comparePassword } from 'src/common/utils'
import { JwtService } from '@nestjs/jwt'
import { RegisterDto } from './dto/create-auth.dto'
import mongoose from 'mongoose'
import { User } from '../users/schemas/user.schema'
import { VerifyDto } from './dto/verify-auth.dto'

@Injectable()
export class AuthService {
  constructor(private usersService: UsersService, private jwtService: JwtService) { }

  async validateUser(identifier: string, pass: string): Promise<mongoose.Document<unknown, {}, User> & User> {
    const user = await this.usersService.findUserByIdentifier(identifier)
    if (!user) return null
    const checkPassword = await comparePassword(pass, user.password)
    if (!checkPassword) return null
    return user
  }

  async login(user: mongoose.Document<unknown, {}, User> & User) {
    const payload = { _id: user._id, email: user.email }
    return {
      access_token: this.jwtService.sign(payload),
      user: {
        _id: user._id,
        email: user.email,
        name: user.name,
        phone: user.phone ?? null,
        address: user.address ?? null,
        avatar: user.avatar ?? null
      }
    }
  }

  async register(registerDto: RegisterDto) {
    return await this.usersService.register(registerDto)
  }

  async verify(data: VerifyDto) {
    return await this.usersService.verify(data)
  }
}
