import { LazyLoadImage } from '@/components/lazy-load-image'
import Section from '@/layout/public/section'

const CriteriaPage = () => {
  return (
    <>
      <Section title='TIÊU CHÍ NHÓM' />
      <div className='space-y-12 mb-6 max-w-3xl mx-auto'>
        <div className='shadow-md rounded-lg overflow-hidden'>
          <LazyLoadImage
            className='w-full aspect-video object-cover hover:scale-110 transition-all duration-1000'
            src='./assets/criteria/chao-tinh-thuong-criteria.jpg'
            alt='chao-tinh-thuong'
            effect='blur'
          />
          <div className='p-3'>
            <span className='font-bold text-lg text-main-color'>CHÁO TÌNH THƯƠNG</span>
            <ul>
              <li>Hàng tháng vào chủ nhật của tuần thứ 3</li>
              <li>Nấu cháo thịt bằm phát tại các bệnh viện ở TP.HCM</li>
              <li>Chi phí vận động mạnh thường quân</li>
            </ul>
          </div>
        </div>

        <div className='shadow-md rounded-lg overflow-hidden'>
          <LazyLoadImage
            className='w-full aspect-video object-cover hover:scale-110 transition-all duration-1000'
            src='./assets/criteria/chuong-trinh-thuong-nien-criteria.jpg'
            alt='chuong-trinh-thuong-nien'
            effect='blur'
          />
          <div className='p-3'>
            <span className='font-bold text-lg text-main-color'>CHƯƠNG TRÌNH THƯỜNG NIÊN</span>
            <ul>
              <li>
                <strong className='text-main-color'>Tên các chương trình:</strong> tết tình thương, mùa hè yêu thương, cùng bé đến trường, vu lan trắng, vui mùa trung thu, mùa đông ấm áp.
              </li>
              <li>Các chương trình có thể được tiết giảm tùy thuộc vào nguồn quỹ và tình hình hàng năm.</li>
              <li>Chi phí từ quỹ nhóm & vận động thêm các mạnh thường quân.</li>
            </ul>
          </div>
        </div>

        <div className='shadow-md rounded-lg overflow-hidden'>
          <LazyLoadImage
            className='w-full aspect-video object-cover hover:scale-110 transition-all duration-1000'
            src='./assets/criteria/ho-tro-hoan-canh.jpg'
            alt='ho-tro-hoan-canh'
            effect='blur'
          />
          <div className='p-3'>
            <span className='font-bold text-lg text-main-color'>
              CÁC HOÀN CẢNH KHÓ KHĂN
            </span>
            <ul>
              <li>
                <strong className='text-main-color'>Chỉ xét 2 đối tượng:</strong> cụ già neo đơn và trẻ em có nguy cơ bỏ học. Sống tại TP.HCM hoặc các tỉnh lân cận. Không xét duyệt các hoàn cảnh đang nằm viện. Không được ở trọ.
              </li>
              <li>
                <strong className='text-main-color'>Cụ Già: </strong>Không con cái, bệnh già, bệnh hiểm nghèo, giấy xác nhận hoàn cảnh nghèo (nếu có)
              </li>
              <li>
                <strong className='text-main-color'>Trẻ Em</strong> mồ côi cha hoặc mẹ, cha hoặc mẹ bị bệnh hiểm nghèo, giấy xác nhận khó khăn (nhà trường hoặc địa phương xét duyệt).
              </li>
            </ul>
          </div>
        </div>

        <div className='shadow-md rounded-lg overflow-hidden'>
          <LazyLoadImage
            className='w-full aspect-video object-cover hover:scale-110 transition-all duration-1000'
            src='./assets/criteria/tiep-suc-tri-thuc-criteria.jpg'
            alt='tiep-suc-tri-thuc'
            effect='blur'
          />
          <div className='p-3'>
            <span className='font-bold text-lg text-main-color'>TIẾP SỨC TRI THỨC</span>
            <ul>
              <li>Phải sống tại tỉnh thành nơi cư trú (không được ở trọ)</li>
              <li>Hiện tại phải là học sinh cấp 3</li>
              <li>Học lực phải từ tiên tiến trở lên</li>
              <li>Mồ côi cha hoặc mẹ</li>
              <li>Thuộc hoàn cảnh khó khăn của địa phương</li>
            </ul>
          </div>
        </div>
      </div>
    </>
  )
}

export default CriteriaPage