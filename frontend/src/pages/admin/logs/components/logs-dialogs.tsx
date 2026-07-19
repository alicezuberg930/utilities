import { LogsDeleteDialog } from './logs-delete-dialog'
import { useLogs } from './logs-provider'

export const LogsDialogs = () => {
  const { open, setOpen, currentRow, setCurrentRow } = useLogs()

  const closeDialog = (state: boolean) => {
    if (state) return
    setOpen(null)
    setTimeout(() => setCurrentRow(null), 300)
  }

  return currentRow ? (
    <LogsDeleteDialog
      key={`log-delete-${currentRow._id}`}
      currentRow={currentRow}
      open={open === 'delete'}
      onOpenChange={closeDialog}
    />
  ) : null
}
