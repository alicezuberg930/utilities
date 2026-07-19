import { MiddlewareConsumer, Module, NestModule } from '@nestjs/common'
import { MongooseModule } from '@nestjs/mongoose'
import { Log, LogSchema } from './schemas/log.schema'
import { LogsController } from './logs.controller'
import { LogsService } from './logs.service'
import { LogsMiddleware } from '../../common/middleware/logs.middleware'

@Module({
  imports: [MongooseModule.forFeature([{ name: Log.name, schema: LogSchema }])],
  controllers: [LogsController],
  providers: [LogsService, LogsMiddleware],
})
export class LogsModule implements NestModule {
  configure(consumer: MiddlewareConsumer) {
    consumer.apply(LogsMiddleware).forRoutes('*')
  }
}
