import { useState, type ReactNode } from 'react'
import { type RequestLog } from '@/@types/log'
import { createContext, useContext, type Dispatch, type SetStateAction } from 'react'

export type LogsDialogType = 'delete'

export type LogsContextType = {
  open: LogsDialogType | null
  setOpen: (value: LogsDialogType | null) => void
  currentRow: RequestLog | null
  setCurrentRow: Dispatch<SetStateAction<RequestLog | null>>
}

const LogsContext = createContext<LogsContextType | null>(null)

export const LogsProvider = ({ children }: { children: ReactNode }) => {
  const [open, setOpen] = useState<LogsDialogType | null>(null)
  const [currentRow, setCurrentRow] = useState<RequestLog | null>(null)

  return (
    <LogsContext.Provider value={{ open, setOpen, currentRow, setCurrentRow }}>
      {children}
    </LogsContext.Provider>
  )
}

export const useLogs = () => {
  const context = useContext(LogsContext)

  if (!context) {
    throw new Error('useLogs must be used within <LogsProvider>')
  }

  return context
}