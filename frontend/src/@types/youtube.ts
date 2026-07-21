type YtDlpFormat = {
  format_id: string
  format: string
  video_ext: string
  audio_ext: string
  vcodec: string
  acodec: string
  url: string
  ext: string
  resolution: string
  filesize: number
  abr: number
}

export type FilteredMetadata = {
  title: string;
  thumbnail: string;
  videoFormats: YtDlpFormat[];
  audioFormats: YtDlpFormat[];
  combinedFormat: string | null; // e.g. "398+251"
}