import { PostsActionDialog } from './posts-action-dialog'
import { PostsDeleteDialog } from './posts-delete-dialog'
import { usePosts } from './posts-provider'

export const PostsDialogs = () => {
  const { open, setOpen, currentRow, setCurrentRow } = usePosts()

  const closeDialog = (state: boolean) => {
    if (state) return
    setOpen(null)
    setTimeout(() => setCurrentRow(null), 300)
  }

  return (
    <>
      <PostsActionDialog
        key={currentRow?._id ?? 'post-add'}
        currentRow={currentRow}
        open={open === 'add' || open === 'edit'}
        onOpenChange={closeDialog}
      />

      {currentRow && (
        <PostsDeleteDialog
          key={`post-delete-${currentRow._id}`}
          currentRow={currentRow}
          open={open === 'delete'}
          onOpenChange={closeDialog}
        />
      )}
    </>
  )
}
