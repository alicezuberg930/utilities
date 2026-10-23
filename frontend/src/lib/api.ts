export const API = {
    AUTH: {
        LOGIN: "/auth/login",
    },
    USERS: {
        PROFILE: "/users/profile",
    },
    BANNERS: "/banners",
    POSTS: "/posts",
    LOGS: "/logs",
    UPLOAD_FILE: "/file/upload",
    INFORMATION: "/information",
    EVENTS: "/events",
    YOUTUBE: {
        INFO: "/youtube/info",
        JOB: "/youtube/job",
        PROGRESS: (jobId: string) => `/youtube/progress/${jobId}`,
        FILE: (jobId: string) => `/youtube/file/${jobId}`,
    },
    CONVERT: {
        JOB: "/convert/job",
        PROGRESS: (jobId: string) => `/convert/progress/${jobId}`,
        FILE: (jobId: string) => `/convert/file/${jobId}`,
    },
    IMAGE_COMPRESSION: {
        JOB: "/image-compression/job",
        PROGRESS: (jobId: string) => `/image-compression/progress/${jobId}`,
        FILE: (jobId: string) => `/image-compression/file/${jobId}`,
    },
}
