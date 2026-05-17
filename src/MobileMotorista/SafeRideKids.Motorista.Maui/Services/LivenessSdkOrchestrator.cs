using SafeRideKids.Motorista.Maui.Models;

namespace SafeRideKids.Motorista.Maui.Services;

// Resultado da invocacao simulada do SDK de liveness.
public sealed record LivenessSdkResult(
    bool Captured,
    string SessionId,
    string ProviderId,
    string? FailureReason);

public interface ILivenessSdkOrchestrator
{
    // Decide qual SDK invocar (AWS vs Unico) com base no providerId vindo do backend.
    // Retorna o sessionId para o backend conseguir consultar VerifyAsync.
    // IMPORTANTE: nada e gravado em disco. Buffer fica no SDK nativo (em producao).
    Task<LivenessSdkResult> RunLivenessAsync(
        string providerId,
        LivenessSessionInfoDto sessionInfo,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}

// Orquestrador POC: stub que apenas simula o tempo de captura.
// Substituir cada metodo *Stub por chamada real ao SDK nativo via DependencyService /
// PartialClass por plataforma (Platforms/Android e Platforms/iOS).
public sealed class LivenessSdkOrchestrator : ILivenessSdkOrchestrator
{
    public async Task<LivenessSdkResult> RunLivenessAsync(
        string providerId,
        LivenessSessionInfoDto sessionInfo,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        // Timeout de 30s conforme requisito do prompt; quem chama trata fallback.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            return providerId switch
            {
                "aws-rekognition" => await RunAwsRekognitionStubAsync(sessionInfo, progress, timeoutCts.Token),
                "unico-idcloud" => await RunUnicoStubAsync(sessionInfo, progress, timeoutCts.Token),
                _ => new LivenessSdkResult(false, sessionInfo.SessionId, providerId, "provedor desconhecido")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout interno de 30s — fluxo deve cair em fallback.
            return new LivenessSdkResult(false, sessionInfo.SessionId, providerId, "timeout_30s");
        }
    }

    // TODO[liveness-sdk] — AWS Rekognition Face Liveness.
    // SDK mobile oficial: https://docs.aws.amazon.com/rekognition/latest/dg/face-liveness-mobile.html
    // Android: amazonaws.amplifyframework:aws-predictions
    // iOS: AWSPredictionsPlugin (Swift Package)
    // Aqui entraria FaceLivenessDetector com:
    //   sessionId = sessionInfo.SessionId
    //   region = sessionInfo.SdkConfig["region"]
    //   credentialsProvider = guest identity pool definida no backend
    // O SDK abre uma view nativa fullscreen com instrucoes ("Centralize o rosto",
    // "Pisque", "Vire a cabeca") e retorna sucesso/falha; nada e gravado no disco do device.
    private static async Task<LivenessSdkResult> RunAwsRekognitionStubAsync(
        LivenessSessionInfoDto sessionInfo,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        progress?.Report("Iniciando AWS Rekognition Face Liveness...");
        await Task.Delay(800, ct);
        progress?.Report("Centralize o rosto da crianca no oval");
        await Task.Delay(1500, ct);
        progress?.Report("Pisque duas vezes");
        await Task.Delay(1500, ct);
        progress?.Report("Aguarde o resultado...");
        await Task.Delay(700, ct);
        return new LivenessSdkResult(true, sessionInfo.SessionId, "aws-rekognition", null);
    }

    // TODO[liveness-sdk] — Unico IDCloud.
    // Docs SDK mobile: https://developers.unico.io/docs/idcloud/sdk-mobile
    // Android: io.unico.idcloud:idcloud-sdk
    // iOS: UnicoCheck via SPM
    // Aqui entraria UnicoCheckBuilder com:
    //   bundleId / hostKey vindos de sessionInfo.SdkConfig
    //   callback que devolve UnicoSelfieResult.encrypted (base64)
    // POC: Unico encapsula liveness+match em uma chamada so; o sessionId que voltar
    // aqui sera o que o backend usa em VerifyAsync.
    private static async Task<LivenessSdkResult> RunUnicoStubAsync(
        LivenessSessionInfoDto sessionInfo,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        progress?.Report("Iniciando Unico Check...");
        await Task.Delay(900, ct);
        progress?.Report("Posicione a face no marcador");
        await Task.Delay(1800, ct);
        progress?.Report("Aguarde a captura...");
        await Task.Delay(1300, ct);
        return new LivenessSdkResult(true, sessionInfo.SessionId, "unico-idcloud", null);
    }
}
