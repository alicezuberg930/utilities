import Section from '@/layout/public/section'

const NewsPage = () => {
  return (
    <div className='mb-6'>
      <Section title='THƯ VẬN ĐỘNG' />
      <iframe
        className='block h-screen w-full border-0'
        src='/assets/news/Thu_Ngo_Van_Dong_Vu_Lan_Trang_2023.pdf'
        title='PDF Viewer'
      />
    </div>
  )
}

export default NewsPage