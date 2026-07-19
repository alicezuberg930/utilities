import { createContext, useContext, useState } from 'react'
import { type Banner } from '@/@types/banner'

type BannersDialogType = 'add' | 'edit' | 'delete'

type BannersContextType = {
  open: BannersDialogType | null
  setOpen: (value: BannersDialogType | null) => void
  currentRow: Banner | null
  setCurrentRow: React.Dispatch<React.SetStateAction<Banner | null>>
}

const BannersContext = createContext<BannersContextType | null>(null)

export const BannersProvider = ({ children }: { children: React.ReactNode }) => {
  const [open, setOpen] = useState<BannersDialogType | null>(null)
  const [currentRow, setCurrentRow] = useState<Banner | null>(null)

  return (
    <BannersContext.Provider value={{ open, setOpen, currentRow, setCurrentRow }}>
      {children}
    </BannersContext.Provider>
  )
}

export const useBanners = () => {
  const context = useContext(BannersContext)

  if (!context) {
    throw new Error('useBanners must be used within <BannersProvider>')
  }

  return context
}
