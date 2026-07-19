import { Injectable, NestMiddleware } from '@nestjs/common'
import { NextFunction, Request, Response } from 'express'
import { LogsService } from '../../modules/logs/logs.service'

@Injectable()
export class LogsMiddleware implements NestMiddleware {
  constructor(private readonly logsService: LogsService) { }

  use(req: Request, res: Response, next: NextFunction) {
    const ipAddress = (req.headers['x-forwarded-for'] as string)?.split(',')[0]?.trim() || req.ip || req.socket.remoteAddress || ''

    void this.logsService.create({
      ipAddress,
      userAgent: req.get('user-agent') || '',
      path: req.originalUrl || req.url,
      method: req.method,
      referrer: req.get('referer') || req.get('referrer') || '',
    }).catch((error) => {
      console.error('Failed to save request log', error)
    })

    next()
  }
}
