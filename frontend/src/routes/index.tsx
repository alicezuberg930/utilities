import { createRootRoute, createRoute, createRouter, Navigate, Outlet, RouterProvider } from "@tanstack/react-router"
import { lazy, Suspense, type ComponentType } from "react"
import { NotFoundPage, VideoPage, HomePage, DesignPage, PhotoshootPage, ChaoTinhThuongPage, ChuongTrinhThuongNienPage, HoTroHoanCanhPage, TiepSucTriThucPage, RulePage, CriteriaPage, StructurePage, ContactPage, NewsPage, ActivityDetailsPage } from '@/pages/public'
import { ROOT_CMS } from "./path"
import { queryClient } from "@/providers/query-client-provider"
import { AuthGuard } from "@/layout/admin/auth-guard"
import ShimmerList from "@/layout/common/shimmer-list"
import { PublicLayout } from "@/layout/public"

const LoginPage = lazy(() => import('@/pages/admin/login'))
const AdminLayout = lazy(() => import('@/layout/admin/admin-layout'))
const PostsPage = lazy(() => import('@/pages/admin/posts'))
const LogsPage = lazy(() => import('@/pages/admin/logs'))
const BannersPage = lazy(() => import('@/pages/admin/banners'))
const EventsPage = lazy(() => import('@/pages/admin/events'))
const InformationPage = lazy(() => import('@/pages/admin/information'))

const lazyRoute = (Component: ComponentType) => () => (
    <Suspense fallback={<ShimmerList />}>
        <Component />
    </Suspense>
)

const guardedLazyRoute = (Component: ComponentType) => () => (
    <AuthGuard>
        <Suspense fallback={<ShimmerList />}>
            <Component />
        </Suspense>
    </AuthGuard>
)

const rootRoute = createRootRoute({
    component: Outlet,
    notFoundComponent: NotFoundPage,
})

const loginRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/login',
    component: lazyRoute(LoginPage),
})

const publicRoute = createRoute({
    getParentRoute: () => rootRoute,
    id: 'public',
    component: PublicLayout,
})

const homeRoute = createRoute({
    getParentRoute: () => publicRoute,
    path: '/',
    component: HomePage,
})

const publicChildRoutes = [
    homeRoute,
    createRoute({ getParentRoute: () => publicRoute, path: 'design', component: DesignPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'photoshoot', component: PhotoshootPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'video', component: VideoPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'chao-tinh-thuong', component: ChaoTinhThuongPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'chuong-trinh-thuong-nien', component: ChuongTrinhThuongNienPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'ho-tro-hoan-canh', component: HoTroHoanCanhPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'tiep-suc-tri-thuc', component: TiepSucTriThucPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'rule', component: RulePage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'criteria', component: CriteriaPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'structure', component: StructurePage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'contact', component: ContactPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'news', component: NewsPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'chao-tinh-thuong/$slug', component: ActivityDetailsPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'ho-tro-hoan-canh/$slug', component: ActivityDetailsPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'tiep-suc-tri-thuc/$slug', component: ActivityDetailsPage }),
    createRoute({ getParentRoute: () => publicRoute, path: 'chuong-trinh-thuong-nien/$slug', component: ActivityDetailsPage }),
]

const adminRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: ROOT_CMS,
    component: guardedLazyRoute(AdminLayout),
})

const adminChildRoutes = [
    createRoute({ getParentRoute: () => adminRoute, path: '/', component: () => <Navigate to='/cms/post/list' replace /> }),
    createRoute({ getParentRoute: () => adminRoute, path: 'post', component: () => <Navigate to='/cms/post/list' replace /> }),
    createRoute({ getParentRoute: () => adminRoute, path: 'post/list', component: lazyRoute(PostsPage) }),
    createRoute({ getParentRoute: () => adminRoute, path: 'log', component: () => <Navigate to='/cms/log/list' replace /> }),
    createRoute({ getParentRoute: () => adminRoute, path: 'log/list', component: lazyRoute(LogsPage) }),
    createRoute({ getParentRoute: () => adminRoute, path: 'banner', component: () => <Navigate to='/cms/banner/list' replace /> }),
    createRoute({ getParentRoute: () => adminRoute, path: 'banner/list', component: lazyRoute(BannersPage) }),
    createRoute({ getParentRoute: () => adminRoute, path: 'event', component: () => <Navigate to='/cms/event/list' replace /> }),
    createRoute({ getParentRoute: () => adminRoute, path: 'event/list', component: lazyRoute(EventsPage) }),
    createRoute({ getParentRoute: () => adminRoute, path: 'information', component: lazyRoute(InformationPage) }),
]

const routeTree = rootRoute.addChildren([
    loginRoute,
    publicRoute.addChildren(publicChildRoutes),
    adminRoute.addChildren(adminChildRoutes),
])

const router = createRouter({
    routeTree,
    context: { queryClient: queryClient() },
    defaultPreload: 'intent',
    defaultPreloadStaleTime: 0,
})

export const Router = () => <RouterProvider router={router} />
