import { memo, useEffect } from 'react'
import moment from 'moment'
import { LazyLoadImage } from '@/components/lazy-load-image'
import { useQuery } from '@tanstack/react-query'
import { information } from '@/lib/queries/information'

const Footer = () => {
  const { data } = useQuery(information().one.queryOptions())

  return (
    <>
      <footer className='border-t border-gray-300 bg-gray-100 py-12 px-3 lg:px-0'>
        <div className='max-w-7xl mx-auto flex gap-4 flex-col md:flex-row'>
          <div className='flex-1'>
            <span className='text-main-color font-semibold text-xl'>
              THÔNG TIN VỀ CHÚNG TÔI
            </span>
            <ul className='mt-3 space-y-2'>
              <li>
                <span className='text-main-color'>Cháo tình thương: </span>
                <span>{data?.activityAddress ?? ''}</span>
              </li>
              <li>
                <span className='text-main-color'>Kho hàng: </span>
                <span>{data?.storageAddress ?? ''}</span>
              </li>
              <li>
                <span className='text-main-color'>Hotline: </span>
                <span>{data?.hotline}</span>
              </li>
              <li>
                <span className='text-main-color'>Email: </span>
                <span>{data?.email}</span>
              </li>
              <li className='flex items-center'>
                <a
                  target='_blank'
                  rel='noreferrer'
                  href={data?.facebookUrl}
                  className='mr-3'
                >
                  <LazyLoadImage
                    className='w-12 h-12'
                    src='/assets/facebook.png'
                    alt='facebook'
                    effect='blur'
                  />
                </a>
                <a
                  target='_blank'
                  rel='noreferrer'
                  href={data?.zaloURL}
                  className='mr-3'
                >
                  <LazyLoadImage
                    className='w-12 h-12'
                    src='/assets/zalo.png'
                    alt='zalo'
                    effect='blur'
                  />
                </a>
                <a
                  target='_blank'
                  rel='noreferrer'
                  href={data?.youtubeURL}
                >
                  <LazyLoadImage
                    className='w-12 h-12'
                    src='/assets/youtube.png'
                    alt='youtube'
                    effect='blur'
                  />
                </a>
              </li>
            </ul>
          </div>
        </div>
      </footer>
    </>
  )
}

export default memo(Footer)