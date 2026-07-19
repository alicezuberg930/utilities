import { postCategoryTitles, type PostCategory } from "@/@types"
import type { UploadImage } from "@/components/upload"
import z from "zod"

const postFormSchema = z.object({
    title: z.string().trim().min(1, 'Vui lòng nhập tiêu đề.'),
    description: z.string().trim().min(1, 'Vui lòng nhập nội dung.'),
    date: z.date().nullable().refine((date) => date !== null, 'Vui lòng chọn ngày.'),
    category: z.enum(Object.entries(postCategoryTitles).map(p => p[0]) as PostCategory[], 'Định dạng không đúng'),
    cover: z.union([
        z.custom<UploadImage>(
            (value) => typeof value === 'string' || (typeof value === 'object' && value !== null),
            { message: 'Vui lòng chọn ảnh bìa.' }
        ),
        z.null(),
    ]),
    images: z.custom<UploadImage[]>(
        (value) => Array.isArray(value) && value.every((v) => typeof v === 'string' || (typeof v === 'object' && v !== null)),
        { message: 'Vui lòng chọn ảnh sự kiện.' }
    ),
})

export { postFormSchema }