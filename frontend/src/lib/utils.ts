import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'
import { toast } from 'sonner'
import { HttpError } from './repository/http-error'
import { httpClient } from './repository/http-client'
import { useNavigate } from '@tanstack/react-router'

const cn = (...inputs: ClassValue[]) => {
  return twMerge(clsx(inputs))
}

const showResponseError = (error: unknown) => {
  const httpError = error instanceof HttpError ? error : undefined

  if (httpError) {
    if (httpError.status === 401) {
      const navigate = useNavigate()
      navigate({ to: '/sign-in' })
    }
    const data = httpError.data as { message?: unknown } | undefined
    const err = data?.message ?? httpError.message
    if (Array.isArray(err)) {
      err.forEach((e) => toast.error(String(e)))
    } else {
      toast.error(String(err))
    }
  } else if (error instanceof Error) {
    toast.error(error.message)
  } else {
    toast.error('Lỗi không xác định')
  }
}

const slugify = (text: string) => {
  // Map of Vietnamese characters to their base Latin equivalents
  const vietnameseMap: Record<string, string> = {
    'à': 'a', 'á': 'a', 'ả': 'a', 'ã': 'a', 'ạ': 'a',
    'ă': 'a', 'ằ': 'a', 'ắ': 'a', 'ẳ': 'a', 'ẵ': 'a', 'ặ': 'a',
    'â': 'a', 'ầ': 'a', 'ấ': 'a', 'ẩ': 'a', 'ẫ': 'a', 'ậ': 'a',
    'è': 'e', 'é': 'e', 'ẻ': 'e', 'ẽ': 'e', 'ẹ': 'e',
    'ê': 'e', 'ề': 'e', 'ế': 'e', 'ể': 'e', 'ễ': 'e', 'ệ': 'e',
    'ì': 'i', 'í': 'i', 'ỉ': 'i', 'ĩ': 'i', 'ị': 'i',
    'ò': 'o', 'ó': 'o', 'ỏ': 'o', 'õ': 'o', 'ọ': 'o',
    'ô': 'o', 'ồ': 'o', 'ố': 'o', 'ổ': 'o', 'ỗ': 'o', 'ộ': 'o',
    'ơ': 'o', 'ờ': 'o', 'ớ': 'o', 'ở': 'o', 'ỡ': 'o', 'ợ': 'o',
    'ù': 'u', 'ú': 'u', 'ủ': 'u', 'ũ': 'u', 'ụ': 'u',
    'ư': 'u', 'ừ': 'u', 'ứ': 'u', 'ử': 'u', 'ữ': 'u', 'ự': 'u',
    'ỳ': 'y', 'ý': 'y', 'ỷ': 'y', 'ỹ': 'y', 'ỵ': 'y',
    'đ': 'd',
    'À': 'A', 'Á': 'A', 'Ả': 'A', 'Ã': 'A', 'Ạ': 'A',
    'Ă': 'A', 'Ằ': 'A', 'Ắ': 'A', 'Ẳ': 'A', 'Ẵ': 'A', 'Ặ': 'A',
    'Â': 'A', 'Ầ': 'A', 'Ấ': 'A', 'Ẩ': 'A', 'Ẫ': 'A', 'Ậ': 'A',
    'È': 'E', 'É': 'E', 'Ẻ': 'E', 'Ẽ': 'E', 'Ẹ': 'E',
    'Ê': 'E', 'Ề': 'E', 'Ế': 'E', 'Ể': 'E', 'Ễ': 'E', 'Ệ': 'E',
    'Ì': 'I', 'Í': 'I', 'Ỉ': 'I', 'Ĩ': 'I', 'Ị': 'I',
    'Ò': 'O', 'Ó': 'O', 'Ỏ': 'O', 'Õ': 'O', 'Ọ': 'O',
    'Ô': 'O', 'Ồ': 'O', 'Ố': 'O', 'Ổ': 'O', 'Ỗ': 'O', 'Ộ': 'O',
    'Ơ': 'O', 'Ờ': 'O', 'Ớ': 'O', 'Ở': 'O', 'Ỡ': 'O', 'Ợ': 'O',
    'Ù': 'U', 'Ú': 'U', 'Ủ': 'U', 'Ũ': 'U', 'Ụ': 'U',
    'Ư': 'U', 'Ừ': 'U', 'Ứ': 'U', 'Ử': 'U', 'Ữ': 'U', 'Ự': 'U',
    'Ỳ': 'Y', 'Ý': 'Y', 'Ỷ': 'Y', 'Ỹ': 'Y', 'Ỵ': 'Y',
    'Đ': 'D'
  }

  return text
    // Replace Vietnamese characters with base Latin characters
    .split('').map((char) => vietnameseMap[char] ?? char).join('')
    // Replace all whitespace with hyphen
    .replace(/\s+/g, '-')
    // Remove any special characters except hyphen
    .replace(/[^a-zA-Z0-9-]/g, '')
    // Replace multiple consecutive hyphens with single hyphen
    .replace(/-+/g, '-')
    // Remove leading/trailing hyphens
    .replace(/^-+|-+$/g, '')
    .toLowerCase()
}

const alpha = (color: string, opacity: number): string => {
  // Handle hex colors
  if (color.startsWith('#')) {
    const hex = color.replace('#', '')
    const r = Number.parseInt(hex.substring(0, 2), 16)
    const g = Number.parseInt(hex.substring(2, 4), 16)
    const b = Number.parseInt(hex.substring(4, 6), 16)
    return `rgba(${r}, ${g}, ${b}, ${opacity})`
  }
  // Handle rgb/rgba colors
  if (color.startsWith('rgb')) {
    const match = color.match(/\d+/g)
    if (match && match.length >= 3) {
      return `rgba(${match[0]}, ${match[1]}, ${match[2]}, ${opacity})`
    }
  }
  return `rgba(0, 0, 0, ${opacity})`
}

/**
 * Generates page numbers for pagination with ellipsis
 * @param currentPage - Current page number (1-based)
 * @param totalPages - Total number of pages
 * @returns Array of page numbers and ellipsis strings
 *
 * Examples:
 * - Small dataset (≤5 pages): [1, 2, 3, 4, 5]
 * - Near beginning: [1, 2, 3, 4, '...', 10]
 * - In middle: [1, '...', 4, 5, 6, '...', 10]
 * - Near end: [1, '...', 7, 8, 9, 10]
 */
const getPageNumbers = (currentPage: number, totalPages: number) => {
  const maxVisiblePages = 5 // Maximum number of page buttons to show
  const rangeWithDots: (number | string)[] = []

  if (totalPages <= maxVisiblePages) {
    // If total pages is 5 or less, show all pages
    for (let i = 1; i <= totalPages; i++) {
      rangeWithDots.push(i)
    }
  } else {
    // Always show first page
    rangeWithDots.push(1)
    if (currentPage <= 3) {
      // Near the beginning: [1] [2] [3] [4] ... [10]
      for (let i = 2; i <= 4; i++) {
        rangeWithDots.push(i)
      }
      rangeWithDots.push('...', totalPages)
    } else if (currentPage >= totalPages - 2) {
      // Near the end: [1] ... [7] [8] [9] [10]
      rangeWithDots.push('...')
      for (let i = totalPages - 3; i <= totalPages; i++) {
        rangeWithDots.push(i)
      }
    } else {
      // In the middle: [1] ... [4] [5] [6] ... [10]
      rangeWithDots.push('...')
      for (let i = currentPage - 1; i <= currentPage + 1; i++) {
        rangeWithDots.push(i)
      }
      rangeWithDots.push('...', totalPages)
    }
  }
  return rangeWithDots
}

const stripHtml = (value: string) => value.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim()

const formatDuration = (duration: number): string => {
  const hour = duration >= 3600 ? `${(Math.floor(duration / 3600)).toString()}:` : ''
  const minute = ((Math.floor(duration / 60) % 60)).toString().padStart(2, '0')
  const second = (duration % 60).toString().padStart(2, '0')
  return `${hour}${minute}:${second}`
}

const textToSpeech = async (text: string) => {
  // first check to see if web speech API support vietnamese and use it
  const utterance = new SpeechSynthesisUtterance(text)
  const voices = window.speechSynthesis.getVoices()
  const vietnameseVoices = voices.filter(voice => voice.lang === 'vi-VN' || voice.lang === 'vi')
  if (vietnameseVoices[0]) {
    utterance.voice = vietnameseVoices[0]
    utterance.lang = 'vi-VN'
    utterance.rate = 1.0
    utterance.pitch = 1.0
    utterance.volume = 1.0
    window.speechSynthesis.speak(utterance)
  } else {
    const apiKey = import.meta.env.VITE_ELEVEN_LAB_API_KEY
    // if web speech API is not supported switch to eleven lab TTS API
    const response = await httpClient.post(
      'https://api.elevenlabs.io/v1/text-to-speech/JBFqnCBsd6RMkjVDRZzb',
      {
        text,
        model_id: 'eleven_v3',
        voice_settings: {
          stability: 0.5,
          similarity_boost: 1.0,
        },
      },
      {
        headers: {
          'Content-Type': 'application/json',
          ...(apiKey && { 'xi-api-key': apiKey }),
        },
        credentials: 'omit'
      }
    )

    if (!response.ok) throw new Error('Failed to generate speech')
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const audio = new Audio(url)
    audio.onended = () => {
      URL.revokeObjectURL(url)
    }
    await audio.play()
  }
}

export { cn, showResponseError, slugify, alpha, getPageNumbers, stripHtml, formatDuration, textToSpeech }