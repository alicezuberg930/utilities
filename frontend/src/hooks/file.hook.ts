import { useMutation } from "@tanstack/react-query"
import { uploadFile } from "../services/api.service"
import { showResponseError } from "../lib/utils"
import type { UploadFileParams, UploadFileResponse } from "@/@types/file"

export const useUploadFileHook = () => {
    return useMutation<UploadFileResponse, unknown, UploadFileParams>({
        mutationFn: (file) => uploadFile(file),
        onError(error) { showResponseError(error) }
    })
}
