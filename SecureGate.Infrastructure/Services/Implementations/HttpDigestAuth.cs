using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace SecureGate.Infrastructure.Services.Implementations;

/// <summary>
/// HTTP Digest (RFC 2617 / RFC 7616 MD5, qop=auth) autentifikatsiyasini qo'lda hisoblaydi.
///
/// <para>
/// <b>Nima uchun qo'lda:</b> <c>HttpClientHandler.Credentials</c> handler darajasida o'rnatiladi,
/// <see cref="IHttpClientFactory"/> esa handler'ni barcha so'rovlar uchun umumiy qilib qayta ishlatadi.
/// Bizda esa har bir NVR ning O'Z login/paroli bor — umumiy handler'ga bitta
/// <c>NetworkCredential</c> qo'yib bo'lmaydi (kameralar bir-birining parolini ishlatib yuborardi).
/// Shu sababli challenge/response so'rov darajasida hisoblanadi — bu thread-safe va
/// har bir kamera uchun to'g'ri.
/// </para>
/// </summary>
internal static class HttpDigestAuth
{
    /// <summary>
    /// 401 javobidagi Digest challenge asosida <c>Authorization</c> sarlavhasi qiymatini yasaydi.
    /// Challenge tushunarsiz bo'lsa (yoki Digest bo'lmasa) — null.
    /// </summary>
    /// <param name="challenge">WWW-Authenticate sarlavhasidagi Digest parametrlari.</param>
    /// <param name="httpMethod">GET / POST ...</param>
    /// <param name="requestUri">So'rov URI'si — digest'da uning PathAndQuery qismi ishlatiladi.</param>
    public static string? BuildDigestHeader(
        string challenge, string httpMethod, Uri requestUri, string username, string password)
    {
        var p = ParseParameters(challenge);

        if (!p.TryGetValue("nonce", out var nonce)) return null;
        p.TryGetValue("realm", out var realm);
        p.TryGetValue("qop", out var qopRaw);
        p.TryGetValue("opaque", out var opaque);
        p.TryGetValue("algorithm", out var algorithm);

        realm ??= string.Empty;
        algorithm = string.IsNullOrWhiteSpace(algorithm) ? "MD5" : algorithm.Trim();

        // Faqat MD5 / MD5-sess qo'llab-quvvatlanadi — Hikvision ISAPI aynan shularni ishlatadi.
        var sess = algorithm.EndsWith("-sess", StringComparison.OrdinalIgnoreCase);
        if (!algorithm.StartsWith("MD5", StringComparison.OrdinalIgnoreCase)) return null;

        // qop ro'yxatdan berilishi mumkin: qop="auth,auth-int" — biz faqat "auth" ni olamiz.
        string? qop = null;
        if (!string.IsNullOrWhiteSpace(qopRaw))
        {
            var options = qopRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (options.Any(o => string.Equals(o, "auth", StringComparison.OrdinalIgnoreCase))) qop = "auth";
            else return null; // auth-int qo'llab-quvvatlanmaydi
        }

        var digestUri = requestUri.PathAndQuery;
        var cnonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        const string nc = "00000001";

        var ha1 = Md5($"{username}:{realm}:{password}");
        if (sess) ha1 = Md5($"{ha1}:{nonce}:{cnonce}");

        var ha2 = Md5($"{httpMethod.ToUpperInvariant()}:{digestUri}");

        var response = qop is null
            ? Md5($"{ha1}:{nonce}:{ha2}")
            : Md5($"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}");

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"username=\"{Escape(username)}\"");
        sb.Append(CultureInfo.InvariantCulture, $", realm=\"{Escape(realm)}\"");
        sb.Append(CultureInfo.InvariantCulture, $", nonce=\"{Escape(nonce)}\"");
        sb.Append(CultureInfo.InvariantCulture, $", uri=\"{digestUri}\"");
        sb.Append(CultureInfo.InvariantCulture, $", algorithm={algorithm}");
        if (qop is not null)
        {
            sb.Append(CultureInfo.InvariantCulture, $", qop={qop}");
            sb.Append(CultureInfo.InvariantCulture, $", nc={nc}");
            sb.Append(CultureInfo.InvariantCulture, $", cnonce=\"{cnonce}\"");
        }
        sb.Append(CultureInfo.InvariantCulture, $", response=\"{response}\"");
        if (!string.IsNullOrEmpty(opaque))
            sb.Append(CultureInfo.InvariantCulture, $", opaque=\"{Escape(opaque)}\"");

        return sb.ToString();
    }

    /// <summary>Basic sxemasi uchun sarlavha qiymati (fallback).</summary>
    public static string BuildBasicHeader(string username, string password) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

    /// <summary>Javobdagi WWW-Authenticate sarlavhalaridan kerakli sxemani topadi.</summary>
    public static AuthenticationHeaderValue? FindChallenge(HttpResponseMessage response, string scheme) =>
        response.Headers.WwwAuthenticate
            .FirstOrDefault(h => string.Equals(h.Scheme, scheme, StringComparison.OrdinalIgnoreCase));

    // ===== Yordamchilar =====

    // MD5 bu yerda protokol talabi (RFC 2617) — kriptografik maqsadda emas.
    private static string Md5(string input) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    private static string Escape(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

    /// <summary>
    /// <c>realm="x", nonce="y", qop=auth</c> ko'rinishidagi parametrlarni ajratadi.
    /// Qiymat ichidagi vergul (masalan qop="auth,auth-int") to'g'ri hisobga olinadi.
    /// </summary>
    private static Dictionary<string, string> ParseParameters(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input)) return result;

        var i = 0;
        while (i < input.Length)
        {
            while (i < input.Length && (char.IsWhiteSpace(input[i]) || input[i] == ',')) i++;
            if (i >= input.Length) break;

            var nameStart = i;
            while (i < input.Length && input[i] != '=' && input[i] != ',') i++;
            var name = input[nameStart..i].Trim();

            if (i >= input.Length || input[i] != '=')
            {
                if (name.Length > 0) result[name] = string.Empty;
                continue;
            }

            i++; // '=' dan o'tamiz

            string value;
            if (i < input.Length && input[i] == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < input.Length && input[i] != '"')
                {
                    if (input[i] == '\\' && i + 1 < input.Length) i++;
                    sb.Append(input[i]);
                    i++;
                }
                if (i < input.Length) i++; // yopuvchi qo'shtirnoq
                value = sb.ToString();
            }
            else
            {
                var valueStart = i;
                while (i < input.Length && input[i] != ',') i++;
                value = input[valueStart..i].Trim();
            }

            if (name.Length > 0) result[name] = value;
        }

        return result;
    }
}
