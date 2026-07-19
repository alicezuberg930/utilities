import { memo } from "react"

const Section = ({ title }) => {
  return (
    <div className='my-4 border-2 border-main-color p-2 rounded-xl w-fit mx-auto'>
      <h3 className='text-main-color font-bold'>
        <p className='text-center'>{title}</p>
      </h3>
    </div>
  )
}

export default memo(Section)
