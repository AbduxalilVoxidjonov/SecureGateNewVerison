using OpenCvSharp;

namespace SecureGate.Infrastructure.Services.Implementations
{
    /// <summary>
    /// Snapshot (kadr) rasmlarini diskka yozishdan oldin kichraytirish/siqish uchun yordamchi.
    ///
    /// <para>
    /// Sabab: har aniqlanishda to'liq o'lchamli (FullHD/4K) JPEG saqlansa, disk juda tez to'ladi.
    /// Snapshot faqat "kim ko'rindi" ni ko'rsatish uchun kerak — 640px kenglik va JPEG q75 yetarli.
    /// </para>
    /// </summary>
    public static class SnapshotImage
    {
        /// <summary>Snapshot uchun standart maksimal kenglik (piksel).</summary>
        public const int DefaultMaxWidth = 640;

        /// <summary>Snapshot uchun standart JPEG sifati.</summary>
        public const int DefaultQuality = 75;

        /// <summary>
        /// OpenCV <see cref="Mat"/> kadrni snapshot sifatida JPEG'ga kodlaydi
        /// (kerak bo'lsa <paramref name="maxWidth"/> gacha kichraytiradi).
        /// </summary>
        public static byte[] Encode(Mat frame, int maxWidth = DefaultMaxWidth, int quality = DefaultQuality)
        {
            var prm = new ImageEncodingParam(ImwriteFlags.JpegQuality, Math.Clamp(quality, 1, 100));

            if (maxWidth > 0 && frame.Width > maxWidth)
            {
                var scale = maxWidth / (double)frame.Width;
                using var resized = new Mat();
                Cv2.Resize(frame, resized, new Size(maxWidth, Math.Max(1, (int)(frame.Height * scale))));
                return resized.ImEncode(".jpg", prm);
            }

            return frame.ImEncode(".jpg", prm);
        }

        /// <summary>
        /// Tayyor JPEG baytlarni snapshot o'lchamiga keltiradi. Kadr allaqachon kichik bo'lsa
        /// yoki dekodlash muvaffaqiyatsiz bo'lsa — asl baytlarni qaytaradi (hech qachon null emas).
        /// </summary>
        public static byte[] Downscale(byte[] jpegBytes, int maxWidth = DefaultMaxWidth, int quality = DefaultQuality)
        {
            if (jpegBytes == null || jpegBytes.Length == 0) return jpegBytes ?? Array.Empty<byte>();

            try
            {
                using var decoded = Cv2.ImDecode(jpegBytes, ImreadModes.Color);
                if (decoded.Empty()) return jpegBytes;
                if (maxWidth > 0 && decoded.Width <= maxWidth) return jpegBytes;

                return Encode(decoded, maxWidth, quality);
            }
            catch
            {
                // Dekodlash imkoni bo'lmasa — asl baytlar saqlanadi (ma'lumot yo'qotmaymiz).
                return jpegBytes;
            }
        }
    }
}
