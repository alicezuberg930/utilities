import z from "zod"

const loginFormSchema = z.object({
    username: z.string().trim().min(1, 'Vui lòng nhập tên người dùng'),
    password: z.string().trim().min(6, 'Vui lòng nhập mật khẩu'),
})

export { loginFormSchema }