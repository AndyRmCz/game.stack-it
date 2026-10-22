using System;
using System.Text;

namespace StackIt.Client
{
    /// <summary>Extracts challenge codes from links, install referrers and typed input.</summary>
    public static class DeepLinks
    {
        // Same alphabet as the server's codes (no 0/O, 1/I/L, U).
        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const int MinLength = 4;
        private const int MaxLength = 10;

        /// <summary>
        /// Accepts https://{base}/c/{code} (Universal Links / App Links) and {scheme}://c/{code}.
        /// Anything else, including other hosts, returns null.
        /// </summary>
        public static string ParseChallengeCode(string url, string baseUrl, string scheme)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri uri)) return null;

            string path;
            if (string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase))
            {
                // stackit://c/CODE parses with Host = "c".
                if (!string.Equals(uri.Host, "c", StringComparison.OrdinalIgnoreCase)) return null;
                path = uri.AbsolutePath.Trim('/');
            }
            else
            {
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri expected)) return null;
                if (!string.Equals(uri.Host, expected.Host, StringComparison.OrdinalIgnoreCase)) return null;
                if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;
                string p = uri.AbsolutePath.Trim('/');
                if (!p.StartsWith("c/", StringComparison.OrdinalIgnoreCase)) return null;
                path = p.Substring(2);
            }
            return Validate(path);
        }

        /// <summary>Android Play Install Referrer, e.g. "utm_source=x&amp;challenge=K7QX9PA".</summary>
        public static string ParseInstallReferrer(string referrer)
        {
            if (string.IsNullOrEmpty(referrer)) return null;
            foreach (string pair in Uri.UnescapeDataString(referrer).Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair.Substring(0, eq) == "challenge") return Validate(pair.Substring(eq + 1));
            }
            return null;
        }

        /// <summary>Code typed by hand in the "Got a challenge code?" box.</summary>
        public static string NormalizeTypedCode(string typed)
        {
            if (typed == null) return null;
            var sb = new StringBuilder();
            foreach (char c in typed)
                if (!char.IsWhiteSpace(c) && c != '-') sb.Append(c);
            return Validate(sb.ToString());
        }

        private static string Validate(string candidate)
        {
            if (string.IsNullOrEmpty(candidate)) return null;
            string code = candidate.Trim().ToUpperInvariant();
            if (code.Length < MinLength || code.Length > MaxLength) return null;
            foreach (char c in code)
                if (Alphabet.IndexOf(c) < 0) return null;
            return code;
        }
    }
}
