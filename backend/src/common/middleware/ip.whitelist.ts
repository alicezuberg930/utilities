import { Injectable, NestMiddleware, UnauthorizedException } from '@nestjs/common';
import { Request, Response, NextFunction } from 'express';

@Injectable()
export class IpWhitelistMiddleware implements NestMiddleware {
    private readonly allowedIps = ['125.235.239.192', '127.0.0.1'];

    use(req: Request, res: Response, next: NextFunction) {
        const clientIp = (req.headers['x-forwarded-for'] as string)?.split(',')[0]?.trim() || req.ip || req.socket.remoteAddress;
        console.log("Request IP: " + (req.headers['x-forwarded-for'] as string)?.split(',')[0]?.trim())
        // console.log("Request socket IP: " + req.socket.remoteAddress)
        // if (!this.allowedIps.includes(clientIp)) {
        //     throw new UnauthorizedException('IP not allowed');
        // }
        next();
    }
}