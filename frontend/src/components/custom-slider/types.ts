import type { ReactNode } from "react"

export type SliderProps = {
    children?: ReactNode
    slidesToShow?: number
    autoplay?: boolean
    autoplaySpeed?: number
    loop?: boolean
    showDot?: boolean
    showButton?: boolean
    responsive?: {
        breakpoint: number
        slidesToShow: number
    }[]
}