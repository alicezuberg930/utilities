import { memo } from 'react'

const ShimmerPost = () => {
  return (
    <div className='animate-pulse' aria-label='Loading post' aria-busy='true'>
      <div className='mt-4 text-center'>
        <div className='mx-auto h-5 w-2/3 max-w-xl rounded bg-gray-300' />
        <hr className='my-4 block border-gray-200' />
      </div>

      <div className='mt-4'>
        <div className='aspect-video mx-auto w-full rounded-xl bg-gray-300 md:w-4/5' />
      </div>

      <div className='mx-auto mt-4 size-12 rounded-md bg-gray-300' />

      <div className='mx-auto mt-4 w-full space-y-3 md:w-4/5'>
        <div className='h-4 rounded bg-gray-300' />
        <div className='h-4 rounded bg-gray-300' />
        <div className='h-4 w-5/6 rounded bg-gray-300' />
        <div className='h-4 w-2/3 rounded bg-gray-300' />
      </div>

      <div className='my-4'>
        <div className='mx-auto w-full md:w-3/5'>
          <div className='text-center'>
            <div className='mx-auto h-5 w-1/2 rounded bg-gray-300' />
            <hr className='my-4 block border-gray-200' />
          </div>
          <div className='aspect-video w-full rounded-xl bg-gray-300' />
        </div>

        <div className='mx-auto mt-6 w-full md:w-3/5'>
          <div className='text-center'>
            <div className='mx-auto h-5 w-1/2 rounded bg-gray-300' />
            <hr className='my-4 block border-gray-200' />
          </div>
          {Array.from({ length: 2 }).map((_, index) => (
            <div
              className='my-4 aspect-video w-full rounded-xl bg-gray-300'
              key={index}
            />
          ))}
        </div>
      </div>
    </div>
  )
}

export default memo(ShimmerPost)
