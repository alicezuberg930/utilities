import { createContext, useContext, useState } from 'react'
import { type AppEvent } from '@/@types/event'

type EventsDialogType = 'add' | 'edit' | 'delete'

type EventsContextType = {
  open: EventsDialogType | null
  setOpen: (value: EventsDialogType | null) => void
  currentRow: AppEvent | null
  setCurrentRow: React.Dispatch<React.SetStateAction<AppEvent | null>>
}

const EventsContext = createContext<EventsContextType | null>(null)

export const EventsProvider = ({ children }: { children: React.ReactNode }) => {
  const [open, setOpen] = useState<EventsDialogType | null>(null)
  const [currentRow, setCurrentRow] = useState<AppEvent | null>(null)

  return (
    <EventsContext.Provider value={{ open, setOpen, currentRow, setCurrentRow }}>
      {children}
    </EventsContext.Provider>
  )
}

export const useEvents = () => {
  const context = useContext(EventsContext)

  if (!context) {
    throw new Error('useEvents must be used within <EventsProvider>')
  }

  return context
}
