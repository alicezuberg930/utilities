import { type CustomFile } from './types'

export type UploadImage = CustomFile | string

export const createUploadImage = (file: File): CustomFile => {
  const preview = URL.createObjectURL(file)

  // Try to attach properties to the original File; if it's not allowed, create a new File instance.
  try {
    // Some environments disallow adding certain properties to File instances ("path" may be read-only).
    ;(file as any).preview = preview
    ;(file as any).path = file.name
    return file as CustomFile
  } catch (e) {
    const newFile = new File([file], file.name, { type: file.type, lastModified: file.lastModified })
    ;(newFile as any).preview = preview
    ;(newFile as any).path = file.name
    return newFile as CustomFile
  }
}

export const isLocalUploadImage = (
  file: UploadImage | null | undefined
): file is CustomFile => !!file && typeof file !== 'string'
