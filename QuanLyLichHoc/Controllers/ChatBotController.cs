using Microsoft.AspNetCore.Mvc;
using QuanLyLichHoc.Services;

namespace QuanLyLichHoc.Controllers
{
    public class ChatBotController : Controller
    {
        private readonly GeminiService _geminiService;

        public ChatBotController(GeminiService geminiService)
        {
            _geminiService = geminiService;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] UserQuery query)
        {
            // Kiểm tra dữ liệu đầu vào
            if (query == null || string.IsNullOrWhiteSpace(query.Message))
            {
                return BadRequest("Vui lòng nhập câu hỏi.");
            }

            // Lấy tên người dùng đang đăng nhập (nếu chưa login thì là "Khách")
            // Thông tin này giúp AI biết đang nói chuyện với ai (Học sinh hay GV)
            string username = User.Identity.IsAuthenticated ? User.Identity.Name : "Khách";

            // Gọi Service xử lý logic AI
            var answer = await _geminiService.GetAnswer(query.Message, username);

            // Xử lý xuống dòng (\n) thành thẻ <br> để hiển thị đẹp trên web
            if (!string.IsNullOrEmpty(answer))
            {
                answer = answer.Replace("\n", "<br>");
            }

            return Ok(new { answer });
        }

        // Class nội bộ để hứng dữ liệu JSON từ Client
        public class UserQuery
        {
            public string Message { get; set; }
        }
    }
}