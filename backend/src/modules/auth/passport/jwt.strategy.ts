
import { ExtractJwt, Strategy } from 'passport-jwt';
import { PassportStrategy } from '@nestjs/passport';
import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import type { Request } from 'express';

const ACCESS_TOKEN_COOKIE = 'accessToken'

const cookieExtractor = (request: Request): string | null => {
    const cookieHeader = request?.headers?.cookie
    if (!cookieHeader) return null

    const accessTokenCookie = cookieHeader
        .split(';')
        .map((cookie) => cookie.trim())
        .find((cookie) => cookie.startsWith(`${ACCESS_TOKEN_COOKIE}=`))

    if (!accessTokenCookie) return null

    const token = accessTokenCookie.slice(ACCESS_TOKEN_COOKIE.length + 1)
    try {
        return decodeURIComponent(token)
    } catch {
        return token
    }
}

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy) {

    constructor(private configService: ConfigService) {
        super({
            jwtFromRequest: ExtractJwt.fromExtractors([
                cookieExtractor,
                ExtractJwt.fromAuthHeaderAsBearerToken(),
            ]),
            ignoreExpiration: false,
            secretOrKey: configService.get<string>("JWT_SECRET_KEY"),
        });
    }

    async validate(payload: any) {
        return payload;
    }
}
