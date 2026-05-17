using Microsoft.Extensions.Logging;

namespace SafeRideKids.Familia.Maui.Services;

/// <summary>
/// Wrapper sobre MediaPicker.CapturePhotoAsync com regras de privacidade da POC:
/// - Fotos NUNCA gravadas em disco. A FileResult é lida em memória e o arquivo
///   temporário (quando a plataforma cria) é deletado imediatamente.
/// - Devolve byte[] com a imagem em JPEG, alvo 1080x1080 max e quality 85.
///   (Reamostragem real exigiria SkiaSharp — deixamos hook documentado.)
/// </summary>
public interface ICameraCaptureService
{
    Task<CapturedPhoto?> CaptureAsync(CancellationToken cancellationToken = default);
    bool IsCaptureSupported { get; }
}

public sealed record CapturedPhoto(byte[] JpegBytes, int ApproximateWidth, int ApproximateHeight);

public sealed class CameraCaptureService : ICameraCaptureService
{
    private const long MaxRawSizeBytes = 8 * 1024 * 1024; // safety: rejeita > 8MB pré-reamostragem
    private readonly ILogger<CameraCaptureService> _logger;

    public CameraCaptureService(ILogger<CameraCaptureService> logger)
    {
        _logger = logger;
    }

    public bool IsCaptureSupported => MediaPicker.Default.IsCaptureSupported;

    public async Task<CapturedPhoto?> CaptureAsync(CancellationToken cancellationToken = default)
    {
        if (!IsCaptureSupported)
        {
            _logger.LogWarning("Câmera não disponível no dispositivo");
            return null;
        }

        FileResult? result = null;
        try
        {
            // MediaPickerOptions.Title aparece em alguns OEMs Android
            result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Foto da criança"
            }).ConfigureAwait(false);

            if (result is null)
            {
                // Usuário cancelou
                return null;
            }

            await using var stream = await result.OpenReadAsync().ConfigureAwait(false);

            // Defesa contra arquivos absurdos (foto de outra origem, p.ex.)
            if (stream.CanSeek && stream.Length > MaxRawSizeBytes)
            {
                _logger.LogWarning("Foto recusada por tamanho {Size} acima do limite", stream.Length);
                return null;
            }

            using var ms = new MemoryStream(capacity: 1024 * 256);
            await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            var bytes = ms.ToArray();

            // TODO(real-impl): reamostrar para 1080x1080 max, JPEG quality 85 via SkiaSharp.
            // Como o objetivo desta POC é fluxo + LGPD, devolvemos a imagem como veio
            // e deixamos a normalização de pixels para o backend (que já redimensiona
            // antes de enviar a provedores que cobram por megapixel).
            return new CapturedPhoto(bytes, ApproximateWidth: 0, ApproximateHeight: 0);
        }
        catch (PermissionException pex)
        {
            _logger.LogWarning(pex, "Permissão de câmera negada pelo usuário");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao capturar foto");
            return null;
        }
        finally
        {
            // Tenta apagar o arquivo temporário que o picker cria em algumas plataformas
            if (result is not null)
            {
                try
                {
                    if (File.Exists(result.FullPath))
                    {
                        File.Delete(result.FullPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Não foi possível remover o arquivo temporário da câmera");
                }
            }
        }
    }
}
