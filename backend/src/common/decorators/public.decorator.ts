import { SetMetadata } from '@nestjs/common'

export const IS_PUBLIC_KEY = 'isPublic'
export const Public = () => SetMetadata(IS_PUBLIC_KEY, true)
export const AUTH_REQUIRED_KEY = 'authRequired'
export const AuthRequired = () => SetMetadata(AUTH_REQUIRED_KEY, true)
export const RESPONSE_MESSAGE = 'response_message'
export const ResponseMessage = (message: string) => SetMetadata(RESPONSE_MESSAGE, message)
