import FileConversion from '@/components/file-conversion'
import YoutubeDownloader from '@/components/youtube-downloader'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ClapperboardIcon, FileVideoIcon } from 'lucide-react'

const HomePage = () => (
  <Tabs defaultValue='convert' className='w-full'>
    <TabsList className='mx-auto mt-6 grid h-10 w-full max-w-md grid-cols-2'>
      <TabsTrigger value='convert'>
        <FileVideoIcon /> File converter
      </TabsTrigger>
      <TabsTrigger value='youtube'>
        <ClapperboardIcon /> YouTube downloader
      </TabsTrigger>
    </TabsList>
    <TabsContent value='convert'>
      <FileConversion />
    </TabsContent>
    <TabsContent value='youtube'>
      <YoutubeDownloader />
    </TabsContent>
  </Tabs>
)

export default HomePage
