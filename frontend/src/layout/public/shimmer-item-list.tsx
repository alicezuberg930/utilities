import { memo } from "react"

const ShimmerItemList = ({ count = 5 }) => {
    return (
        <div className="flex flex-wrap mb-4 animate-pulse -mx-2.5">
            {
                Array.from({ length: count }).map((_, i) => (
                    <div className='w-full p-2.5 sm:w-1/2 lg:w-1/3' key={i}>
                        <div className='w-full'>
                            <div className='block'>
                                <div className='shadow-md space-y-3 rounded-lg overflow-hidden'>
                                    {/* Picture */}
                                    <div className='w-full h-72 bg-gray-300'></div>
                                    {/* Text */}
                                    <div className='space-y-3 p-3'>
                                        <div className="h-4 bg-gray-300 rounded" />
                                        <div className="h-4 bg-gray-300 rounded" />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                ))
            }
        </div>
    )
}

export default memo(ShimmerItemList)