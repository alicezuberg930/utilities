import React from 'react'
import Section from '@/layout/public/section'
import { LazyLoadImage } from '@/components/lazy-load-image'

const designs = [
  {
    'name': 'NHẬN THIẾT KẾ TỜ RƠI, BROCHURE, BANNER, ALBUM, QUẢNG CÁO',
    'images': ['./assets/design/canh-en-mua-xuan.jpg', './assets/design/cung-be-den-truong.jpg', './assets/design/chao-tinh-thuong.jpg', './assets/design/mua-he-yeu-thuong.jpg', './assets/design/tiep-suc-tri-thuc.jpg', './assets/design/vui-mua-trung-thu.jpg', './assets/design/vu-lan-trang.jpg']
  }
]

const DesignPage = () => {
  return (
    <>
      {designs.map(design => (
        <React.Fragment key={design.name}>
          <Section title={design.name} />
          <div className='max-w-3xl mx-auto'>
            {design.images.map(image => (
              <div key={image} className='my-4'>
                <LazyLoadImage
                  wrapperClassName='image-container rounded-2xl relative aspect-video'
                  className='h-full w-full object-cover'
                  src={image}
                  alt={image}
                  effect='blur'
                />
              </div>
            ))}
          </div>
        </React.Fragment>
      ))}
    </>
  )
}

export default DesignPage