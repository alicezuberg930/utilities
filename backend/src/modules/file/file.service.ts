import { BadRequestException, Injectable } from '@nestjs/common'
import { v2 as cloudinary } from 'cloudinary'
import { UploadApiResponse } from 'cloudinary'
import * as fs from 'fs'

@Injectable()
export class FileService {
  async upload(files: Array<Express.Multer.File>) {
    try {
      let fileUrls = []
      cloudinary.config({
        cloud_name: 'dopxhmw1q',
        api_key: process.env.CLOUDINARY_API_KEY,
        api_secret: process.env.CLOUDINARY_API_SECRET
      })
      for (let i = 0; i < files.length; i++) {
        let response: UploadApiResponse = await cloudinary.uploader.upload(files[i].path, {
          folder: 'webtinhthuong', resource_type: 'image',
        })
        fileUrls.push(response.secure_url)
        fs.unlink(files[i].path, (err) => {
          if (err) console.error(`Error deleting file ${files[i].path}: ${err.message}`)
          else console.log(`File deleted: ${files[i].path}`)
        })
      }
      return fileUrls
    } catch (error) {
      throw new BadRequestException(error)
    }

    // console.log(uploadResult)
    // Optimize delivery by resizing and applying auto-format and auto-quality
    // const optimizeUrl = cloudinary.url('shoes', {
    //   fetch_format: 'auto',
    //   quality: 'auto'
    // })
    // console.log(optimizeUrl)
    // Transform the image: auto-crop to square aspect_ratio
    // const autoCropUrl = cloudinary.url('shoes', {
    //   crop: 'auto',
    //   gravity: 'auto',
    //   width: 500,
    //   height: 500,
    // })
    // console.log(autoCropUrl)
  }

  async delete(fileUrls: string[]) {
    try {
      cloudinary.config({
        cloud_name: 'dopxhmw1q',
        api_key: process.env.CLOUDINARY_API_KEY,
        api_secret: process.env.CLOUDINARY_API_SECRET
      })
      // Extract the public ID from the URL
      for (let i = 0; i < fileUrls.length; i++) {
        const publicId = fileUrls[i].split('/').slice(-2).join('/').replace(/\.[^/.]+$/, '')
        const response = await cloudinary.uploader.destroy(publicId)
        if (response.result !== 'ok') {
          throw new BadRequestException('Xóa file thất bại')
        }
      }
    } catch (error) {
      throw new BadRequestException(error)
    }
  }
}