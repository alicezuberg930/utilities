import { CirclePlus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useBanners } from './banners-provider'

export const BannersPrimaryButtons = () => {
  const { setCurrentRow, setOpen } = useBanners()

  return (
    <Button
      className='gap-1.5'
      onClick={() => {
        setCurrentRow(null)
        setOpen('add')
      }}
    >
      <CirclePlus size={18} />
      <span>Thêm mới</span>
    </Button>
  )
}
