import { ArgumentsHost, Catch, ExceptionFilter, HttpException, HttpStatus, } from '@nestjs/common'

@Catch()
export class AllExceptionsFilter implements ExceptionFilter {
    catch(exception: any, host: ArgumentsHost) {
        const ctx = host.switchToHttp()
        const response = ctx.getResponse()
        let status = HttpStatus.BAD_REQUEST
        let message = 'Bad Request'

        if (exception instanceof HttpException) {
            status = exception.getStatus()
            const exceptionResponse = exception.getResponse()
            message = typeof exceptionResponse === 'string' ? exceptionResponse : (exceptionResponse as any).message || message
        }

        response.status(status).json({
            statusCode: status,
            message: message,
        })
    }
}