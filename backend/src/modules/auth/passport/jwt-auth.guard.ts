
import { ExecutionContext, Injectable, UnauthorizedException } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { AuthGuard } from '@nestjs/passport';
import { AUTH_REQUIRED_KEY, IS_PUBLIC_KEY } from 'src/common/decorators/public.decorator';

@Injectable()
export class JwtAuthGuard extends AuthGuard('jwt') {
    private readonly protectedMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])

    constructor(private reflector: Reflector) { super() }

    canActivate(context: ExecutionContext) {
        const isPublic = this.reflector.getAllAndOverride<boolean>(IS_PUBLIC_KEY, [
            context.getHandler(),
            context.getClass(),
        ])
        if (isPublic) return true

        const isAuthRequired = this.reflector.getAllAndOverride<boolean>(AUTH_REQUIRED_KEY, [
            context.getHandler(),
            context.getClass(),
        ])
        if (isAuthRequired) return super.canActivate(context)

        const request = context.switchToHttp().getRequest<{ method?: string }>()
        const method = request.method?.toUpperCase()
        if (!method || !this.protectedMethods.has(method)) return true

        // Add your custom authentication logic here
        // for example, call super.logIn(request) to establish a session.
        return super.canActivate(context);
    }

    handleRequest(err: any, user: any, info: any) {
        // You can throw an exception based on either "info" or "err" arguments
        if (err || !user) throw err || new UnauthorizedException("Access token không hợp lệ");
        return user
    }
}
