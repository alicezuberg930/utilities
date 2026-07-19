FROM node:22-bookworm

RUN apt-get update && apt-get install -y python3 python3-pip ffmpeg

RUN pip3 install yt-dlp

WORKDIR /app

COPY . .

RUN npm ci

CMD ["npm", "start"]