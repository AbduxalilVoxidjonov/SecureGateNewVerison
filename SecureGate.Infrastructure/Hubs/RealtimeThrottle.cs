using System.Collections.Concurrent;

namespace SecureGate.Infrastructure.Hubs
{
    /// <summary>
    /// SignalR broadcast'lar uchun **leading-edge** throttle.
    ///
    /// <para>
    /// Nima uchun kerak: yuz aniqlash pipeline'i bir xil shaxsni har ~3 soniyada
    /// qayta aniqlaydi (cooldown). Har aniqlanishda ogohlantirish yuborilsa
    /// operator ekrani spam bilan to'ladi; har o'tishda to'liq dashboard
    /// statistikasi qayta hisoblansa DB behuda yuklanadi.
    /// </para>
    ///
    /// <para>
    /// Holat <b>static</b> — chunki chaqiruvchilar (FaceMatchHandler) scoped va
    /// har voqeada qaytadan yaratiladi, throttle esa jarayon bo'yicha umumiy
    /// bo'lishi shart. DI'ga yangi singleton qo'shilmasligi uchun ham shu yo'l
    /// tanlangan (Program.cs o'zgarmaydi).
    /// </para>
    ///
    /// <para>
    /// DIQQAT: bu leading-edge throttle — oynadagi BIRINCHI voqea o'tadi,
    /// qolganlari tashlanadi (trailing "oxirgi qiymatni keyin yuborish" YO'Q).
    /// Statistika har yuborishda DB'dan qayta hisoblangani uchun tashlangan
    /// voqealar keyingi yuborishga baribir kiradi.
    /// </para>
    /// </summary>
    public static class RealtimeThrottle
    {
        private static readonly ConcurrentDictionary<string, long> LastSentTicks = new();

        // Lug'at cheksiz o'smasligi uchun chegara (kalitlar: kamera/shaxs/turniket).
        private const int MaxKeys = 2000;
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);

        /// <summary>
        /// <paramref name="key"/> uchun oxirgi ruxsatdan beri <paramref name="interval"/>
        /// o'tgan bo'lsa <c>true</c> qaytaradi va vaqtni yangilaydi; aks holda <c>false</c>.
        /// Thread-safe.
        /// </summary>
        public static bool TryAcquire(string key, TimeSpan interval)
        {
            var now = Environment.TickCount64;
            var windowMs = (long)interval.TotalMilliseconds;

            while (true)
            {
                if (!LastSentTicks.TryGetValue(key, out var last))
                {
                    if (LastSentTicks.TryAdd(key, now))
                    {
                        PruneIfNeeded(now);
                        return true;
                    }
                    continue; // boshqa oqim ulgurdi — qaytadan tekshiramiz
                }

                if (now - last < windowMs)
                    return false;

                if (LastSentTicks.TryUpdate(key, now, last))
                    return true;
            }
        }

        private static void PruneIfNeeded(long now)
        {
            if (LastSentTicks.Count <= MaxKeys) return;

            var staleMs = (long)StaleAfter.TotalMilliseconds;
            foreach (var pair in LastSentTicks)
            {
                if (now - pair.Value > staleMs)
                    LastSentTicks.TryRemove(pair.Key, out _);
            }
        }
    }
}
