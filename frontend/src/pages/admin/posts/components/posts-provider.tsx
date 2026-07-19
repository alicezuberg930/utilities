import { createContext, useContext, useState } from 'react'
import { type Post } from '@/@types/post'

type PostsDialogType = 'add' | 'edit' | 'delete'

type PostsContextType = {
  open: PostsDialogType | null
  setOpen: (value: PostsDialogType | null) => void
  currentRow: Post | null
  setCurrentRow: React.Dispatch<React.SetStateAction<Post | null>>
}

const PostsContext = createContext<PostsContextType | null>(null)

export const PostsProvider = ({ children }: { children: React.ReactNode }) => {
  const [open, setOpen] = useState<PostsDialogType | null>(null)
  const [currentRow, setCurrentRow] = useState<Post | null>(null)

  return (
    <PostsContext.Provider value={{ open, setOpen, currentRow, setCurrentRow }}>
      {children}
    </PostsContext.Provider>
  )
}

export const usePosts = () => {
  const context = useContext(PostsContext)

  if (!context) {
    throw new Error('usePosts must be used within <PostsProvider>')
  }

  return context
}
