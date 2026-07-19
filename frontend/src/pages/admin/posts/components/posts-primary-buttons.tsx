import { CirclePlus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { usePosts } from './posts-provider'

export const PostsPrimaryButtons = () => {
  const { setCurrentRow, setOpen } = usePosts()

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
