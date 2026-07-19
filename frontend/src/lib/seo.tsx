import { Helmet } from "react-helmet"

interface SEOProps {
    title?: string
    image?: string
    url?: string
    keywords?: string[]
    description?: string
    type?: string
}

const SITE_URL = "https://www.anhsangtuthien.com"

const getAbsoluteUrl = (value?: string) => {
    if (!value) return ""
    try {
        return new URL(value, SITE_URL).toString()
    } catch {
        return value
    }
}

const getCurrentUrl = () => {
    if (typeof window !== "undefined") return window.location.href
    return SITE_URL
}

export const SEO = ({ title, image, url, keywords = [], description, type = "article" }: SEOProps) => {
    const keywordContent = Array.isArray(keywords) ? keywords.join(",") : keywords
    const pageUrl = getAbsoluteUrl(url) || getCurrentUrl()
    const imageUrl = getAbsoluteUrl(image)

    return (
        <Helmet>
            <title itemProp="name" lang="en">{title}</title>
            <meta name="keywords" content={keywordContent} />
            <meta name="description" content={description} />
            {/* open graph */}
            <meta property="og:type" content={type} />
            <meta property="og:url" content={pageUrl} />
            <meta property="og:title" content={title} />
            <meta property="og:description" content={description} />
            <meta property="og:site_name" content="Ánh sáng từ thiện" />
            {imageUrl && <meta property="og:image" content={imageUrl} />}
            {imageUrl && <meta property="og:image:secure_url" content={imageUrl} />}
            {/* open graph twitter */}
            <meta name="twitter:card" content={imageUrl ? "summary_large_image" : "summary"} />
            <meta name="twitter:title" content={title} />
            <meta name="twitter:description" content={description} />
            {imageUrl && <meta name="twitter:image" content={imageUrl} />}
            <link rel="canonical" href={pageUrl} />
        </Helmet>
    )
}
