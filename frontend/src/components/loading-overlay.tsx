import { memo } from "react"
import { Spinner } from "./ui/spinner"

const LoadingOverlay = ({ isLoading }: { isLoading: boolean }) => {
    return (
        <div className={`absolute select-none top-0 bottom-0 right-0 left-0 bg-[rgba(0,0,0,.3)] z-50 ${isLoading ? '' : 'hidden'}`}>
            <div className="w-full h-full relative">
                <Spinner className='size-12' />
            </div>
        </div>
    )
}

export default memo(LoadingOverlay)