import Section from '@/layout/public/section'
import { Tree, TreeNode } from 'react-organizational-chart'

const StructureLabel = ({ title }: { title: string }) => (
  <div className='rounded-2xl border border-main-color bg-white px-4 py-3 text-center text-sm font-semibold text-slate-900 shadow-sm'>
    {title}
  </div>
)

const StructurePage = () => {
  return (
    <>
      <Section title='CƠ CẤU TỔ CHỨC CỦA NHÓM' />
      <div className='space-y-12 mb-6'>
        <div className='overflow-x-auto py-6'>
          <div className='mx-auto max-w-5xl'>
            <Tree
              lineWidth={'3px'}
              lineColor='#8b5cf6'
              lineBorderRadius={'10px'}
              label={<StructureLabel title='Trưởng Nhóm' />}
            >
              <TreeNode
                label={<StructureLabel title='Trợ Lý' />}
              />
              <TreeNode
                label={<StructureLabel title='Dẫn Chương Trình' />}
              />
              <TreeNode
                label={<StructureLabel title='Quản Trị Web' />}
              />
              <TreeNode
                label={<StructureLabel title='Quản Lý Nhân Sự' />}
              >
                <TreeNode
                  label={<StructureLabel title='Thành Viên' />}
                />
              </TreeNode>
              <TreeNode
                label={<StructureLabel title='Thiết Kế & Thư Ký' />}
              />
              <TreeNode
                label={<StructureLabel title='Kỹ Thuật' />}
              />
            </Tree>
          </div>
        </div>
        <div className='flex gap-8 flex-col md:flex-row'>
          <div className='flex-1'>
            <p>
              <strong className='text-main-color'>I. Trưởng Nhóm (01 người)</strong>
            </p>
            <ol>
              <li>Lập kế hoạch, chiến lược cho Nhóm hoạt động.</li>
              <li>Quan hệ đối ngoại: kết hợp với địa phương nơi người cần trợ giúp (nếu cần).</li>
              <li>Tìm hiểu rõ thông tin của nhân vật sắp được nhóm trợ giúp.</li>
              <li>Xem xét, sắp xếp và chọn ra từng hoàn cảnh cần trợ giúp trước tiên.</li>
              <li>Phối hợp với các thành viên tìm thêm hoàn cảnh khó khăn.</li>
              <li>Chọn ngày họp và đi từ thiện cho nhóm.</li>
              <li>Xây dựng, viết bài trên website.</li>
            </ol>
            <p>
              <strong className='text-main-color'>Điều kiện:</strong> tìm tòi, học hỏi, biết vi tính, biết giao tiếp, biết lập kế hoạch, tham gia đầy đủ.
            </p>
          </div>
          <div className='flex-1'>
            <p>
              <strong className='text-main-color'>II. Dẫn Chương Trình (02 người)</strong>
            </p>
            <ol>
              <li>Hỗ trợ Trưởng Nhóm: dẫn các chương trình lớn của nhóm.</li>
              <li>Am hiểu về cách dẫn chương trình..</li>
              <li>Hoạt náo sôi động &amp; tạo nhiều trò chơi mới cho các em nhỏ.</li>
            </ol>
            <p>
              <strong className='text-main-color'>Điều kiện:</strong> tìm tòi, học hỏi, biết vi tính, tham gia đầy đủ các chương trình lớn
            </p>
          </div>
        </div>
        <hr></hr>
        <div className='flex gap-8 flex-col md:flex-row'>
          <div className='flex-1'>
            <p>
              <strong className='text-main-color'>III. Trợ Lý (02 người)</strong>
            </p>
            <ol>
              <li>Hỗ trợ Trưởng Nhóm</li>
              <li>Lập kế hoạch</li>
              <li>Chiến lược cho Nhóm hoạt động</li>
              <li>Lập biên bản cuộc họp (nếu cần)</li>
              <li>Sắp xếp và chọn ra từng hoàn cảnh cần trợ giúp trước tiên.</li>
              <li>Quan quan hệ đối ngoại: kết hợp với địa phương nơi người cần trợ giúp (nếu cần).</li>
              <li>Tìm hiểu rõ thông tin của nhân vật sắp được nhóm trợ giúp (nếu cần).</li>
            </ol>
            <p>
              <strong className='text-main-color'>Điều kiện</strong>: tìm tòi, học hỏi,
              biết vi tính, biết giao tiếp, biết lập kế hoạch, biết cách xử lý tốt
              trong mọi tình huống, tham gia đầy đủ.
            </p>
          </div>
          <div className='flex-1'>
            <p>
              <strong className='text-main-color'>IV: Quản lý nhân sự (01 người)</strong>
            </p>
            <ol>
              <li>Kết nối tư tưởng giữa các thành viên với nhau, trong quan hệ đối nội</li>
              <li>Kết hợp với Trưởng Nhóm lập Kế hoạch, chiến lược cho Nhóm.</li>
              <li>Thông báo đến các thành viên khi nhóm trưởng yêu cầu.</li>
              <li>Kết hợp cùng Trưởng Nhóm chọn ngày để đi từ thiện..</li>
            </ol>
            <p>
              <strong className='text-main-color'>Điều kiện:</strong> tìm tòi, học hỏi,
              biết vi tính, biết giao tiếp, biết lập kế hoạch, siêng năng,tham gia
              đầy đủ.
            </p>
          </div>
        </div>
        <hr></hr>
        <div className='flex gap-8 flex-col md:flex-row'>
          <div className='flex-1'>
            <p>
              <strong className='text-main-color'>V: Thiết kế &amp; thư kí (02 người)</strong>
            </p>
            <ol>
              <li>Nhận nhiệm vụ của trưởng nhóm khi có yêu cầu thiết kế cho nhóm.</li>
              <li>Chịu trách nhiệm về các mẫu băng rôn, danh thiếp của nhóm.</li>
              <li>Viết biên bản nội cung cuộc họp nhóm.</li>
            </ol>
            <p>
              <strong className='text-main-color'>Điều kiện:</strong> tìm tòi, học hỏi,
              biết đồ hoạ, biết giao tiếp, biết lập kế hoạch, siêng năng, tham gia
              đầy đủ.&nbsp;&nbsp;&nbsp;&nbsp;
            </p>
            <p>&nbsp;</p>
          </div>
          <div className='flex-1'>
            <p>
              <strong className='text-main-color'>VI: Phó Nhóm Quản Trị Web (01 người)</strong>
            </p>
            <ol>
              <li>Học hỏi tìm cách xây dựng giao diện mới cho website</li>
              <li>Thảo luận với Trưởng Nhóm để lựa chọn giao diện cho website.</li>
              <li>Tuyệt đối không để lộ password website cho bất cứ ai khi chưa có sự cho phép của Trưởng Nhóm. Những thông tin muốn cập nhật trên website phải thông qua Trưởng Nhóm xem xét.</li>
            </ol>
            <p>
              <strong className='text-main-color'>Điều kiện:</strong> tìm tòi, học hỏi, giỏi về lập trình website, quản lý tốt tổ website, tham gia nhóm đầy đủ.
            </p>
          </div>
        </div>
        <hr></hr>
        <div className='flex gap-8 flex-col md:flex-row'>
          <div className='flex-1'>
            <p>
              <strong className='text-main-color'>VII: Các Tổ Trưởng</strong>
            </p>
            <ol>
              <li>Có trách nhiệm thu quỹ nhóm mình hàng tháng &amp; gửi về Trưởng Nhóm.</li>
              <li>Triển khai các thông tin (tin nhắn SMS, Email, văn bản …) đến các thành viên trong tổ khi nhận thông tin từ Trưởng Nhóm hoặc các Phó Nhóm qua: tin nhắn SMS, Email, văn bản</li>
              <li>Cập nhật, tổng hợp các ý kiến của các thành viên trong nhóm để phát biểu trong các cuộc họp.</li>
              <li>Tìm thành viên đại diện đi họp nếu như ngày đó vắng mặt.</li>
            </ol>
            <p>
              <strong className='text-main-color'>Điều kiện:</strong> tìm tòi, học hỏi, biết vi tính, biết giao tiếp, biết lập kế hoạch, siêng năng,tham gia đầy đủ.
            </p>
          </div>
          <div className='flex-1'>
            <p>
              <span>
                <strong className='text-danger'>LƯU Ý</strong>
              </span>
            </p>
            <ol>
              <li>Nhiệm vụ chung các thành viên: đóng tiền (hoặc thêm vật phẩm) đầy đủ.</li>
              <li>Mỗi thành viên đều là người thu thập thông tin để tìm những hoàn cảnh gia đình khó khăn (ưu tiên: người già neo đơn, các em nhỏ mồ côi).</li>
              <li>Mỗi thành viên có thể kêu gọi sự đóng góp của người thân trong gia đình, bạn bè, đồng nghiệp hay từ nguồn trợ giúp khác.</li>
              <li>Mục đích nhóm hỗ trợ cụ già neo đơn &amp; trẻ em có hoàn cảnh khó khăn.</li>
              <li>Khi đã nằm trong dàn quản lý nhóm, tuyệt đối những thông tin bảo mật của nhóm không được rò rỉ ra bên ngoài.
              </li>
            </ol>
          </div>
        </div>
      </div>
    </>
  )
}

export default StructurePage