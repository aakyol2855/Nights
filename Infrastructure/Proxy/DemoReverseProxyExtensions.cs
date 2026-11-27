using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Nights.Services.Interfaces;
using Yarp.ReverseProxy.Forwarder;

namespace Nights.Infrastructure.Proxy;

/// <summary>
/// YARP (Yet Another Reverse Proxy) Direct Forwarding ile /live-demo/{slug}/ trafiğini
/// dinamik olarak ayağa kalkan 127.0.0.1:51xx container portuna yönlendirir.
/// Her istekte ilgili demonun LastActivityUtc zaman damgasını günceller.
/// </summary>
public static partial class DemoReverseProxyExtensions
{
    // HTML içindeki kök-göreli (href="/...", src="/...", action="/...") yolları
    // /live-demo/{slug}/... altına güvenli şekilde yeniden yazar.
    [GeneratedRegex(@"(?<attr>\b(?:href|src|action|data-url)\s*=\s*)(?<quote>[""'])/(?!/|live-demo/)(?<rest>[^""']*)\k<quote>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HtmlRootRelativeAttrRegex();

    /// <summary>
    /// Demo container içindeki uygulamaların JavaScript (fetch/$.ajax) veya kök-göreli isteklerini
    /// Referer başlığı üzerinden tespit ederek DİVAN ana sitesine düşmeden aktif /live-demo/{slug}/
    /// proxy yoluna yönlendirir. UseStaticFiles() ve UseRouting() öncesinde çağrılmalıdır.
    /// </summary>
    public static IApplicationBuilder UseLiveDemoSubpathRouting(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // Zaten /live-demo/ veya /api/demos/ altındaysa dokunma
            if (!path.StartsWith("/live-demo", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("/api/demos", StringComparison.OrdinalIgnoreCase))
            {
                var referer = context.Request.Headers.Referer.ToString();
                if (TryExtractLiveDemoSlugFromReferer(referer, out var slug))
                {
                    var orchestrator = context.RequestServices.GetRequiredService<IDockerOrchestratorService>();
                    var session = orchestrator.GetActiveSessionBySlug(slug);
                    if (session != null)
                    {
                        var prefix = $"/live-demo/{slug}";
                        var fetchMode = context.Request.Headers["Sec-Fetch-Mode"].ToString();
                        var fetchDest = context.Request.Headers["Sec-Fetch-Dest"].ToString();

                        // Tam sayfa/iframe GET gezintilerinde tarayıcı adresini /live-demo/{slug}/ altında tutmak için yönlendir
                        var isDocumentNavigation =
                            HttpMethods.IsGet(context.Request.Method) &&
                            (string.Equals(fetchMode, "navigate", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(fetchDest, "iframe", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(fetchDest, "document", StringComparison.OrdinalIgnoreCase));

                        if (isDocumentNavigation)
                        {
                            context.Response.Redirect($"{prefix}{path}{context.Request.QueryString}", permanent: false);
                            return;
                        }

                        // Statik dosyalar (CSS, JS, görsel), form POST ve AJAX çağrılarını dahili olarak proxy yoluna çevir
                        context.Request.Path = new PathString($"{prefix}{path}");
                    }
                }
            }

            await next();
        });
    }

    public static IEndpointRouteBuilder MapLiveDemoProxy(this IEndpointRouteBuilder endpoints)
    {
        var httpClient = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            EnableMultipleHttp2Connections = false,
            ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
            ConnectTimeout = TimeSpan.FromSeconds(10)
        });

        var requestConfig = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromSeconds(60)
        };

        // /live-demo/{slug} ve /live-demo/{slug}/{**catch-all}
        endpoints.Map("/live-demo/{slug}/{**catch-all}", async (
            HttpContext httpContext,
            string slug,
            IHttpForwarder forwarder,
            IDockerOrchestratorService orchestrator,
            ILoggerFactory loggerFactory) =>
        {
            var rawPath = httpContext.Request.Path.Value ?? string.Empty;
            var exactPrefix = $"/live-demo/{slug}";
            if (string.Equals(rawPath, exactPrefix, StringComparison.OrdinalIgnoreCase))
            {
                httpContext.Response.Redirect($"{exactPrefix}/{httpContext.Request.QueryString}", permanent: false);
                return;
            }

            var session = orchestrator.GetActiveSessionBySlug(slug);
            if (session == null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                httpContext.Response.ContentType = "text/html; charset=utf-8";
                httpContext.Response.Headers.Remove("X-Frame-Options");
                httpContext.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self'";
                await httpContext.Response.WriteAsync(BuildSleepingDemoHtml(slug));
                return;
            }

            // Her proxy isteğinde 15 dakikalık uyku sayacını sıfırla
            orchestrator.TouchActivity(slug);

            var destinationPrefix = $"http://127.0.0.1:{session.HostPort}";
            var accept = httpContext.Request.Headers.Accept.ToString();
            var mightBeHtml = accept.Contains("text/html", StringComparison.OrdinalIgnoreCase) && !HasStaticAssetExtension(rawPath);
            var transformer = new DemoSubpathTransformer(exactPrefix, destinationPrefix, stripAcceptEncodingForHtml: mightBeHtml);

            if (!mightBeHtml)
            {
                // Statik dosyalar (CSS, JS, görsel, font, API/JSON) doğrudan sıfır-kopya stream edilir
                var directError = await forwarder.SendAsync(httpContext, destinationPrefix, httpClient, requestConfig, transformer);
                if (directError != ForwarderError.None)
                {
                    LogForwarderError(loggerFactory, httpContext, slug, session.HostPort, directError);
                }
                return;
            }

            // HTML yanıtlarını tamponlayarak href="/...", src="/...", action="/..." yollarını
            // doğrudan /live-demo/{slug}/... altına yeniden yaz (DİVAN'ın /css/site.css önbellek çakışmasını %100 önler)
            var originalBody = httpContext.Response.Body;
            await using var buffer = new MemoryStream();
            httpContext.Response.Body = buffer;

            try
            {
                var error = await forwarder.SendAsync(httpContext, destinationPrefix, httpClient, requestConfig, transformer);
                if (error != ForwarderError.None)
                {
                    LogForwarderError(loggerFactory, httpContext, slug, session.HostPort, error);
                }

                buffer.Position = 0;
                var contentType = httpContext.Response.ContentType ?? string.Empty;
                var contentEncoding = httpContext.Response.Headers.ContentEncoding.ToString();

                if (buffer.Length > 0 &&
                    contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(contentEncoding))
                {
                    using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                    var html = await reader.ReadToEndAsync(httpContext.RequestAborted);

                    var rewrittenHtml = HtmlRootRelativeAttrRegex().Replace(
                        html,
                        match => $"{match.Groups["attr"].Value}{match.Groups["quote"].Value}{exactPrefix}/{match.Groups["rest"].Value}{match.Groups["quote"].Value}");

                    var rewrittenBytes = Encoding.UTF8.GetBytes(rewrittenHtml);
                    httpContext.Response.Headers.Remove("Transfer-Encoding");
                    httpContext.Response.ContentLength = rewrittenBytes.Length;

                    httpContext.Response.Body = originalBody;
                    await originalBody.WriteAsync(rewrittenBytes, httpContext.RequestAborted);
                }
                else
                {
                    httpContext.Response.Body = originalBody;
                    if (buffer.Length > 0)
                    {
                        await buffer.CopyToAsync(originalBody, httpContext.RequestAborted);
                    }
                }
            }
            finally
            {
                httpContext.Response.Body = originalBody;
            }
        });

        return endpoints;
    }

    private static void LogForwarderError(
        ILoggerFactory loggerFactory,
        HttpContext httpContext,
        string slug,
        int hostPort,
        ForwarderError error)
    {
        var logger = loggerFactory.CreateLogger("LiveDemoProxy");
        var errorFeature = httpContext.GetForwarderErrorFeature();
        logger.LogWarning(errorFeature?.Exception, "[LiveDemoProxy] Yönlendirme hatası. Slug={Slug}, Port={Port}, Error={Error}",
            slug, hostPort, error);
    }

    private static bool HasStaticAssetExtension(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) return false;

        return ext.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".map", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".woff", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".woff2", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".eot", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractLiveDemoSlugFromReferer(string? referer, out string slug)
    {
        slug = string.Empty;
        if (string.IsNullOrWhiteSpace(referer) || !Uri.TryCreate(referer, UriKind.Absolute, out var uri))
        {
            return false;
        }

        const string marker = "/live-demo/";
        var absPath = uri.AbsolutePath;
        if (!absPath.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = absPath[marker.Length..];
        var slashIdx = remainder.IndexOf('/');
        var candidate = slashIdx >= 0 ? remainder[..slashIdx] : remainder;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        slug = candidate;
        return true;
    }

    private sealed class DemoSubpathTransformer : HttpTransformer
    {
        private readonly string _prefix;
        private readonly string _destinationPrefix;
        private readonly bool _stripAcceptEncodingForHtml;

        public DemoSubpathTransformer(string prefix, string destinationPrefix, bool stripAcceptEncodingForHtml)
        {
            _prefix = prefix.TrimEnd('/');
            _destinationPrefix = destinationPrefix.TrimEnd('/');
            _stripAcceptEncodingForHtml = stripAcceptEncodingForHtml;
        }

        public override async ValueTask TransformRequestAsync(
            HttpContext httpContext,
            HttpRequestMessage proxyRequest,
            string destinationPrefix,
            CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

            var incomingPath = httpContext.Request.Path.Value ?? "/";
            var strippedPath = incomingPath.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase)
                ? incomingPath[_prefix.Length..]
                : incomingPath;

            if (string.IsNullOrEmpty(strippedPath))
            {
                strippedPath = "/";
            }

            proxyRequest.RequestUri = RequestUtilities.MakeDestinationAddress(
                destinationPrefix,
                new PathString(strippedPath),
                httpContext.Request.QueryString);

            // Hedef Kestrel'e yerel Host bilgisinin temiz gitmesini sağla
            proxyRequest.Headers.Host = null;

            // HTML yeniden yazımı yapılacaksa hedef container'dan sıkıştırılmamış düz HTML iste
            if (_stripAcceptEncodingForHtml)
            {
                proxyRequest.Headers.Remove("Accept-Encoding");
            }

            // Cloudflare Tunnel (HTTPS) ve alt yol (Subpath) uyumluluğu için standart Forwarded başlıklarını ilet
            var scheme = string.IsNullOrWhiteSpace(httpContext.Request.Scheme) ? "https" : httpContext.Request.Scheme;
            proxyRequest.Headers.Remove("X-Forwarded-Proto");
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", scheme);

            if (httpContext.Request.Host.HasValue)
            {
                proxyRequest.Headers.Remove("X-Forwarded-Host");
                proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", httpContext.Request.Host.Value);
            }

            proxyRequest.Headers.Remove("X-Forwarded-Prefix");
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Prefix", _prefix);
        }

        public override async ValueTask<bool> TransformResponseAsync(
            HttpContext httpContext,
            HttpResponseMessage? proxyResponse,
            CancellationToken cancellationToken)
        {
            // 1. Demo container'dan gelen iframe engelleyici başlıkları proxy yanıtından temizle
            if (proxyResponse != null)
            {
                proxyResponse.Headers.Remove("X-Frame-Options");
                proxyResponse.Headers.Remove("Content-Security-Policy");
                proxyResponse.Headers.Remove("Content-Security-Policy-Report-Only");
            }

            var result = await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);

            // 2. Yanıt başlıklarında kalan X-Frame-Options / CSP varsa kesin temizle ve frame-ancestors 'self' uygula
            httpContext.Response.Headers.Remove("X-Frame-Options");
            httpContext.Response.Headers.Remove("Content-Security-Policy");
            httpContext.Response.Headers.Remove("Content-Security-Policy-Report-Only");
            httpContext.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self'";

            // 3. Yönlendirme (Location) başlıklarını relative /live-demo/{slug}/... formatına uyarla
            if (httpContext.Response.Headers.TryGetValue("Location", out var locations))
            {
                var location = locations.ToString();
                var rewritten = RewriteLocationHeader(location, _prefix, _destinationPrefix, httpContext.Request.Host.Value);
                if (!string.Equals(location, rewritten, StringComparison.Ordinal))
                {
                    httpContext.Response.Headers["Location"] = rewritten;
                }
            }

            return result;
        }

        private static string RewriteLocationHeader(
            string location,
            string prefix,
            string destinationPrefix,
            string? externalHost)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return location;
            }

            if (location.StartsWith('/') && !location.StartsWith("//", StringComparison.Ordinal))
            {
                return location.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? location
                    : $"{prefix}{location}";
            }

            if (Uri.TryCreate(location, UriKind.Absolute, out var uri))
            {
                var isInternalLoopback =
                    uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    location.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase);

                var isSameExternalHost =
                    !string.IsNullOrWhiteSpace(externalHost) &&
                    (uri.Authority.Equals(externalHost, StringComparison.OrdinalIgnoreCase) ||
                     uri.Host.Equals(externalHost, StringComparison.OrdinalIgnoreCase));

                if (isInternalLoopback || isSameExternalHost)
                {
                    var pathAndQuery = uri.PathAndQuery;
                    var fragment = uri.Fragment ?? string.Empty;
                    if (!pathAndQuery.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{prefix}{pathAndQuery}{fragment}";
                    }
                    return $"{pathAndQuery}{fragment}";
                }
            }

            return location;
        }
    }

    private static string BuildSleepingDemoHtml(string slug)
    {
        var safeSlug = WebUtility.HtmlEncode(slug);
        var isEn = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var lang = isEn ? "en" : "tr";
        var title = isEn ? "Live Demo Sleeping - DIVAN" : "Canlı Demo Uyku Modunda - DİVAN";
        var heading = isEn ? "Live Demo Is Currently Sleeping" : "Canlı Demo Şu An Uyku Modunda";
        var body = isEn
            ? $"The <strong>{safeSlug}</strong> demo environment is currently stopped to conserve server resources or automatically went to sleep after 15 minutes of inactivity."
            : $"<strong>{safeSlug}</strong> demo ortamı donanım tasarrufu gereği şu anda kapalıdır veya 15 dakikalık hareketsizlik süresi dolduğu için otomatik olarak durdurulmuştur.";
        var btn = isEn ? "✦ Return to Projects" : "✦ İmalathane (Projeler) Sayfasına Dön";

        return $$"""
            <!DOCTYPE html>
            <html lang="{{lang}}">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>{{title}}</title>
                <style>
                    body { background:#080D14; color:#F8F6F0; font-family: system-ui, -apple-system, sans-serif; display:flex; align-items:center; justify-content:center; min-height:100vh; margin:0; padding:1.5rem; }
                    .box { max-width:500px; text-align:center; padding:2.5rem 2rem; background:rgba(15,24,38,0.92); border:1px solid rgba(212,175,95,0.45); border-radius:6px; }
                    .crest { font-size:2.5rem; color:#F7D688; margin-bottom:0.75rem; }
                    h1 { font-size:1.5rem; color:#FFFFFF; margin:0 0 0.75rem; }
                    p { color:#CBD5E1; font-size:0.95rem; line-height:1.6; margin:0 0 1.5rem; }
                    a { display:inline-block; padding:0.7rem 1.4rem; background:#163E33; color:#FFFFFF; text-decoration:none; border:1px solid #C5A059; border-radius:4px; font-weight:600; }
                    a:hover { background:#1C4D40; }
                </style>
            </head>
            <body>
                <div class="box">
                    <div class="crest">۞</div>
                    <h1>{{heading}}</h1>
                    <p>{{body}}</p>
                    <a href="/Projects" target="_top">{{btn}}</a>
                </div>
            </body>
            </html>
            """;
    }
}
