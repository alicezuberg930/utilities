import { BannersActionDialog } from './banners-action-dialog'
import { BannersDeleteDialog } from './banners-delete-dialog'
import { useBanners } from './banners-provider'

export const BannersDialogs = () => {
  const { open, setOpen, currentRow, setCurrentRow } = useBanners()

  const closeDialog = (state: boolean) => {
    if (state) return
    setOpen(null)
    setTimeout(() => setCurrentRow(null), 300)
  }

  return (
    <>
      <BannersActionDialog
        key={currentRow?._id ?? 'banner-add'}
        currentRow={currentRow}
        open={open === 'add' || open === 'edit'}
        onOpenChange={closeDialog}
      />

      {currentRow && (
        <BannersDeleteDialog
          key={`banner-delete-${currentRow._id}`}
          currentRow={currentRow}
          open={open === 'delete'}
          onOpenChange={closeDialog}
        />
      )}
    </>
  )
}
