import { ThemeProvider as NextThemesProvider } from 'next-themes'
import type { ThemeProviderProps } from 'next-themes'

export const ThemeProvider = ({
  children,
  defaultTheme = 'light',
  storageKey = 'theme',
  attribute = 'class',
  disableTransitionOnChange = true,
  ...props
}: ThemeProviderProps) => {
  return (
    <NextThemesProvider
      attribute={attribute}
      defaultTheme={defaultTheme}
      storageKey={storageKey}
      disableTransitionOnChange={disableTransitionOnChange}
      {...props}
    >
      {children}
    </NextThemesProvider>
  )
}