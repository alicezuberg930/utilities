import { CirclePlus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useEvents } from './events-provider'

export const EventsPrimaryButtons = () => {
  const { setCurrentRow, setOpen } = useEvents()

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
