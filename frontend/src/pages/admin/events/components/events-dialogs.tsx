import { EventsActionDialog } from './events-action-dialog'
import { EventsDeleteDialog } from './events-delete-dialog'
import { useEvents } from './events-provider'

export const EventsDialogs = () => {
  const { open, setOpen, currentRow, setCurrentRow } = useEvents()

  const closeDialog = (state: boolean) => {
    if (state) return
    setOpen(null)
    setTimeout(() => setCurrentRow(null), 300)
  }

  return (
    <>
      <EventsActionDialog
        key={currentRow?._id ?? 'event-add'}
        currentRow={currentRow}
        open={open === 'add' || open === 'edit'}
        onOpenChange={closeDialog}
      />

      {currentRow && (
        <EventsDeleteDialog
          key={`event-delete-${currentRow._id}`}
          currentRow={currentRow}
          open={open === 'delete'}
          onOpenChange={closeDialog}
        />
      )}
    </>
  )
}
