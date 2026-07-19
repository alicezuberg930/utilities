
import { Strategy } from 'passport-local';
import { PassportStrategy } from '@nestjs/passport';
import { BadRequestException, Injectable, UnauthorizedException } from '@nestjs/common';
import { AuthService } from '../auth.service';

@Injectable()
export class LocalStrategy extends PassportStrategy(Strategy) {
    constructor(private authService: AuthService) { super() }

    async validate(username: string, password: string) {
        const user = await this.authService.validateUser(username, password)
        if (!user) throw new UnauthorizedException('Sai mật khẩu hoặc tên người dùng')
        if (!user.isEmailVerified) throw new BadRequestException("Tài khoản chưa được kích hoạt")
        return user
    }
}
