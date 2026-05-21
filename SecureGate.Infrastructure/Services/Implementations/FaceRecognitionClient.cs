using SecureGate.Infrastructure.Services.Interfaces;

namespace SecureGate.Infrastructure.Services.Implementations
{
    // UsersService/StaffService bu interfeysni ishlatadi.
    // Avval bu HTTP klient edi (Python /embed ga so'rov yuborardi) —
    // endi to'g'ridan-to'g'ri ichki FaceRecognitionEngine'ga delegatsiya qiladi.
    // Tashqi servis kerakmas, ko'kdan oltinga.
    public class FaceRecognitionClient : IFaceRecognitionClient
    {
        private readonly IFaceRecognitionEngine _engine;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FaceRecognitionClient> _logger;

        public FaceRecognitionClient(
            IFaceRecognitionEngine engine,
            IWebHostEnvironment env,
            ILogger<FaceRecognitionClient> logger)
        {
            _engine = engine;
            _env = env;
            _logger = logger;
        }

        public async Task<float[]?> ComputeEmbeddingAsync(string webRelativePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(webRelativePath))
                return null;

            var relative = webRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.Combine(_env.WebRootPath, relative);

            if (!File.Exists(absolutePath))
            {
                _logger.LogWarning("Embedding uchun fayl topilmadi: {Path}", absolutePath);
                return null;
            }

            var embedding = await _engine.ComputeEmbeddingFromFileAsync(absolutePath, ct);
            if (embedding == null)
                _logger.LogInformation("Rasmda yuz topilmadi: {Path}", webRelativePath);
            return embedding;
        }

        public Task<bool> IsHealthyAsync(CancellationToken ct = default)
        {
            // Endi tashqi servis yo'q — engine konstruktorda muvaffaqiyatli ishga tushgan
            // bo'lsa, har doim "sog'lom" deb hisoblaymiz.
            return Task.FromResult(true);
        }
    }
}
