export const slideLeft = ({ delay = 0, x = -100, duration = 0.5 }) => ({
    invisible: { x, opacity: 0 },
    visible: {
        x: 0, opacity: 1, transition: { duration, delay }
    },
})

export const slideRight = ({ delay = 0, x = 100, duration = 0.5 }) => ({
    invisible: { x, opacity: 0 },
    visible: {
        x: 0, opacity: 1, transition: { duration, delay }
    },
})

export const slideUp = ({ delay = 0, y = 100, duration = 0.5 }) => ({
    invisible: { y, opacity: 0 },
    visible: {
        y: 0, opacity: 1, transition: { duration, delay }
    },
})