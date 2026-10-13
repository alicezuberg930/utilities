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
}
