import Section from '@/layout/public/section'
import { information } from '@/lib/queries/information'
import { useQuery } from '@tanstack/react-query'

const ContactPage = () => {
  const { data } = useQuery(information().one.queryOptions())

  return (
    <div className='mb-6'>
      <Section title='THÔNG TIN LIÊN HỆ NHÓM ÁNH SÁNG TỪ THIỆN' />
      <div className='flex flex-col md:flex-row'>
        <div className='flex-1'>
          <Section title='THÔNG TIN LIÊN HỆ' />
          <ul>
            <li>
              <p>
                <strong>
                  Website:
                  <a
                    href={data?.websiteURL ?? "/"}
                    className='text-main-color'
                  >
                    {' '}
                    {data?.websiteURL ?? ""}
                  </a>
                </strong>
              </p>
            </li>
            <li>
              <p>
                <strong>Hotline</strong>: {data?.hotline ?? ""}
              </p>
            </li>
            <li>
              <p>
                <strong>Địa chỉ nấu cháo:</strong> {data?.activityAddress ?? ""}
              </p>
            </li>
            <li>
              <p>
                <strong>Kho lưu trữ:</strong> {data?.storageAddress ?? ""}
              </p>
            </li>
          </ul>
        </div>
        <div className='flex-1'>
          <Section title='THÔNG TIN CHUYỂN KHOẢN' />
          <ul>
            <li>
              <p>
                <strong>Chuyển khoản:</strong> Chủ Tài Khoản Nguyễn Thanh Tân.
              </p>
            </li>
            <li>
              <p>Ngân hàng Á Châu: {data?.achaubankNumber ?? ""}</p>
            </li>
            <li>
              <p>Ngân hàng VPBank: {data?.vpbankNumber ?? ""}</p>
            </li>
          </ul>
        </div>
      </div>
    </div>
  )
}

export default ContactPage