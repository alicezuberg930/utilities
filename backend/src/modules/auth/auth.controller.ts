import { Body, Controller, Post, Request, Res, UseGuards } from '@nestjs/common';
import type { CookieOptions, Response } from 'express';
import { AuthService } from './auth.service';
import { RegisterDto } from './dto/create-auth.dto';
import { LocalAuthGuard } from './passport/local-auth.guard';
import { Public, ResponseMessage } from 'src/common/decorators/public.decorator';
import { VerifyDto } from './dto/verify-auth.dto';
import { ConfigService } from '@nestjs/config';

const ACCESS_TOKEN_COOKIE = 'accessToken'

@Controller('auth')
export class AuthController {
  constructor(
    private readonly authService: AuthService,
    private readonly configService: ConfigService,
  ) { }

  @UseGuards(LocalAuthGuard)
  @Public()
  @Post("login")
  @ResponseMessage("Đăng nhập thành công")
  async login(@Request() request: any, @Res({ passthrough: true }) response: Response) {
    const loginResult = await this.authService.login(request.user)
    this.setAccessTokenCookie(response, loginResult.access_token)
    return loginResult
  }

  @Public()
  @Post("register")
  register(@Body() registerDto: RegisterDto) {
    return this.authService.register(registerDto)
  }

  @Public()
  @Post("verify")
  @ResponseMessage("Xác thực mã thành công")
  verify(@Body() data: VerifyDto) {
    return this.authService.verify(data)
  }

  private setAccessTokenCookie(response: Response, accessToken: string) {
    const isProduction = process.env.NODE_ENV === 'production'
    const sameSite: CookieOptions['sameSite'] = isProduction ? 'none' : 'lax'

    response.cookie(ACCESS_TOKEN_COOKIE, accessToken, {
      httpOnly: true,
      secure: isProduction,
      sameSite,
      path: '/',
      maxAge: this.getAccessTokenCookieMaxAge(),
    })
  }

  private getAccessTokenCookieMaxAge() {
    const expiresIn = this.configService.get<string>('JWT_EXPIRED_IN')
    if (!expiresIn) return undefined

    const match = /^(\d+)([smhd])?$/i.exec(expiresIn.trim())
    if (!match) return undefined

    const value = Number(match[1])
    const unit = match[2]?.toLowerCase() ?? 's'
    const multipliers: Record<string, number> = {
      s: 1000,
      m: 60 * 1000,
      h: 60 * 60 * 1000,
      d: 24 * 60 * 60 * 1000,
    }

    return value * multipliers[unit]
  }
}
