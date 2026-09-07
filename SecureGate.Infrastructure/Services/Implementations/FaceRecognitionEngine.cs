using FaceAiSharp;
using SecureGate.Infrastructure.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SecureGate.Infrastructure.Services.Implementations
{
    // FaceAiSharp ONNX modellarini singleton sifatida ushlab turadigan dvigatel.
    // Modellar (SCRFD detektor + ArcFace embedding) birinchi marta NuGet paketidan yuklanadi.
    //
    // Thread safety: FaceAiSharp sessiyalari ichida bir vaqtning o'zida bir nechta
    // chaqirish bilan ishlamaydi, shuning uchun SemaphoreSlim orqali ketma-ket ishlatamiz.
    // Bizning yuk (~1-2 fps har kamera × bir nechta kamera) uchun bu yetarli.
    public class FaceRecognitionEngine : IFaceRecognitionEngine, IDisposable
    {
        private readonly IFaceDetectorWithLandmarks _detector;
        private readonly IFaceEmbeddingsGenerator _embeddings;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly ILogger<FaceRecognitionEngine> _logger;

        // Stream'da kichik/past sifatli yuzlarni filtrlash. Profil rasmida (registratsiyada)
        // bu filtrlar qo'llanilmaydi — u yerda bitta yuz bo'lib har qanday holatda kerak.
        private readonly int _minFaceWidth;
        private readonly float _minDetectionConfidence;

        public int EmbeddingSize => 512;

        public FaceRecognitionEngine(IConfiguration config, ILogger<FaceRecognitionEngine> logger)
        {
            _logger = logger;
            _minFaceWidth = config.GetValue<int?>("FaceRecognition:MinFaceWidth") ?? 80;
            _minDetectionConfidence = (float)(config.GetValue<double?>("FaceRecognition:MinDetectionConfidence") ?? 0.6);
            _detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
            _embeddings = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
            _logger.LogInformation("FaceRecognitionEngine ishga tushdi (SCRFD + ArcFace, embedding={Size}, minFaceW={W}, minDetConf={C})",
                EmbeddingSize, _minFaceWidth, _minDetectionConfidence);
        }

        public async Task<float[]?> ComputeEmbeddingFromFileAsync(string absolutePath, CancellationToken ct = default)
        {
            if (!File.Exists(absolutePath))
            {
                _logger.LogWarning("Embedding uchun fayl topilmadi: {Path}", absolutePath);
                return null;
            }

            try
            {
                using var image = await Image.LoadAsync<Rgb24>(absolutePath, ct);
                return await ComputeBestEmbeddingAsync(image, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Faylda embedding hisoblashda xato: {Path}", absolutePath);
                return null;
            }
        }

        public async Task<IReadOnlyList<DetectedFace>> DetectFacesAsync(byte[] imageBytes, CancellationToken ct = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return Array.Empty<DetectedFace>();

            try
            {
                using var image = Image.Load<Rgb24>(imageBytes);
                return await DetectAllInternalAsync(image, ct);
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<DetectedFace>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bayt massivida yuz aniqlashda xato");
                return Array.Empty<DetectedFace>();
            }
        }

        public float Similarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0)
                return -1f;

            // ArcFace embeddinglari L2-normalizatsiyalangan → dot product = kosinus o'xshashlik
            float sum = 0f;
            for (int i = 0; i < a.Length; i++)
                sum += a[i] * b[i];
            return sum;
        }

        // --- Internal helpers ---

        // Gate'ni ASINXRON kutamiz — bloklovchi _gate.Wait() thread pool'ni band qilardi.
        private async Task<float[]?> ComputeBestEmbeddingAsync(Image<Rgb24> image, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var faces = _detector.DetectFaces(image);
                if (faces.Count == 0) return null;

                // Eng katta yuzni tanlaymiz (anketa rasmida odatda bitta yuz bo'ladi,
                // lekin agar fonda boshqa odam bo'lsa — kattasi to'g'ri)
                var primary = faces.OrderByDescending(f => f.Box.Width * f.Box.Height).First();
                if (primary.Landmarks == null || primary.Landmarks.Count == 0) return null;

                using var aligned = image.Clone();
                _embeddings.AlignFaceUsingLandmarks(aligned, primary.Landmarks);
                var emb = _embeddings.GenerateEmbedding(aligned);
                NormalizeInPlace(emb); // ArcFace chiqishini L2-normalizatsiya qilamiz (kosinus o'xshashlik uchun)
                return emb;
            }
            finally
            {
                _gate.Release();
            }
        }

        // Gate'ni ASINXRON kutamiz — bloklovchi _gate.Wait() thread pool'ni band qilardi.
        // ONNX inferens sinxron bajariladi, lekin chaqiruvchi (CameraStreamWorker) alohida
        // LongRunning thread'da ishlaydi, shuning uchun thread pool starvation bo'lmaydi.
        private async Task<IReadOnlyList<DetectedFace>> DetectAllInternalAsync(Image<Rgb24> image, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var faces = _detector.DetectFaces(image);
                if (faces.Count == 0) return Array.Empty<DetectedFace>();

                var result = new List<DetectedFace>(faces.Count);
                foreach (var face in faces)
                {
                    if (face.Landmarks == null || face.Landmarks.Count == 0) continue;

                    // Filter 1: detection confidence past bo'lsa o'tkazib yuboramiz (false-positive ehtimoli yuqori)
                    var detConfidence = face.Confidence ?? 0f;
                    if (detConfidence < _minDetectionConfidence) continue;

                    // Filter 2: yuz juda kichik bo'lsa o'tkazib yuboramiz (embedding ishonchsiz)
                    if (face.Box.Width < _minFaceWidth) continue;

                    using var aligned = image.Clone();
                    _embeddings.AlignFaceUsingLandmarks(aligned, face.Landmarks);
                    var emb = _embeddings.GenerateEmbedding(aligned);
                    NormalizeInPlace(emb);

                    var box = new BoundingBox(
                        (int)face.Box.X,
                        (int)face.Box.Y,
                        (int)face.Box.Width,
                        (int)face.Box.Height);

                    result.Add(new DetectedFace(emb, detConfidence, box));
                }
                return result;
            }
            finally
            {
                _gate.Release();
            }
        }

        private static void NormalizeInPlace(float[] v)
        {
            double sumSq = 0;
            for (int i = 0; i < v.Length; i++) sumSq += v[i] * v[i];
            var norm = (float)Math.Sqrt(sumSq);
            if (norm < 1e-8f) return; // bo'sh vektor — qoldiramiz
            var inv = 1f / norm;
            for (int i = 0; i < v.Length; i++) v[i] *= inv;
        }

        public void Dispose()
        {
            _gate.Dispose();
            (_detector as IDisposable)?.Dispose();
            (_embeddings as IDisposable)?.Dispose();
        }
    }
}
