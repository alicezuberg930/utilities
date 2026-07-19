import { memo } from "react"

const ShimmerList = () => {
    return (
        <div className="animate-pulse" >
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
            <div className="mb-2.5 h-10 rounded bg-gray-200 dark:bg-gray-700"></div>
        </div >
    )
}

export default memo(ShimmerList)